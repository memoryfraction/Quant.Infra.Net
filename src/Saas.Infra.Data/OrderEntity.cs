using System;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 订单实体，作为支付流程的统一入口。
    /// Order entity acting as the unified entry point for the payment flow.
    /// </summary>
    public class OrderEntity
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
        /// 关联订阅ID（支付成功后可回填）。
        /// Related subscription ID (backfilled after successful payment).
        /// </summary>
        public Guid? SubscriptionId { get; set; }

        /// <summary>
        /// 原始金额（分）。
        /// Original amount (in cents).
        /// </summary>
        public long OriginalAmount { get; set; }

        /// <summary>
        /// 实际支付金额（分）。
        /// Actual amount (in cents).
        /// </summary>
        public long ActualAmount { get; set; }

        /// <summary>
        /// 优惠金额（分）。
        /// Discount amount (in cents).
        /// </summary>
        public long DiscountAmount { get; set; }

        /// <summary>
        /// 订单状态（0=Pending, 1=Paid, 2=Cancelled, 3=Refunded）。
        /// Order status (0=Pending, 1=Paid, 2=Cancelled, 3=Refunded).
        /// </summary>
        public short Status { get; set; }

        /// <summary>
        /// 订单过期时间。
        /// Order expiration time.
        /// </summary>
        public DateTimeOffset? ExpiredTime { get; set; }

        /// <summary>
        /// 实际支付时间。
        /// Actual paid time.
        /// </summary>
        public DateTimeOffset? PaidTime { get; set; }

        /// <summary>
        /// 扩展元数据。
        /// Extended metadata.
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// 创建时间。
        /// Created time.
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// 软删除标记。
        /// Soft delete flag.
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// 导航：用户。
        /// Navigation: user.
        /// </summary>
        public UserEntity? User { get; set; }

        /// <summary>
        /// 导航：产品。
        /// Navigation: product.
        /// </summary>
        public ProductEntity? Product { get; set; }

        /// <summary>
        /// 导航：价格。
        /// Navigation: price.
        /// </summary>
        public PriceEntity? Price { get; set; }

        /// <summary>
        /// 导航：订阅。
        /// Navigation: subscription.
        /// </summary>
        public SubscriptionEntity? Subscription { get; set; }
    }
}
