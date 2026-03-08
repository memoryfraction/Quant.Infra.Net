using System;

namespace Saas.Infra.MVC.Models.Responses
{
    /// <summary>
    /// 价格响应DTO。
    /// Price response DTO.
    /// </summary>
    public class PriceDto
    {
        /// <summary>
        /// 价格ID。
        /// Price ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 关联的产品ID。
        /// Associated product ID.
        /// </summary>
        public Guid ProductId { get; set; }

        /// <summary>
        /// 产品代码。
        /// Product code.
        /// </summary>
        public string? ProductCode { get; set; }

        /// <summary>
        /// 产品名称。
        /// Product name.
        /// </summary>
        public string? ProductName { get; set; }

        /// <summary>
        /// 价格名称。
        /// Price name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 计费周期。
        /// Billing period.
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
        /// 格式化后的金额（带货币符号）。
        /// Formatted amount (with currency symbol).
        /// </summary>
        public string FormattedAmount => $"{Currency} {Amount / 100.0:F2}";

        /// <summary>
        /// 是否激活。
        /// Whether active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// 创建时间。
        /// Created time.
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }
    }
}
