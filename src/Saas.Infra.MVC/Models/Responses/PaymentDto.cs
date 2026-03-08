using System;

namespace Saas.Infra.MVC.Models.Responses
{
    /// <summary>
    /// 支付意图响应DTO。
    /// Payment intent response DTO.
    /// </summary>
    public class PaymentIntentDto
    {
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
