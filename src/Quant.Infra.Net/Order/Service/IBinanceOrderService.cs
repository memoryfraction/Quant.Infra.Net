using Binance.Net.Enums;
using Binance.Net.Objects.Models;
using Binance.Net.Objects.Models.Spot;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Order.Service
{
    /// <summary>
    /// 传入参数，负责与订单相关的操作。 比如: 增加订单，获取所有订单，取消订单等
    /// Pass in parameters and be responsible for order-related operations. For example: add orders, get all orders, cancel orders, etc.
    /// </summary>
    public interface IBinanceOrderService
    {
        Task<BinancePlacedOrder> CreateSpotOrderAsync(string symbol, decimal quantity, OrderSide side, SpotOrderType type, decimal? price = null, int retryCount = 3);
        Task<BinanceOrder> GetSpotOrderAsync(string symbol, long orderId, int retryAttempts = 3);
        Task<IEnumerable<BinanceOrder>> GetAllSpotOpenOrdersAsync(string symbol = null, int retryAttempts = 3);
        Task<BinanceOrderBase> CancelSpotOrderAsync(string symbol, long orderId, int retryAttempts = 3);
        Task<BinanceReplaceOrderResult> ReplaceSpotOrderAsync(string symbol, OrderSide side, SpotOrderType type, CancelReplaceMode cancelReplaceMode, long? cancelOrderId = null, string? cancelClientOrderId = null, string? newCancelClientOrderId = null, string? newClientOrderId = null, decimal? quantity = null, decimal? quoteQuantity = null, decimal? price = null, TimeInForce? timeInForce = null, decimal? stopPrice = null, decimal? icebergQty = null, OrderResponseType? orderResponseType = null, int? trailingDelta = null, int? strategyId = null, int? strategyType = null, CancelRestriction? cancelRestriction = null, int? receiveWindow = null, CancellationToken ct = default(CancellationToken), int retryAttempts = 3);
        
        /// <summary>
        /// 允许手工设定apiKey和apiSecret
        /// Allow to set apiKey and apiSecret manually
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="apiSecret"></param>
        public void SetBinanceCredential(string apiKey, string apiSecret);
    }
}

