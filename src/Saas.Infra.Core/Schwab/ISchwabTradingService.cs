using System;
using System.Threading.Tasks;

namespace Saas.Infra.Core.Schwab
{
    /// <summary>
    /// 嘉信理财交易服务接口。
    /// Charles Schwab trading service interface.
    /// </summary>
    public interface ISchwabTradingService
    {
        /// <summary>
        /// 创建订单。
        /// Creates an order.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="accountHashValue">账户哈希值。 / Account hash value.</param>
        /// <param name="orderRequest">订单请求。 / Order request.</param>
        /// <returns>订单 ID。 / Order ID.</returns>
        Task<string> CreateOrderAsync(Guid userId, string accountHashValue, SchwabOrderRequest orderRequest);

        /// <summary>
        /// 取消订单。
        /// Cancels an order.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="accountHashValue">账户哈希值。 / Account hash value.</param>
        /// <param name="orderId">订单 ID。 / Order ID.</param>
        /// <returns>是否成功。 / Whether successful.</returns>
        Task<bool> CancelOrderAsync(Guid userId, string accountHashValue, string orderId);

        /// <summary>
        /// 获取订单详情。
        /// Gets order details.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="accountHashValue">账户哈希值。 / Account hash value.</param>
        /// <param name="orderId">订单 ID。 / Order ID.</param>
        /// <returns>订单详情。 / Order details.</returns>
        Task<SchwabOrder> GetOrderAsync(Guid userId, string accountHashValue, string orderId);
    }

    /// <summary>
    /// 嘉信理财订单请求。
    /// Charles Schwab order request.
    /// </summary>
    public class SchwabOrderRequest
    {
        /// <summary>
        /// 股票代码。
        /// Symbol.
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// 资产类型（EQUITY, OPTION, etc.）。
        /// Asset type (EQUITY, OPTION, etc.).
        /// </summary>
        public string AssetType { get; set; } = "EQUITY";

        /// <summary>
        /// 指令（BUY, SELL, BUY_TO_COVER, SELL_SHORT）。
        /// Instruction (BUY, SELL, BUY_TO_COVER, SELL_SHORT).
        /// </summary>
        public string Instruction { get; set; } = string.Empty;

        /// <summary>
        /// 订单类型（MARKET, LIMIT, STOP, STOP_LIMIT）。
        /// Order type (MARKET, LIMIT, STOP, STOP_LIMIT).
        /// </summary>
        public string OrderType { get; set; } = "MARKET";

        /// <summary>
        /// 数量。
        /// Quantity.
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// 价格（限价单必填）。
        /// Price (required for limit orders).
        /// </summary>
        public decimal? Price { get; set; }

        /// <summary>
        /// 止损价（止损单必填）。
        /// Stop price (required for stop orders).
        /// </summary>
        public decimal? StopPrice { get; set; }

        /// <summary>
        /// 会话类型（NORMAL, AM, PM, SEAMLESS）。
        /// Session type (NORMAL, AM, PM, SEAMLESS).
        /// </summary>
        public string Session { get; set; } = "NORMAL";

        /// <summary>
        /// 有效期（DAY, GTC, FILL_OR_KILL）。
        /// Duration (DAY, GTC, FILL_OR_KILL).
        /// </summary>
        public string Duration { get; set; } = "DAY";

        /// <summary>
        /// 订单策略类型（SINGLE, OCO, TRIGGER）。
        /// Order strategy type (SINGLE, OCO, TRIGGER).
        /// </summary>
        public string OrderStrategyType { get; set; } = "SINGLE";
    }
}
