// 以下为 Alpaca 美股经纪服务类的带注释实现（中英文 XML 注释）
using Alpaca.Markets;
using Microsoft.Data.Analysis;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Broker.Model;
using Quant.Infra.Net.Portfolio.Models;
using Quant.Infra.Net.Shared.Extension;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service.Historical;
using Quant.Infra.Net.SourceData.Service.RealTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;


namespace Quant.Infra.Net.Broker.Service
{
    /// <summary>
    /// 美股 Alpaca 经纪服务实现类。
    /// Broker service implementation for U.S. equities using Alpaca API.
    /// </summary>
    public class USEquityAlpacaBrokerService : IUSEquityBrokerService, IHistoricalDataSourceServiceTraditionalFinance, IRealtimeDataSourceServiceTraditionalFinance
    {
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly string _apiKey, _apiSecret;
        private readonly AlpacaClient _alpacaClient;
        IAlpacaTradingClient _alpacaTradingClient;


        /// <summary>
        /// 当前交易环境（实盘 / 模拟盘）。
        /// Current exchange environment (e.g., Live or Paper).
        /// </summary>
        public ExchangeEnvironment ExchangeEnvironment { get; set; }

        public Currency BaseCurrency { get; set; }

        /// <summary>
        /// 构造函数，初始化 API 密钥与重试策略。
        /// Constructor that initializes API credentials and retry policy.
        /// </summary>
        /// <param name="configuration">配置文件接口。</param>
        public USEquityAlpacaBrokerService(IConfiguration configuration)
        {
            _apiKey = configuration["Exchange:ApiKey"];
            _apiSecret = configuration["Exchange:ApiSecret"];
            ExchangeEnvironment = (ExchangeEnvironment)Enum.Parse(typeof(ExchangeEnvironment), configuration["Exchange:Environment"].ToString());

            _alpacaClient = new AlpacaClient(new BrokerCredentials { ApiKey = _apiKey, Secret = _apiSecret }, ExchangeEnvironment);
            // 初始化_tradingClient
            var credentials = new SecretKey(_apiKey, _apiSecret);
            _alpacaTradingClient = ExchangeEnvironment == ExchangeEnvironment.Live
                ? Environments.Live.GetAlpacaTradingClient(credentials)
                : Environments.Paper.GetAlpacaTradingClient(credentials);

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))); // 指数退避策略
        }

        /// <summary>
        /// 获取当前投资组合的市值（USD）。
        /// Get the total market value of the current portfolio.
        /// </summary>
        public async Task<decimal> GetAccountEquityAsync()
        {
            return await _retryPolicy.ExecuteAsync(async () => await _alpacaClient.GetAccountEquityAsync());
        }

        /// <summary>
        /// 获取未实现盈亏比率（总浮盈/浮亏）。
        /// Get unrealized profit/loss rate.
        /// </summary>
        public async Task<double> GetUnrealizedProfitRateAsync()
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var positions = await _alpacaClient.GetAllPositionsAsync();
                decimal totalCost = 0;
                decimal totalValue = 0;

                foreach (var pos in positions)
                {
                    // 注意：pos.Quantity 可为负，表示做空
                    totalCost += pos.AverageEntryPrice * pos.Quantity;
                    totalValue += pos.MarketValue.Value;
                }

                var pnl = totalValue - totalCost;
                var rate = totalCost != 0 ? (double)(pnl / Math.Abs(totalCost)) : 0.0;
                return rate;
            });
        }


        /// <summary>
        /// 检查指定股票是否持有仓位。
        /// Check if a position exists for the given symbol.
        /// </summary>
        /// <param name="symbol">股票代码，例如 AAPL。</param>
        public async Task<bool> HasPositionAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentNullException(nameof(symbol), "Symbol cannot be null or empty.");

            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var position = await _alpacaClient.GetPositionAsync(symbol);
                return position != null && position.Quantity != 0;
            });
        }

        /// <summary>
        /// 平掉指定股票的所有持仓。
        /// Liquidate the position for the given symbol.
        /// </summary>
        /// <param name="symbol">要平仓的股票代码。</param>
        public async Task LiquidateAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentNullException(nameof(symbol), "Symbol cannot be null or empty.");

            await _retryPolicy.ExecuteAsync(async () =>
            {
                await _alpacaClient.ExitPositionAsync(symbol);
                return Task.CompletedTask;
            });
        }


        /// <summary>
        /// 设置指定股票的持仓比例（如 0.1 表示持仓市值占总资产 10%）。
        /// Set target position for a symbol as a percentage of total portfolio value.
        /// </summary>
        /// <param name="symbol">股票代码。</param>
        /// <param name="rate">目标持仓比例。</param>
        public async Task SetHoldingsAsync(string symbol, double rate)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentNullException(nameof(symbol), "Symbol cannot be null or empty.");

            if (rate < -1.0 || rate > 1.0)
                throw new ArgumentOutOfRangeException(nameof(rate), "Rate must be between -1.0 and 1.0.");

            var asset = await _alpacaClient.GetAssetAsync(symbol);
            if (!asset.IsTradable)
                throw new InvalidOperationException($"{symbol} is not tradable.");

            await _retryPolicy.ExecuteAsync(async () =>
            {
                var asset = await _alpacaClient.GetAssetAsync(symbol);
                if (!asset.IsTradable)
                    throw new InvalidOperationException($"{symbol} is not tradable.");

                if (rate < 0 && !asset.Shortable)
                    throw new InvalidOperationException($"{symbol} is not shortable but a short position was requested.");

                var accountEquity = await _alpacaClient.GetAccountEquityAsync();
                var latestPrice = await _alpacaClient.GetLatestPriceAsync(symbol);

                var targetMarketValue = accountEquity * (decimal)rate;
                var targetShares = targetMarketValue / latestPrice;

                var existingPosition = await _alpacaClient.GetPositionAsync(symbol);
                var currentShares = existingPosition?.Quantity ?? 0m;
                var diffShares = targetShares - currentShares;

                if (Math.Abs(diffShares) < 0.0001m)
                    return; // 差距极小则忽略

                await _alpacaClient.PlaceOrderAsync(symbol, diffShares);
            });



        }


        public async Task<string> GetFormattedAccountSummaryAsync()
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _alpacaClient.GetFormattedAccountSummaryAsync();
            });
        }



        /// <summary>
        /// 拉取指定时间区间内的 OHLCV 数据，并返回一个 DataFrame。
        /// Retrieve OHLCV data in the [startDate, endDate] interval and return as a DataFrame.
        /// </summary>
        /// <param name="underlying">标的资产信息，包括 Symbol 和 AssetType。  
        /// Underlying asset info, including Symbol and AssetType.</param>
        /// <param name="startDate">时间区间开始（包含）。  
        /// Interval start date (inclusive).</param>
        /// <param name="endDate">时间区间结束（包含）。  
        /// Interval end date (inclusive).</param>
        /// <param name="resolutionLevel">数据分辨率。  
        /// Desired data resolution.</param>
        /// <returns>包含 DateTime、Open、High、Low、Close、Volume、AdjustedClose 列的 DataFrame。  
        /// A DataFrame with columns: DateTime, Open, High, Low, Close, Volume, AdjustedClose.</returns>
        /// <exception cref="ArgumentNullException">当 underlying 为 null 时抛出。  
        /// Thrown if underlying is null.</exception>
        /// <exception cref="ArgumentException">当 endDate <= startDate 时抛出。  
        /// Thrown if endDate is before or equal to startDate.</exception>
        public async Task<DataFrame> GetHistoricalDataFrameAsync(
            Underlying underlying,
            DateTime startDate,
            DateTime endDate,
            ResolutionLevel resolutionLevel)
        {
            if (underlying == null)
                throw new ArgumentNullException(nameof(underlying));
            if (endDate <= startDate)
                throw new ArgumentException("endDate must be after startDate", nameof(endDate));

            var ohlcvs = await GetOhlcvListAsync(underlying, startDate, endDate, resolutionLevel);

            var times = ohlcvs.Select(x => x.OpenDateTime);
            var opens = ohlcvs.Select(x => x.Open);
            var highs = ohlcvs.Select(x => x.High);
            var lows = ohlcvs.Select(x => x.Low);
            var closes = ohlcvs.Select(x => x.Close);
            var vols = ohlcvs.Select(x => x.Volume);
            var adjCloses = ohlcvs.Select(x => x.AdjustedClose);

            var df = new DataFrame();
            df.Columns.Add(new PrimitiveDataFrameColumn<DateTime>("DateTime", times));
            df.Columns.Add(new PrimitiveDataFrameColumn<decimal>("Open", opens));
            df.Columns.Add(new PrimitiveDataFrameColumn<decimal>("High", highs));
            df.Columns.Add(new PrimitiveDataFrameColumn<decimal>("Low", lows));
            df.Columns.Add(new PrimitiveDataFrameColumn<decimal>("Close", closes));
            df.Columns.Add(new PrimitiveDataFrameColumn<decimal>("Volume", vols));
            df.Columns.Add(new PrimitiveDataFrameColumn<decimal>("AdjustedClose", adjCloses));

            return df;
        }


        /// <summary>
        /// 获取给定标的的最新成交价格。
        /// Get the latest trade price for the specified underlying asset.
        /// </summary>
        /// <param name="underlying">
        /// 标的资产信息，包括 Symbol 和 AssetType。  
        /// Underlying asset info, including Symbol and AssetType.
        /// </param>
        /// <returns>
        /// 最新价格（以 decimal 表示）。  
        /// The latest price as a decimal.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// 当 underlying 为 null 时抛出。  
        /// Thrown if the underlying argument is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// 当不支持该 AssetType 时抛出。  
        /// Thrown if the asset type is not supported by Alpaca.
        /// </exception>
        public async Task<decimal> GetLatestPriceAsync(Underlying underlying)
        {
            if (underlying == null)
                throw new ArgumentNullException(nameof(underlying));

            // Alpaca 目前仅支持传统美股（Stock）和加密现货（CryptoSpot）
            switch (underlying.AssetType)
            {
                case AssetType.UsEquity:
                    // 通过 AlpacaClient 获取最新成交价
                    return await _alpacaClient.GetLatestPriceAsync(underlying.Symbol);

                case AssetType.CryptoSpot:
                    // 如果 Alpaca 开通了加密现货接口，也可走同一方法
                    return await _alpacaClient.GetLatestPriceAsync(underlying.Symbol);

                default:
                    throw new InvalidOperationException(
                        $"AssetType '{underlying.AssetType}' is not supported for price retrieval.");
            }
        }



        /// <summary>
        /// 获取给定交易对的最新 OHLCV 数据，从 <paramref name="endDt"/> 向前拉取 <paramref name="limit"/> 条记录。
        /// Retrieve the most recent <paramref name="limit"/> OHLCV bars ending at <paramref name="endDt"/>.
        /// </summary>
        /// <param name="underlying">标的资产信息，包括 Symbol 和 AssetType。  
        /// Underlying asset info, including Symbol and AssetType.</param>
        /// <param name="endDt">拉取截止时间（包含）。  
        /// End datetime (inclusive) for retrieval.</param>
        /// <param name="limit">需要返回的 K 线条数。  
        /// Number of bars to retrieve.</param>
        /// <param name="resolutionLevel">数据分辨率（分钟/小时/日）。  
        /// Desired data resolution (e.g., Minute, Hourly, Daily).</param>
        /// <returns>按时间正序排列的 <paramref name="limit"/> 条 OHLCV 数据。  
        /// An ordered sequence of the most recent <paramref name="limit"/> OHLCV bars.</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="underlying"/> 为 null 时抛出。  
        /// Thrown if <paramref name="underlying"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="limit"/> ≤ 0 时抛出。  
        /// Thrown if <paramref name="limit"/> is not positive.</exception>
        public async Task<IEnumerable<Ohlcv>> GetOhlcvListAsync(
            Underlying underlying,
            DateTime endDt,
            int limit,
            ResolutionLevel resolutionLevel = ResolutionLevel.Hourly)
        {
            if (underlying == null)
                throw new ArgumentNullException(nameof(underlying));
            if (limit <= 0)
                throw new ArgumentOutOfRangeException(nameof(limit), "limit must be positive");
            if (resolutionLevel == ResolutionLevel.Daily)
            {
                var historicalOhlcvsBeforeToday = await GetHistoricalBarsAsync(
                    underlying,
                    endUtc: endDt,
                    limit: limit,
                    resolutionLevel);

                // Get today's ohlcv instance
                var todayOhlcv = await GetTodayOhlcvAsync(underlying);

                // 合并，用倒叙方式，只保留正确的limit个ohlcv; 作为res
                var ohlcvs = await MergeAsync(historicalOhlcvsBeforeToday, todayOhlcv, limit);

                // 返回;
                return ohlcvs;

            }
            else
            {
                return await GetHistoricalBarsAsync(
                    underlying,
                    endUtc: endDt,
                    limit: limit,
                    resolutionLevel);
            }

        }

        /// <summary>
        /// 分钟级获取今天的 OHLCV 数据，最终合并为一个 Ohlcv 实例。如果今天不是交易日或市场未开盘，则返回 null。  
        /// Retrieve today’s in-progress OHLCV by fetching minute bars since market open and aggregating.  
        /// Returns null if today is not a trading day or market is not open yet.
        /// </summary>
        public async Task<Ohlcv> GetTodayOhlcvAsync(Underlying underlying)
        {
            if (underlying == null)
                throw new ArgumentNullException(nameof(underlying));

            bool isMarketOpening = await _alpacaClient.IsMarketOpenNowAsync();
            if (isMarketOpening == false)
                return null;


            // 1) 当前 UTC 时间
            var nowUtc = DateTime.UtcNow;

            // 2) 计算今天美东开盘时间对应的 UTC：09:30 ET = 13:30 UTC
            var marketOpenUtc = new DateTime(
                nowUtc.Year, nowUtc.Month, nowUtc.Day,
                13, 30, 0, DateTimeKind.Utc);

            // 如果未到开盘，返回 null
            if (nowUtc < marketOpenUtc)
                return null;

            // 3) 计算已过去的分钟数 + 1
            int minutesSinceOpen = (int)Math.Ceiling((nowUtc - marketOpenUtc).TotalMinutes) + 1;

            // 4) 拉取从开盘到现在的分钟级数据
            var bars = (await GetOhlcvListAsync(
                underlying,
                endDt: nowUtc,
                limit: minutesSinceOpen,
                resolutionLevel: ResolutionLevel.Minute))
                .Where(b => b.OpenDateTime.Date == nowUtc.Date)
                .ToList();

            if (!bars.Any())
                return null;

            // 5) 聚合为单个 Ohlcv
            var first = bars.First();
            var last = bars.Last();
            return new Ohlcv
            {
                Symbol = underlying.Symbol,
                OpenDateTime = first.OpenDateTime,
                CloseDateTime = nowUtc,
                Open = first.Open,
                High = bars.Max(b => b.High),
                Low = bars.Min(b => b.Low),
                Close = last.Close,
                Volume = bars.Sum(b => b.Volume),
                AdjustedClose = last.Close
            };
        }


       

        /// <summary>
        /// 合并历史 OHLCV 列表与今天的未完成 Ohlcv，倒序保留最新 limit 条后再升序返回。  
        /// If today's ohlcv is null, returns up to the last limit historical bars.
        /// </summary>
        public async Task<IEnumerable<Ohlcv>> MergeAsync(IEnumerable<Ohlcv> historical, Ohlcv todayOhlcv, int limit)
        {
            if (todayOhlcv == null)
                return historical
                    .Reverse()
                    .Take(limit)
                    .Reverse()
                    .ToList();

            var list = new List<Ohlcv>(historical ?? Enumerable.Empty<Ohlcv>());

            // If the last historical bar is from today, replace it with the up-to-date todayOhlcv
            if (list.Any() && list.Last().OpenDateTime.Date == todayOhlcv.OpenDateTime.Date)
            {
                list[list.Count - 1] = todayOhlcv;
            }
            else
            {
                // Otherwise append today's bar
                list.Add(todayOhlcv);
            }

            // 倒序取最新 limit 条，再升序
            var result = list
                .AsEnumerable()
                .Reverse()
                .Take(limit)
                .Reverse()
                .ToList();

            return result;
        }


        /// <summary>
        /// 获取给定交易对在指定时间区间 [<paramref name="startDt"/>, <paramref name="endDt"/>] 内的全部 OHLCV 数据。
        /// Retrieve all OHLCV bars between <paramref name="startDt"/> and <paramref name="endDt"/> inclusive.
        /// </summary>
        /// <param name="underlying">标的资产信息，包括 Symbol 和 AssetType。  
        /// Underlying asset info, including Symbol and AssetType.</param>
        /// <param name="startDt">时间区间开始（包含）。  
        /// Start datetime (inclusive) for retrieval.</param>
        /// <param name="endDt">时间区间结束（包含）。  
        /// End datetime (inclusive) for retrieval.</param>
        /// <param name="resolutionLevel">数据分辨率（分钟/小时/日）。  
        /// Desired data resolution (e.g., Minute, Hourly, Daily).</param>
        /// <returns>按时间正序排列的 OHLCV 数据序列，覆盖整个指定区间。  
        /// An ordered sequence of OHLCV bars covering the specified interval.</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="underlying"/> 为 null 时抛出。  
        /// Thrown if <paramref name="underlying"/> is null.</exception>
        /// <exception cref="ArgumentException">当 <paramref name="endDt"/> ≤ <paramref name="startDt"/> 时抛出。  
        /// Thrown if <paramref name="endDt"/> is before or equal to <paramref name="startDt"/>.</exception>
        public Task<IEnumerable<Ohlcv>> GetOhlcvListAsync(
            Underlying underlying,
            DateTime startDt,
            DateTime endDt,
            ResolutionLevel resolutionLevel = ResolutionLevel.Hourly)
        {
            // 在 [startDt, endDt] 范围内拉全量
            return FetchOhlcvAsync(
                underlying,
                startUtc: startDt.ToUniversalTime(),
                endUtc: endDt.ToUniversalTime(),
                limit: null,
                resolutionLevel);
        }




        /// <summary>
        /// 核心拉取、映射并（可选）聚合 OHLCV 数据的方法。
        /// </summary>
        private async Task<IEnumerable<Ohlcv>> FetchOhlcvAsync(
            Underlying underlying,
            DateTime startUtc,
            DateTime endUtc,
            int? limit,
            ResolutionLevel resolutionLevel)
        {
            // 1) 校验
            if (underlying == null) throw new ArgumentNullException(nameof(underlying));
            if (endUtc <= startUtc && limit == null)
                throw new ArgumentException("endUtc must be after startUtc when limit is not specified");

            // 2) 确定分页参数
            var tf = resolutionLevel == ResolutionLevel.Daily ? BarTimeFrame.Day : BarTimeFrame.Minute;
            var aggregateHr = resolutionLevel == ResolutionLevel.Hourly;
            var needed = limit.HasValue
                                 ? (aggregateHr ? limit.Value * 60 : limit.Value)
                                 : int.MaxValue;  // 如果不按 limit 拉，就用时间窗口决定何时停

            var rawBars = new List<IBar>();
            var cursor = endUtc;
            const int pageSize = 1000;

            // 3) 分页拉取
            while (rawBars.Count < needed)
            {
                var take = limit.HasValue
                    ? Math.Min(pageSize, needed - rawBars.Count)
                    : pageSize;

                var req = new HistoricalBarsRequest(
                    underlying.Symbol,
                    startUtc,
                    cursor,
                    tf
                ).WithPageSize((uint)take);

                var page = await _alpacaClient.ListHistoricalBarsAsync(req);
                if (page == null || page.Count == 0) break;

                rawBars.InsertRange(0, page);
                cursor = page.Min(b => b.TimeUtc).AddSeconds(-1);

                // 如果是按时间窗口拉，当已跑过 startUtc，就停
                if (!limit.HasValue && cursor <= startUtc) break;
            }

            // 4) 映射到 Ohlcv，并按时间正序
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

            // 5) 如果是按小时聚合
            if (aggregateHr)
            {
                mapped = mapped
                    .GroupBy(o => new DateTime(
                        o.OpenDateTime.Year,
                        o.OpenDateTime.Month,
                        o.OpenDateTime.Day,
                        o.OpenDateTime.Hour, 0, 0, DateTimeKind.Utc))
                    .Select(g =>
                    {
                        var first = g.First();
                        var last = g.Last();
                        return new Ohlcv
                        {
                            Symbol = first.Symbol,
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

            // 6) 如果指定了 limit，则取最后 limit 条
            if (limit.HasValue && mapped.Count > limit.Value)
                return mapped.Skip(mapped.Count - limit.Value);

            // 否则，返回整个时间窗口的数据
            return mapped;
        }


        /// <summary>
        /// 从 endUtc 向前取 limit 条 OHLCV 数据。
        /// </summary>
        private async Task<IEnumerable<Ohlcv>> GetHistoricalBarsAsync(
            Underlying underlying,
            DateTime endUtc,
            int limit,
            ResolutionLevel resolutionLevel)
        {
            // 1) 校验
            if (underlying == null) throw new ArgumentNullException(nameof(underlying));
            if (limit <= 0) throw new ArgumentException("limit must be > 0", nameof(limit));


            // 只保留最后 limit 根，按时间升序
            var rawBars = await _alpacaClient.GetHistoricalBarsAsync(underlying,endUtc,limit,resolutionLevel);

            return rawBars;
        }


        public async Task<bool> IsMarketOpeningAsync()
        {
            return await _alpacaClient.IsMarketOpenNowAsync();
        }

        public async Task<IAccount> GetAccountAsync()
        {
            // 直接调用 Alpaca SDK
            var account = await _alpacaTradingClient.GetAccountAsync(); 
            return account;
        }

        public async Task<Position> GetPositionAsync(string symbol)
        {
            try
            {
                // 1) 从 Alpaca SDK 拉取该标的的持仓信息
                var alpacaPos = await _alpacaTradingClient.GetPositionAsync(symbol);

                // 2) 将返回的 IPosition 映射到我们自己的 Position 类型
                return new Position
                {
                    Symbol = alpacaPos.Symbol,
                    Quantity = alpacaPos.Quantity,                      // 持仓数量
                    CostPrice = alpacaPos.AverageEntryPrice,             // 平均建仓成本
                    UnrealizedProfitLoss = alpacaPos.UnrealizedProfitLoss          // 未实现盈亏
                };
            }
            catch (RestClientErrorException e) when (e.ErrorCode == (int)HttpStatusCode.NotFound)
            {
                // 如果 Alpaca 返回 404，说明该标的没有持仓，返回一个空仓位
                return new Position
                {
                    Symbol = symbol,
                    Quantity = 0m,
                    CostPrice = 0m,
                    UnrealizedProfitLoss = 0m
                };
            }
        }

        public async Task<IAccount> GetAlpacaAccountAsync()
        {
            // 通过 _alpacaTradingClient 获取账户信息并返回 RegtBuyingPower 字段
            var account = await _alpacaTradingClient.GetAccountAsync();
            return account;
        }

        public async Task PlaceOrderAsync(
            Underlying underlying, 
            int quality,
            OrderExecutionType orderType = OrderExecutionType.Market,
            TimeInForce timeInForce = TimeInForce.GoodTillCanceled,
            bool afterHours = false
            )
        {
            var timeInForceAlpaca = AlpacaMarketsExtension.ToAlpacaTimeInForce(timeInForce);
            var orderTypeAlpaca = AlpacaMarketsExtension.ToAlpacaOrderType(orderType);
            await _alpacaClient.PlaceOrderAsync(underlying.Symbol, quality, orderTypeAlpaca, timeInForceAlpaca, afterHours);
        }
    }
}