using Binance.Net.Enums;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Futures;
using Binance.Net.Objects.Models.Spot;
using Quant.Infra.Net.Shared.Model;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quant.Infra.Net
{
    /// <summary>
    /// 传入参数，负责与订单相关的操作。 比如: 增加订单，获取所有订单，取消订单等
    /// Pass in parameters and be responsible for order-related operations. For example: add orders, get all orders, cancel orders, etc.
    /// </summary>
    public interface IBinanceOrderService
    {
        Task<IEnumerable<string>> GetAllSymbolsAsync();

        Task<BinancePlacedOrder> PlaceSpotOrderAsync(string symbol, OrderSide orderSide, OrderActionType spotOrderType, decimal? quantity, decimal? quoteQuantity, decimal? price = null, int retryCount = 3);

        Task<BinanceOrder> GetSpotOrderAsync(string symbol, long orderId, int retryAttempts = 3);

        Task<IEnumerable<BinanceOrder>> GetAllSpotOpenOrdersAsync(string symbol = null, int retryAttempts = 3);

        Task<BinanceOrderBase> CancelSpotOrderAsync(string symbol, long orderId, int retryAttempts = 3);

        Task<BinanceReplaceOrderResult> ReplaceSpotOrderAsync(string symbol, OrderSide side, OrderActionType type, CancelReplaceMode cancelReplaceMode, long? cancelOrderId = null, string? cancelClientOrderId = null, string? newCancelClientOrderId = null, string? newClientOrderId = null, decimal? quantity = null, decimal? quoteQuantity = null, decimal? price = null, TimeInForce? timeInForce = null, decimal? stopPrice = null, decimal? icebergQty = null, OrderResponseType? orderResponseType = null, int? trailingDelta = null, int? strategyId = null, int? strategyType = null, CancelRestriction? cancelRestriction = null, int? receiveWindow = null, CancellationToken ct = default(CancellationToken), int retryAttempts = 3);

        Task<IEnumerable<BinanceOrderBase>> CancelAllOrdersAsync(string symbol, int retryAttempts = 3);

        Task<BinancePlacedOrder> PlaceMarginOrderAsync(string symbol, OrderSide orderSide, OrderActionType spotOrderType, decimal? quantity, decimal? quoteQuantity, decimal? price = null, int retryCount = 3);

        Task<decimal> GetSubAccountTotalAssetOfBtcAsync();

        Task LiquidateAsync(string symbol);

        /// <summary>
        /// 清仓所有持仓
        /// </summary>
        /// <returns></returns>
        Task LiquidateAsync();

        /// <summary>
        /// 允许手工设定apiKey和apiSecret， 需要账户操作的，先要调用这个方法
        /// Allow to set apiKey and apiSecret manually
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="apiSecret"></param>
        public void SetBinanceCredential(string apiKey, string apiSecret);

        #region Binance Future

        Task<BinanceFuturesAccountInfoV3> GetBinanceFuturesAccountInfoAsync();

        Task<IEnumerable<BinancePositionDetailsUsdt>> GetHoldingPositionAsync();

        Task<IEnumerable<BinancePositionDetailsUsdt>> GetHoldingPositionAsync(string symbol);

        /// <summary>
        /// 创建UsdFuture订单
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="orderSide">此信号开关仓时相反</param>
        /// <param name="quantity">总是正数</param>
        /// <param name="positionSide">做多为Long，做空为Short，开关仓需一致</param>
        /// <param name="orderType">默认是Market类型</param>
        /// <returns></returns>
        Task<BinanceUsdFuturesOrder> PlaceUsdFutureOrderAsync(string symbol, OrderSide orderSide, decimal quantity, PositionSide positionSide, FuturesOrderType orderType = FuturesOrderType.Market);

        #endregion Binance Future
    }
}