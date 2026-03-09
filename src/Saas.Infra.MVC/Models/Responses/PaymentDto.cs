using System;

namespace Saas.Infra.MVC.Models.Responses
{
    /// <summary>
    /// 创建订单响应DTO。
    /// Create order response DTO.
    /// </summary>
    public class CreateOrderDto
    {
        /// <summary>
        /// 订单ID。
        /// Order ID.
        /// </summary>
        public Guid OrderId { get; set; }

        /// <summary>
        /// 订单状态。
        /// Order status.
        /// </summary>
        public short Status { get; set; }

        /// <summary>
        /// 产品ID。
        /// Product ID.
        /// </summary>
        public Guid ProductId { get; set; }

        /// <summary>
        /// 价格ID。
        /// Price ID.
        /// </summary>
        public Guid PriceId { get; set; }

        /// <summary>
        /// 订单类型。
        /// Order type.
        /// </summary>
        public string OrderType { get; set; } = string.Empty;

        /// <summary>
        /// 原始金额。
        /// Original amount.
        /// </summary>
        public long OriginalAmount { get; set; }

        /// <summary>
        /// 实际金额。
        /// Actual amount.
        /// </summary>
        public long ActualAmount { get; set; }

        /// <summary>
        /// 货币代码。
        /// Currency code.
        /// </summary>
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// 产品名称。
        /// Product name.
        /// </summary>
        public string ProductName { get; set; } = string.Empty;

        /// <summary>
        /// 价格名称。
        /// Price name.
        /// </summary>
        public string PriceName { get; set; } = string.Empty;

        /// <summary>
        /// 计费周期。
        /// Billing period.
        /// </summary>
        public string BillingPeriod { get; set; } = string.Empty;

        /// <summary>
        /// 支付页链接。
        /// Payment page URL.
        /// </summary>
        public string PaymentUrl { get; set; } = string.Empty;

        /// <summary>
        /// 过期时间。
        /// Expiration time.
        /// </summary>
        public DateTimeOffset? ExpiredTime { get; set; }
    }

    /// <summary>
    /// 支付意图响应DTO。
    /// Payment intent response DTO.
    /// </summary>
    public class PaymentIntentDto
    {
        /// <summary>
        /// 订单ID。
        /// Order ID.
        /// </summary>
        public Guid OrderId { get; set; }

        /// <summary>
        /// 客户端密钥（用于Stripe Elements）。
        /// Client secret (for Stripe Elements).
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// 支付意图ID。
        /// Payment intent ID.
        /// </summary>
        public string PaymentIntentId { get; set; } = string.Empty;

        /// <summary>
        /// 金额（以分为单位）。
        /// Amount (in cents).
        /// </summary>
        public long Amount { get; set; }

        /// <summary>
        /// 货币代码。
        /// Currency code.
        /// </summary>
        public string Currency { get; set; } = string.Empty;

        /// <summary>
        /// 可发布密钥（前端用于初始化Stripe）。
        /// Publishable key (frontend uses to initialize Stripe).
        /// </summary>
        public string? PublishableKey { get; set; }
    }

    /// <summary>
    /// 支付确认响应DTO。
    /// Payment confirmation response DTO.
    /// </summary>
    public class PaymentConfirmationDto
    {
        /// <summary>
        /// 是否成功。
        /// Whether succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 订单ID。
        /// Order ID.
        /// </summary>
        public Guid? OrderId { get; set; }

        /// <summary>
        /// 订阅ID。
        /// Subscription ID.
        /// </summary>
        public Guid? SubscriptionId { get; set; }

        /// <summary>
        /// 消息。
        /// Message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}
