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

    /// <summary>
    /// 支付状态响应DTO。
    /// Payment status response DTO.
    /// </summary>
    public class PaymentStatusDto
    {
        /// <summary>
        /// 订单ID。
        /// Order ID.
        /// </summary>
        public Guid OrderId { get; set; }

        /// <summary>
        /// 订单状态值。
        /// Order status value.
        /// </summary>
        public short OrderStatus { get; set; }

        /// <summary>
        /// 订单状态文本。
        /// Order status text.
        /// </summary>
        public string OrderStatusText { get; set; } = string.Empty;

        /// <summary>
        /// 是否支付成功。
        /// Whether payment succeeded.
        /// </summary>
        public bool Paid { get; set; }

        /// <summary>
        /// 订阅ID（如已创建）。
        /// Subscription ID if created.
        /// </summary>
        public Guid? SubscriptionId { get; set; }

        /// <summary>
        /// 最新交易ID（如存在）。
        /// Latest transaction ID if present.
        /// </summary>
        public Guid? TransactionId { get; set; }

        /// <summary>
        /// 最新交易状态（如存在）。
        /// Latest transaction status if present.
        /// </summary>
        public short? TransactionStatus { get; set; }

        /// <summary>
        /// 最新外部交易ID（如Stripe Charge ID）。
        /// Latest external transaction ID (for example Stripe Charge ID).
        /// </summary>
        public string? ExternalTransactionId { get; set; }

        /// <summary>
        /// 支付时间。
        /// Paid time.
        /// </summary>
        public DateTimeOffset? PaidTime { get; set; }

        /// <summary>
        /// 订单过期时间。
        /// Order expiration time.
        /// </summary>
        public DateTimeOffset? ExpiredTime { get; set; }

        /// <summary>
        /// 支付成功后颁发的订阅访问令牌。
        /// Subscription access token issued after successful payment.
        /// </summary>
        public string? SubscriptionAccessToken { get; set; }

        /// <summary>
        /// 订阅令牌过期秒数。
        /// Subscription token expiry in seconds.
        /// </summary>
        public int? SubscriptionTokenExpiresIn { get; set; }
    }
}
