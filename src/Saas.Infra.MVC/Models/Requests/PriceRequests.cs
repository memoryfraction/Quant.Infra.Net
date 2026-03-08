using System;
using System.ComponentModel.DataAnnotations;

namespace Saas.Infra.MVC.Models.Requests
{
    /// <summary>
    /// 创建价格请求模型。
    /// Create price request model.
    /// </summary>
    public class CreatePriceRequest
    {
        /// <summary>
        /// 关联的产品ID。
        /// Associated product ID.
        /// </summary>
        [Required(ErrorMessage = "Product ID is required")]
        public Guid ProductId { get; set; }

        /// <summary>
        /// 价格名称（例如："Monthly Plan"）。
        /// Price name (e.g., "Monthly Plan").
        /// </summary>
        [Required(ErrorMessage = "Price name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 计费周期（week / month / year）。
        /// Billing period (week / month / year).
        /// </summary>
        [Required(ErrorMessage = "Billing period is required")]
        [StringLength(20, ErrorMessage = "Billing period cannot exceed 20 characters")]
        public string BillingPeriod { get; set; } = string.Empty;

        /// <summary>
        /// 金额（以分为单位）。
        /// Amount (in cents).
        /// </summary>
        [Required(ErrorMessage = "Amount is required")]
        [Range(0, long.MaxValue, ErrorMessage = "Amount must be positive")]
        public long Amount { get; set; }

        /// <summary>
        /// 货币代码（默认USD）。
        /// Currency code (default USD).
        /// </summary>
        [StringLength(10, ErrorMessage = "Currency cannot exceed 10 characters")]
        public string Currency { get; set; } = "USD";

        /// <summary>
        /// 是否激活。
        /// Whether active.
        /// </summary>
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// 更新价格请求模型。
    /// Update price request model.
    /// </summary>
    public class UpdatePriceRequest
    {
        /// <summary>
        /// 价格名称。
        /// Price name.
        /// </summary>
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string? Name { get; set; }

        /// <summary>
        /// 金额（以分为单位）。
        /// Amount (in cents).
        /// </summary>
        [Range(0, long.MaxValue, ErrorMessage = "Amount must be positive")]
        public long? Amount { get; set; }

        /// <summary>
        /// 是否激活。
        /// Whether active.
        /// </summary>
        public bool? IsActive { get; set; }
    }
}
