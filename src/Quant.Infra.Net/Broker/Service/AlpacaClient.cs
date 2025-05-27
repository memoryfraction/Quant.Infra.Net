using Alpaca.Markets;
using NodaTime;
using Quant.Infra.Net.Broker.Model;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;


namespace Quant.Infra.Net.Broker.Service
{
    /// <summary>
    /// Alpaca 客户端封装类，封装交易和市场数据 API。
    /// Encapsulates Alpaca trading and data client functionality.
    /// </summary>
    public class AlpacaClient
    {
        private readonly IAlpacaTradingClient _tradeClient;
        private readonly IAlpacaDataClient _dataClient;

        /// <summary>
        /// 构造函数，基于实盘或模拟盘创建 Alpaca 客户端。
        /// Constructor: initialize Alpaca clients using paper or live environment.
        /// </summary>
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
        /// 提交一个市价/限价单，可设定买卖方向、盘后交易等。
        /// Submit a market or limit order with side and extended-hours support.
        /// </summary>
        /// <param name="symbol">标的代码 / Symbol</param>
        /// <param name="quantity">数量,小数会被处理为整数，正为买入，负为卖出 / Quantity (positive = buy, negative = sell)</param>
        /// <param name="orderType">订单类型（市价、限价等）/ Order type</param>
        /// <param name="timeInForce">订单有效时间（Day, GTC）/ Time in force</param>
        /// <param name="afterHours">是否盘后交易 / Allow extended hours</param>
        public async Task PlaceOrderAsync(string symbol, decimal quantity, OrderType orderType = OrderType.Market, Alpaca.Markets.TimeInForce timeInForce = Alpaca.Markets.TimeInForce.Day, bool afterHours = false)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentNullException(nameof(symbol), "Symbol cannot be null or empty.");
            if (quantity == 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be 0.");

            var side = quantity > 0 ? OrderSide.Buy : OrderSide.Sell;
            var qty = Math.Abs(quantity);

            OrderQuantity qtyValue = OrderQuantity.FromInt64((int)qty);

            var request = new NewOrderRequest(symbol, qtyValue, side, orderType, timeInForce)
            {
                ExtendedHours = afterHours
            };

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
        /// 薄封装：拉取历史 K 线（Bars），返回 Items 列表
        /// </summary>
        public async Task<IReadOnlyList<IBar>> ListHistoricalBarsAsync(HistoricalBarsRequest req)
        {
            // 指定只从 IEX 拉取数据
            req.Feed = MarketDataFeed.Iex;
            var response = await _dataClient.ListHistoricalBarsAsync(req);        
            return response.Items;
        }



        /// <summary>
        /// 根据 分辨率 + 条数，计算回溯的 startUtc
        /// – Tick/Second/Minute/Hourly/Monthly：直接按时间偏移
        /// – Daily：按交易日数回溯（避免周末/节假日）
        /// – Weekly：按交易周数回溯（每周取周一开盘）
        /// </summary>
        public async Task<DateTime> CalculateStartUtcAsync(
            DateTime endUtc,
            int count,
            ResolutionLevel resolutionLevel)
        {
            switch (resolutionLevel)
            {
                case ResolutionLevel.Tick:
                case ResolutionLevel.Second:
                    return endUtc.AddSeconds(-count);

                case ResolutionLevel.Minute:
                    return endUtc.AddMinutes(-count);

                case ResolutionLevel.Hourly:
                    return endUtc.AddHours(-count);

                case ResolutionLevel.Monthly:
                    return endUtc.AddMonths(-count);

                case ResolutionLevel.Daily:
                    return await CalculateByTradingDaysAsync(endUtc, count);

                case ResolutionLevel.Weekly:
                    // 这里把 “1 周” 当作 5 个交易日
                    return await CalculateByTradingDaysAsync(endUtc, count * 5);

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