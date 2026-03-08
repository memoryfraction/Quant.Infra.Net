using System;

namespace Saas.Infra.MVC.Models.Responses
{
    /// <summary>
    /// 订阅响应DTO。
    /// Subscription response DTO.
    /// </summary>
    public class SubscriptionDto
    {
        /// <summary>
        /// 订阅ID。
        /// Subscription ID.
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
        /// 价格ID。
        /// Price ID.
        /// </summary>
        public Guid PriceId { get; set; }

        /// <summary>
        /// 价格名称（如"Monthly Plan"）。
        /// Price name (e.g., "Monthly Plan").
        /// </summary>
        public string? PriceName { get; set; }

        /// <summary>
        /// 计费周期。
        /// Billing period.
        /// </summary>
        public string? BillingPeriod { get; set; }

        /// <summary>
        /// 订阅状态（0=待支付，1=激活，2=已取消，3=过期）。
        /// Subscription status (0=Pending, 1=Active, 2=Cancelled, 3=Expired).
        /// </summary>
        public short Status { get; set; }

        /// <summary>
        /// 状态文本。
        /// Status text.
        /// </summary>
        public string StatusText => Status switch
        {
            0 => "Pending",
            1 => "Active",
            2 => "Cancelled",
            3 => "Expired",
            _ => "Unknown"
        };

        /// <summary>
        /// 开始日期。
        /// Start date.
        /// </summary>
        public DateTimeOffset StartDate { get; set; }

        /// <summary>
        /// 结束日期。
        /// End date.
        /// </summary>
        public DateTimeOffset? EndDate { get; set; }

        /// <summary>
        /// 是否自动续费。
        /// Whether auto-renew.
        /// </summary>
        public bool AutoRenew { get; set; }

        /// <summary>
        /// 原价（以分为单位）。
        /// Original amount (in cents).
        /// </summary>
        public long OriginalAmount { get; set; }

        /// <summary>
        /// 实付金额（以分为单位）。
        /// Actual amount (in cents).
        /// </summary>
        public long ActualAmount { get; set; }

        /// <summary>
        /// 货币代码。
        /// Currency code.
        /// </summary>
        public string? Currency { get; set; }

        /// <summary>
        /// 格式化后的实付金额。
        /// Formatted actual amount.
        /// </summary>
        public string FormattedAmount => $"{Currency ?? "USD"} {ActualAmount / 100.0:F2}";

        /// <summary>
        /// 创建时间。
        /// Created time.
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// 是否已删除。
        /// Whether deleted.
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// 剩余天数（距离结束日期）。
        /// Remaining days (until end date).
        /// </summary>
        public int? RemainingDays
        {
            get
            {
                if (EndDate == null || Status != 1)
                    return null;
                var diff = (EndDate.Value - DateTimeOffset.UtcNow).TotalDays;
                return diff > 0 ? (int)Math.Ceiling(diff) : 0;
            }
        }
    }
}
