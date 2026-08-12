using System;

namespace Quant.Infra.Net.Broker.Model
{
    /// <summary>
    /// 开放订单模型，表示交易所中的未完全成交的订单。
    /// Open order model representing an unfilled or partially filled order on the exchange.
    /// </summary>
    public class OpenOrder
    {
        /// <summary>
        /// 交易品种代码（如 BTCUSDT, AAPL）/ Trading symbol (e.g., BTCUSDT, AAPL).
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// 订单数量（合约张数或股数）/ Order quantity in contracts or shares.
        /// </summary>
        public decimal? Quantity { get; set; }

        /// <summary>
        /// 已成交数量 / Filled quantity of the order.
        /// </summary>
        public decimal FilledQuantity { get; set; }

        /// <summary>
        /// 订单ID（交易所唯一标识）/ Order ID unique to the exchange.
        /// </summary>
        public Guid OrderId { get; set; }

        /// <summary>
        /// 订单状态：New, PartiallyFilled, Filled, Cancelled 等 / Order status: New, PartiallyFilled, Filled, Cancelled, etc.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// 创建时间（UTC）/ Creation timestamp in UTC.
        /// </summary>
        public DateTime? CreatedAtUtc { get; set; }
    }
}
