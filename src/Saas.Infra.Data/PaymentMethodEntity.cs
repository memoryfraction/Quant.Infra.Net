using System;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 支付方式实体，存储用户的支付方式（Stripe卡、OxaPay等）。
    /// Payment method entity storing user's payment methods (Stripe cards, OxaPay, etc.).
    /// </summary>
    public class PaymentMethodEntity
    {
        /// <summary>
        /// 主键（UUID）。
        /// Primary key (UUID).
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 关联的用户ID。
        /// Associated user ID.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// 支付方式类型（CREDIT_CARD / CRYPTO）。
        /// Payment method type (CREDIT_CARD / CRYPTO).
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 支付网关（STRIPE / OXAPAY）。
        /// Payment gateway (STRIPE / OXAPAY).
        /// </summary>
        public string Gateway { get; set; } = string.Empty;

        /// <summary>
        /// 第三方平台的PaymentMethod ID（Stripe PaymentMethod ID等）。
        /// External platform PaymentMethod ID (Stripe PaymentMethod ID, etc.).
        /// </summary>
        public string? ExternalId { get; set; }

        /// <summary>
        /// 是否为默认支付方式。
        /// Whether this is the default payment method.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// 创建时间戳（带时区）。
        /// Created timestamp (with timezone).
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// 关联的用户实体（导航属性）。
        /// Associated user entity (navigation property).
        /// </summary>
        public UserEntity? User { get; set; }
    }
}
