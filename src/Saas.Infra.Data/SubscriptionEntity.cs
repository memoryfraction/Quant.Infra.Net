using System;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 订阅实体，存储用户订阅产品的记录。
    /// Subscription entity storing user product subscription records.
    /// </summary>
    public class SubscriptionEntity
    {
        /// <summary>
        /// 主键（UUID）。
        /// Primary key (UUID).
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 用户ID。
        /// User ID.
        /// </summary>
        public Guid UserId { get; set; }

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
        /// 关联订单ID。
        /// Related order ID.
        /// </summary>
        public Guid? OrderId { get; set; }

        /// <summary>
        /// 订阅状态（0=Pending, 1=Active, 2=Expired）。
        /// Subscription status (0=Pending, 1=Active, 2=Expired).
        /// </summary>
        public short Status { get; set; } = 0;

        /// <summary>
        /// 订阅开始日期。
        /// Subscription start date.
        /// </summary>
        public DateTimeOffset StartDate { get; set; }

        /// <summary>
        /// 订阅结束日期。
        /// Subscription end date.
        /// </summary>
        public DateTimeOffset? EndDate { get; set; }

        /// <summary>
        /// 是否自动续订。
        /// Whether to auto-renew.
        /// </summary>
        public bool AutoRenew { get; set; } = true;

        /// <summary>
        /// 原始金额（冗余快照，订阅时的原始标价，以分为单位）。
        /// Original amount (redundant snapshot, original price at subscription time, in cents).
        /// </summary>
        public long OriginalAmount { get; set; }

        /// <summary>
        /// 实际支付金额（冗余快照，实际支付金额，以分为单位）。
        /// Actual amount (redundant snapshot, actual payment amount, in cents).
        /// </summary>
        public long ActualAmount { get; set; }

        /// <summary>
        /// JSON元数据（预留，未来存储关联的优惠券ID或折扣说明）。
        /// JSON metadata (reserved for future coupon ID or discount description).
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// 创建时间戳（带时区）。
        /// Created timestamp (with timezone).
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// 是否已删除（软删除标记）。
        /// Whether deleted (soft delete flag).
        /// </summary>
        public bool IsDeleted { get; set; } = false;

        /// <summary>
        /// 关联的订单实体（导航属性）。
        /// Associated order entity (navigation property).
        /// </summary>
        public OrderEntity? Order { get; set; }

        /// <summary>
        /// 关联的用户实体（导航属性）。
        /// Associated user entity (navigation property).
        /// </summary>
        public UserEntity? User { get; set; }

        /// <summary>
        /// 关联的产品实体（导航属性）。
        /// Associated product entity (navigation property).
        /// </summary>
        public ProductEntity? Product { get; set; }

        /// <summary>
        /// 关联的价格实体（导航属性）。
        /// Associated price entity (navigation property).
        /// </summary>
        public PriceEntity? Price { get; set; }
    }
}
