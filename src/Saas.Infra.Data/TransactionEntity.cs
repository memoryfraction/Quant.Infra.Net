using System;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 交易实体，存储支付交易记录。
    /// Transaction entity storing payment transaction records.
    /// </summary>
    public class TransactionEntity
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
        /// 关联的订阅ID（可选）。
        /// Associated subscription ID (optional).
        /// </summary>
        public Guid? SubscriptionId { get; set; }

        /// <summary>
        /// 交易金额（以分为单位）。
        /// Transaction amount (in cents).
        /// </summary>
        public long Amount { get; set; }

        /// <summary>
        /// 货币代码（USD / CNY等）。
        /// Currency code (USD / CNY, etc.).
        /// </summary>
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// 支付网关（STRIPE / OXAPAY / USDT）。
        /// Payment gateway (STRIPE / OXAPAY / USDT).
        /// </summary>
        public string Gateway { get; set; } = string.Empty;

        /// <summary>
        /// 第三方交易ID（Stripe Charge ID / OxaPay Transaction ID）。
        /// External transaction ID (Stripe Charge ID / OxaPay Transaction ID).
        /// </summary>
        public string? ExternalTransactionId { get; set; }

        /// <summary>
        /// 交易状态（0=Pending, 1=Success, 2=Failed, 3=Refunded）。
        /// Transaction status (0=Pending, 1=Success, 2=Failed, 3=Refunded).
        /// </summary>
        public short Status { get; set; }

        /// <summary>
        /// JSON元数据（存储支付相关的额外信息）。
        /// JSON metadata (storing additional payment-related information).
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// 创建时间戳（带时区）。
        /// Created timestamp (with timezone).
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// 更新时间戳（带时区）。
        /// Updated timestamp (with timezone).
        /// </summary>
        public DateTimeOffset? UpdatedTime { get; set; }

        /// <summary>
        /// 备注信息。
        /// Remarks.
        /// </summary>
        public string? Remarks { get; set; }

        /// <summary>
        /// 关联的用户实体（导航属性）。
        /// Associated user entity (navigation property).
        /// </summary>
        public UserEntity? User { get; set; }

        /// <summary>
        /// 关联的订阅实体（导航属性）。
        /// Associated subscription entity (navigation property).
        /// </summary>
        public SubscriptionEntity? Subscription { get; set; }
    }
}
