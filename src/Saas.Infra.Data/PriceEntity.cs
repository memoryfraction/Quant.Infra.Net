using System;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 价格实体，关联产品的定价信息。
    /// Price entity associated with product pricing information.
    /// </summary>
    public class PriceEntity
    {
        /// <summary>
        /// 主键（UUID）。
        /// Primary key (UUID).
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 关联的产品ID。
        /// Associated product ID.
        /// </summary>
        public Guid ProductId { get; set; }

        /// <summary>
        /// 价格名称。
        /// Price name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 计费周期（month / year）。
        /// Billing period (month / year).
        /// </summary>
        public string BillingPeriod { get; set; } = string.Empty;

        /// <summary>
        /// 金额（以分为单位）。
        /// Amount (in cents).
        /// </summary>
        public long Amount { get; set; }

        /// <summary>
        /// 货币代码。
        /// Currency code.
        /// </summary>
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// 价格是否激活。
        /// Whether price is active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// 创建时间戳（带时区）。
        /// Created timestamp (with timezone).
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// 关联的产品实体（导航属性）。
        /// Associated product entity (navigation property).
        /// </summary>
        public ProductEntity? Product { get; set; }
    }
}
