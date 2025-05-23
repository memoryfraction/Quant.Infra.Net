using Alpaca.Markets;
using InterReact;
using NodaTime;
using Quant.Infra.Net.Broker.Model;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;
using System;
using System.Collections.Generic;
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
        public decimal GetLatestPrice(string symbol)
        {
            var request = new LatestMarketDataRequest(symbol);
            var quote = _dataClient.GetLatestTradeAsync(request).Result;
            return quote.Price;
        }

        /// <summary>
        /// 提交一个市价/限价单，可设定买卖方向、盘后交易等。
        /// Submit a market or limit order with side and extended-hours support.
        /// </summary>
        /// <param name="symbol">标的代码 / Symbol</param>
        /// <param name="quantity">数量，正为买入，负为卖出 / Quantity (positive = buy, negative = sell)</param>
        /// <param name="orderType">订单类型（市价、限价等）/ Order type</param>
        /// <param name="timeInForce">订单有效时间（Day, GTC）/ Time in force</param>
        /// <param name="afterHours">是否盘后交易 / Allow extended hours</param>
        public async Task PlaceOrderAsync(string symbol, decimal quantity, OrderType orderType = OrderType.Market, Alpaca.Markets.TimeInForce timeInForce = Alpaca.Markets.TimeInForce.Day, bool afterHours = false)
        {
            var side = quantity > 0 ? Alpaca.Markets.OrderSide.Buy : Alpaca.Markets.OrderSide.Sell;
            var qty = Math.Abs(quantity);
            var request = new NewOrderRequest(symbol, OrderQuantity.Fractional(qty), side, orderType, timeInForce)
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
        public bool IsMarketOpenNow()
        {
            try { return _tradeClient.GetClockAsync().Result.IsOpen; }
            catch (Exception ex) { UtilityService.LogAndWriteLine(ex.ToString()); return false; }
        }

        /// <summary>
        /// 撤销指定订单。
        /// Cancel an open order by order ID.
        /// </summary>
        public void CancelOrder(Guid orderId)
        {
            try { _tradeClient.CancelOrderAsync(orderId).Wait(); }
            catch (Exception ex) { UtilityService.LogAndWriteLine("CancelOrder error: " + ex.Message); }
        }

        /// <summary>
        /// 获取指定股票的资产信息（是否可交易、可做空等）。
        /// Get asset metadata such as tradability and shortability.
        /// </summary>
        public async Task<IAsset> GetAssetAsync(string symbol)
        {
            return await _tradeClient.GetAssetAsync(symbol);
        }
    }
}
