using Alpaca.Markets;
using Polly;
using Polly.Retry;
using Quant.Infra.Net.Broker.Model;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;
using Quant.Infra.Net.SourceData.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Broker.Service
{
    public class AlpacaClient
    {
        private readonly IAlpacaTradingClient _tradeClient;
        private readonly IAlpacaDataClient _dataClient;

        // 在类顶端添加这个静态节流控制器（例如每秒最多 5 个请求）
        private static readonly SemaphoreSlim _rateLimiter = new SemaphoreSlim(5);

        // SSL异常+限流重试策略增强版（添加 HttpRequestException + inner SSL 检查）
        private static readonly AsyncRetryPolicy _rateLimitRetryPolicy = Policy
            .Handle<RestClientErrorException>(ex => ex.HttpStatusCode == HttpStatusCode.TooManyRequests)
            .Or<HttpRequestException>(ex => ex.InnerException?.Message?.Contains("SSL") == true)
            .Or<TimeoutException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 5,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(3, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    var inner = exception.InnerException?.Message ?? "";
                    UtilityService.LogAndWriteLine(
                        $"[WARN] 请求失败（{exception.Message} | Inner: {inner}），将在{timeSpan.TotalSeconds}秒后重试(第{retryCount}次)。");
                });

        // 历史数据缓存
        private static readonly ConcurrentDictionary<string, (DateTime expiry, IReadOnlyList<IBar> data)> _barCache = new();

        public AlpacaClient(BrokerCredentials creds, ExchangeEnvironment mode)
        {
            var env = mode == ExchangeEnvironment.Paper ? Environments.Paper : Environments.Live;
            _tradeClient = env.GetAlpacaTradingClient(new SecretKey(creds.ApiKey, creds.Secret));
            _dataClient = env.GetAlpacaDataClient(new SecretKey(creds.ApiKey, creds.Secret));
        }

        /// <summary>
        /// 获取所有当前持仓。
        /// Get all current positions in the account.
        /// </summary>
        public async Task<IReadOnlyList<IPosition>> GetAllPositionsAsync() =>
            await _tradeClient.ListPositionsAsync();

        /// <summary>
        /// 获取指定标的的持仓（可能为 null）。
        /// Get the position for a given symbol, or null if not held.
        /// </summary>
        public async Task<IPosition> GetPositionAsync(string symbol)
        {
            try
            {
                return await _tradeClient.GetPositionAsync(symbol);
            }
            catch (Exception ex)
            {
#if DEBUG
                UtilityService.LogAndWriteLine($"[DEBUG] GetPositionAsync failed: {ex.Message}");
#endif
                return null;
            }
        }

        /// <summary>
        /// 获取账户中可用现金。
        /// Get available tradable cash in the account.
        /// </summary>
        public async Task<decimal> GetCashValueAsync()
        {
            var account = await _tradeClient.GetAccountAsync();
            return account.TradableCash;
        }


        /// <summary>
        /// 获取当前账户净值。
        /// Get current total equity of the account.
        /// </summary>
        public async Task<decimal> GetAccountEquityAsync()
        {
            var account = await _tradeClient.GetAccountAsync();
            return account.Equity ?? 0;
        }


        /// <summary>
        /// 获取指定标的的最新成交价格。
        /// Get latest trade price for a given symbol.
        /// </summary>
        public async Task<decimal> GetLatestPriceAsync(string symbol)
        {
            var request = new LatestMarketDataRequest(symbol);
            var quote = await _dataClient.GetLatestTradeAsync(request);
            return quote.Price;
        }


        /// <summary>
        /// 提交一个市价/限价单，可设定买卖方向、盘后交易，并自动跳过重复挂单。
        /// Submit a market or limit order with side, extended-hours support, and duplicate-order prevention.
        /// </summary>
        /// <param name="symbol">
        /// 标的代码 / Symbol. 
        /// Cannot be null or whitespace.
        /// </param>
        /// <param name="quantity">
        /// 数量, 小数会被向下取整为整数；
        /// positive = buy, negative = sell. 
        /// Cannot be 0.
        /// </param>
        /// <param name="orderType">
        /// 订单类型（市价/限价等）/ Order type. Default is Market.
        /// </param>
        /// <param name="timeInForce">
        /// 订单有效时间（Day, GTC）/ Time in force. Default is Day.
        /// </param>
        /// <param name="afterHours">
        /// 是否允许盘后交易 / Allow extended-hours. Default is false.
        /// </param>
        public async Task PlaceOrderAsync(
            string symbol,
            decimal quantity,
            OrderType orderType = OrderType.Market,
            Alpaca.Markets.TimeInForce timeInForce = Alpaca.Markets.TimeInForce.Day,
            bool afterHours = false)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentNullException(nameof(symbol), "Symbol cannot be null or empty.");
            if (quantity == 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be 0.");

            // Determine side and absolute quantity
            var side = quantity > 0 ? OrderSide.Buy : OrderSide.Sell;
            var qty = Math.Abs(quantity);
            var qtyValue = OrderQuantity.FromInt64((int)qty);

            // —— 修改点：使用 StatusFilter = OrderStatusFilter.Open 来获取所有挂单（包括 New 和 PartiallyFilled） —— 
            var listRequest = new ListOrdersRequest
            {
                LimitOrderNumber = 100,
                OrderStatusFilter = Alpaca.Markets.OrderStatusFilter.Open
            };
            var pending = await _tradeClient.ListOrdersAsync(listRequest);

            // 如果已存在相同 symbol、方向、数量的挂单，则跳过
            if (pending.Any(x =>
                    x.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) &&
                    x.OrderSide == side &&
                    x.IntegerQuantity == qtyValue))
            {
                Console.WriteLine($"[{DateTime.UtcNow:O}] Skipping duplicate order: {symbol} {side} {qty}");
                return;
            }

            // 构造下单请求 / build new order request
            var request = new NewOrderRequest(symbol, qtyValue, side, orderType, timeInForce)
            {
                ExtendedHours = afterHours
            };

            // 发送至 Alpaca / submit to broker
            await _tradeClient.PostOrderAsync(request);
        }



        /// <summary>
        /// 平掉指定标的的所有仓位。
        /// Close (liquidate) all shares of a specific symbol.
        /// </summary>
        public async Task ExitPositionAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentNullException(nameof(symbol), "Symbol cannot be null or empty.");

            try
            {
                await _tradeClient.DeletePositionAsync(new DeletePositionRequest(symbol));
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// 获取所有未成交订单（开盘中）。
        /// Get all open/pending orders.
        /// </summary>
        public async Task<List<Model.OpenOrder>> GetOpenOrdersAsync()
        {
            var rawOrders = await _tradeClient.ListOrdersAsync(new ListOrdersRequest
            {
                OrderStatusFilter = OrderStatusFilter.Open
            });

            return rawOrders.Select(x => new Model.OpenOrder
            {
                Symbol = x.Symbol,
                Quantity = x.Quantity,
                FilledQuantity = x.FilledQuantity,
                OrderId = x.OrderId,
                Status = x.OrderStatus.ToString(),
                CreatedAtUtc = x.CreatedAtUtc
            }).ToList();
        }




        /// <summary>
        /// 获取交易所当前时间和状态。
        /// Get market clock and open/close status.
        /// </summary>
        public Alpaca.Markets.IClock GetClock() => _tradeClient.GetClockAsync().Result;

        /// <summary>
        /// 当前市场是否开盘。
        /// Is the market currently open?
        /// </summary>
        public async Task<bool> IsMarketOpenNowAsync()
        {
            try
            {
                var clock = await _tradeClient.GetClockAsync();
                return clock.IsOpen;
            }
            catch (Exception ex) { UtilityService.LogAndWriteLine(ex.ToString()); return false; }
        }

        /// <summary>
        /// 撤销指定订单。
        /// Cancel an open order by order ID.
        /// </summary>
        public void CancelOrder(Guid orderId)
        {
            if (orderId == Guid.Empty)
                throw new ArgumentException("Order ID cannot be empty.", nameof(orderId));

            try { _tradeClient.CancelOrderAsync(orderId).Wait(); }
            catch (Exception ex) { UtilityService.LogAndWriteLine("CancelOrder error: " + ex.Message); }
        }

        /// <summary>
        /// 获取指定股票的资产信息（是否可交易、可做空等）。
        /// Get asset metadata such as tradability and shortability.
        /// </summary>
        public async Task<IAsset> GetAssetAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentNullException(nameof(symbol), "Symbol cannot be null or empty.");

            return await _tradeClient.GetAssetAsync(symbol);
        }


        /// <summary>
        /// Get a formatted account summary string (in English).
        /// Includes equity, cash, and current positions.
        /// </summary>
        public async Task<string> GetFormattedAccountSummaryAsync()
        {
            var cash = await GetCashValueAsync();
            var equity = await GetAccountEquityAsync();
            var positions = await GetAllPositionsAsync();

            // force U.S. dollar formatting
            var us = CultureInfo.GetCultureInfo("en-US");

            // build lines
            var lines = new List<string>
            {
                "[Account Summary]",
                $"Total Equity     : {equity.ToString("C2", us)}",
                $"Available Cash   : {cash.ToString("C2", us)}",
                $"Open Positions count   : {positions.Count}"
            };

            foreach (var pos in positions)
            {
                lines.Add(string.Format(us,
                    "- {0} | Qty: {1} | Entry Avg: {2:C2} | Market Value: {3:C2} | Unrealized PnL: {4:C2}",
                    pos.Symbol,
                    pos.Quantity,
                    pos.AverageEntryPrice,
                    pos.MarketValue,
                    pos.UnrealizedProfitLoss));
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// 带缓存的历史K线数据获取
        /// </summary>
        public async Task<IReadOnlyList<IBar>> ListHistoricalBarsAsync(HistoricalBarsRequest req)
        {
            req.Feed = MarketDataFeed.Iex;

            // 生成缓存键
            var cacheKey = $"{req.Symbols}-{req.TimeFrame}-{req.TimeInterval.From}-{req.TimeInterval.Into}";

            // 检查缓存
            if (_barCache.TryGetValue(cacheKey, out var cached) && cached.expiry > DateTime.UtcNow)
            {
                return cached.data;
            }

            // 没有缓存或已过期，从API获取
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

            var page = await _rateLimitRetryPolicy.ExecuteAsync(
                ct => _dataClient.ListHistoricalBarsAsync(req, ct),
                cts.Token
            );

            // 存入缓存(5分钟有效期)
            _barCache[cacheKey] = (DateTime.UtcNow.AddMinutes(5), page.Items);

            return page.Items;
        }
    
        public async Task<IEnumerable<Ohlcv>> GetHistoricalBarsAsync(
            Underlying underlying,
            DateTime endUtc,
            int limit,
            ResolutionLevel resolutionLevel)
        {
            if (underlying == null) throw new ArgumentNullException(nameof(underlying));
            if (limit <= 0) throw new ArgumentException("limit必须大于0", nameof(limit));

            var startUtc = await CalculateUSEquityStartUtcAsync(endUtc, limit, resolutionLevel);
            startUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
            endUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);

            BarTimeFrame tf = resolutionLevel switch
            {
                ResolutionLevel.Minute => BarTimeFrame.Minute,
                ResolutionLevel.Hourly => BarTimeFrame.Hour,
                ResolutionLevel.Daily => BarTimeFrame.Day,
                _ => throw new NotSupportedException($"不支持的时间级别: {resolutionLevel}")
            };

            const int MaxPageSize = 1000;
            int pageSize = Math.Min(limit, MaxPageSize);

            var allBars = new List<IBar>();
            var req = new HistoricalBarsRequest(
                    underlying.Symbol,
                    from: startUtc,
                    into: endUtc,
                    tf
                )
                .WithPageSize((uint)pageSize);
            req.SortDirection = SortDirection.Descending; // 降序获取最新数据
            req.Feed = MarketDataFeed.Iex; // 使用 IEX 数据源

            // 第一次请求
            var page = await _rateLimitRetryPolicy.ExecuteAsync(async () =>
            {
                await _rateLimiter.WaitAsync();
                try
                {
                    return await _dataClient.ListHistoricalBarsAsync(req);
                }
                finally
                {
                    _rateLimiter.Release();
                }
            });
            allBars.AddRange(page.Items);

            // 后续翻页请求
            while (allBars.Count < limit && !string.IsNullOrEmpty(page.NextPageToken))
            {
                await Task.Delay(100); // 延迟（避免重试和连接复用问题）

                req = req.WithPageToken(page.NextPageToken);

                page = await _rateLimitRetryPolicy.ExecuteAsync(async () =>
                {
                    await _rateLimiter.WaitAsync();
                    try
                    {
                        return await _dataClient.ListHistoricalBarsAsync(req);
                    }
                    finally
                    {
                        _rateLimiter.Release();
                    }
                });

                allBars.AddRange(page.Items);
            }

            var rawBars = allBars
                .OrderBy(b => b.TimeUtc)
                .TakeLast(limit)
                .ToList();

            var mapped = rawBars
                .Select(b => new Ohlcv
                {
                    Symbol = underlying.Symbol,
                    OpenDateTime = b.TimeUtc,
                    CloseDateTime = b.TimeUtc,
                    Open = b.Open,
                    High = b.High,
                    Low = b.Low,
                    Close = b.Close,
                    Volume = b.Volume,
                    AdjustedClose = b.Close
                })
                .OrderBy(o => o.OpenDateTime)
                .ToList();

            if (resolutionLevel == ResolutionLevel.Hourly)
            {
                mapped = mapped
                    .GroupBy(o => new DateTime(o.OpenDateTime.Year, o.OpenDateTime.Month, o.OpenDateTime.Day, o.OpenDateTime.Hour, 0, 0, DateTimeKind.Utc))
                    .Select(g =>
                    {
                        var first = g.First();
                        var last = g.Last();
                        return new Ohlcv
                        {
                            Symbol = underlying.Symbol,
                            OpenDateTime = g.Key,
                            CloseDateTime = g.Key.AddHours(1).AddSeconds(-1),
                            Open = first.Open,
                            High = g.Max(x => x.High),
                            Low = g.Min(x => x.Low),
                            Close = last.Close,
                            Volume = g.Sum(x => x.Volume),
                            AdjustedClose = last.Close
                        };
                    })
                    .OrderBy(o => o.OpenDateTime)
                    .ToList();
            }

            return mapped;
        }

        /// <summary>
        /// 分批获取历史数据
        /// </summary>
        public async Task<IEnumerable<Ohlcv>> GetHistoricalBarsBatchAsync(
            Underlying underlying,
            DateTime startUtc,
            DateTime endUtc,
            ResolutionLevel resolutionLevel,
            int batchSize = 500)
        {
            var result = new List<Ohlcv>();
            var currentEnd = endUtc;

            while (currentEnd > startUtc)
            {
                var batch = await GetHistoricalBarsAsync(
                    underlying,
                    currentEnd,
                    batchSize,
                    resolutionLevel);

                if (!batch.Any())
                    break;

                result.AddRange(batch);
                currentEnd = batch.Min(x => x.OpenDateTime) - TimeSpan.FromSeconds(1);

                // 如果不是最后一次迭代，添加延迟
                if (currentEnd > startUtc)
                {
                    await Task.Delay(2000); // 批次间延迟2秒
                }
            }

            return result
                .Where(x => x.OpenDateTime >= startUtc)
                .OrderBy(x => x.OpenDateTime)
                .ToList();
        }

        /// <summary>
        /// 【中】根据分辨率和条数，计算美国股票市场的回溯起始 UTC 时间：
        /// – Tick/Second/Minute：直接按时间偏移  
        /// – Hourly：按交易小时回溯（每天 6.5 小时交易）  
        /// – Daily：按交易日回溯（每年平均 252 交易日）  
        /// – Weekly：按交易周回溯（每周 5 个交易日）  
        /// 【EN】Compute the back‐offset startUtc for U.S. equities given resolution and count:
        /// – Tick/Second/Minute: simple subtract  
        /// – Hourly: subtract trading‐hours (6.5h/day) via trading‐day lookup + extra hours  
        /// – Daily: subtract trading days (≈252/year)  
        /// – Weekly: subtract trading weeks (5 days/week)
        /// </summary>
        public async Task<DateTime> CalculateUSEquityStartUtcAsync(
            DateTime endUtc,
            int count,
            ResolutionLevel resolutionLevel)
        {
            const double TradingHoursPerDay = 6.5;
            switch (resolutionLevel)
            {
                case ResolutionLevel.Tick:
                case ResolutionLevel.Second:
                    return endUtc.AddSeconds(-count);

                case ResolutionLevel.Minute:
                    return endUtc.AddMinutes(-count);

                case ResolutionLevel.Hourly:
                    {
                        // 1) 计算需要回溯的整交易日数
                        int fullTradingDays = (int)Math.Floor(count / TradingHoursPerDay);
                        // 2) 余下的交易小时
                        double extraHours = count - fullTradingDays * TradingHoursPerDay;

                        // 3) 先按交易日回溯 fullTradingDays + 1 天（加 1 天作 buffer）
                        var dayBack = fullTradingDays + 1;
                        var dayStart = await CalculateByTradingDaysAsync(endUtc, dayBack);
                        dayStart = DateTime.SpecifyKind(dayStart, DateTimeKind.Utc);

                        // 4) 再从当日截点减去余下交易小时
                        var startUtc = dayStart
                            .AddHours(9.5)             // 美股交易日通常从 09:30 ET（13:30 UTC）开始
                            .AddHours(-extraHours);    // 去掉余下交易小时

                        return startUtc;

                    }

                case ResolutionLevel.Daily:
                    // 精确回溯交易日
                    return await CalculateByTradingDaysAsync(endUtc, count);

                case ResolutionLevel.Weekly:
                    // 按 5 交易日/周 回溯
                    return await CalculateByTradingDaysAsync(endUtc, count * 5);

                case ResolutionLevel.Monthly:
                    return endUtc.AddMonths(-count);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(resolutionLevel), resolutionLevel,
                        "Unsupported resolution for startUtc calc");
            }
        }



        /// <summary>
        /// 核心：按交易日数回溯，返回startDateUtc;
        /// Core: Calculate the UTC start time by looking back a given number of trading days.
        /// </summary>
        /// <param name="endUtc">
        /// 结束时间（UTC）  
        /// End UTC datetime.
        /// </param>
        /// <param name="tradingDaysBack">
        /// 回溯的交易日天数  
        /// Number of trading days to look back.
        /// </param>
        /// <returns>
        /// 起始时间（UTC）  
        /// Start UTC datetime.
        /// </returns>
        private async Task<DateTime> CalculateByTradingDaysAsync(
            DateTime endUtc,
            int tradingDaysBack)
        {
            // 1. 按比例估算需要回溯的日历天数，并加上 buffer
            const double tradingDaysPerYear = 252.0;
            const double calendarDaysPerYear = 365.0;
            const int bufferDays = 10;

            int estimatedCalendarDays = (int)Math.Ceiling(
                    tradingDaysBack * (calendarDaysPerYear / tradingDaysPerYear))
                + bufferDays;

            // 2. 直接用一次请求拉取这段区间的交易日历
            var lookbackStart =
                endUtc.AddDays(-estimatedCalendarDays);

            return lookbackStart;
        }
    }
}