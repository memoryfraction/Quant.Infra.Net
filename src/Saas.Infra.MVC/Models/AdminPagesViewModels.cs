using System;
using System.Collections.Generic;

namespace Saas.Infra.MVC.Models
{
    /// <summary>
    /// 管理后台首页视图模型。
    /// Admin dashboard home view model.
    /// </summary>
    public class AdminDashboardViewModel
    {
        /// <summary>
        /// 产品总数。
        /// Total product count.
        /// </summary>
        public int TotalProducts { get; set; }

        /// <summary>
        /// 激活产品数。
        /// Active product count.
        /// </summary>
        public int ActiveProducts { get; set; }

        /// <summary>
        /// 订阅总数。
        /// Total subscription count.
        /// </summary>
        public int TotalSubscriptions { get; set; }

        /// <summary>
        /// 激活订阅数。
        /// Active subscription count.
        /// </summary>
        public int ActiveSubscriptions { get; set; }

        /// <summary>
        /// 交易总数。
        /// Total transaction count.
        /// </summary>
        public int TotalTransactions { get; set; }

        /// <summary>
        /// 成功交易数。
        /// Successful transaction count.
        /// </summary>
        public int SuccessfulTransactions { get; set; }
    }

    /// <summary>
    /// 管理后台订阅列表页视图模型。
    /// Admin subscription list page view model.
    /// </summary>
    public class AdminSubscriptionsPageViewModel
    {
        /// <summary>
        /// 查询关键字。
        /// Search keyword.
        /// </summary>
        public string? Keyword { get; set; }

        /// <summary>
        /// 状态筛选。
        /// Status filter.
        /// </summary>
        public short? Status { get; set; }

        /// <summary>
        /// 开始日期筛选。
        /// From date filter.
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// 结束日期筛选。
        /// To date filter.
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// 是否包含已删除订阅。
        /// Whether to include deleted subscriptions.
        /// </summary>
        public bool IncludeDeleted { get; set; }

        /// <summary>
        /// 订阅列表。
        /// Subscription items.
        /// </summary>
        public IReadOnlyList<AdminSubscriptionItemViewModel> Items { get; set; } = Array.Empty<AdminSubscriptionItemViewModel>();
    }

    /// <summary>
    /// 管理后台订阅列表项。
    /// Admin subscription list item.
    /// </summary>
    public class AdminSubscriptionItemViewModel
    {
        /// <summary>
        /// 订阅ID。
        /// Subscription ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 用户名。
        /// User name.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// 用户邮箱。
        /// User email.
        /// </summary>
        public string UserEmail { get; set; } = string.Empty;

        /// <summary>
        /// 产品代码。
        /// Product code.
        /// </summary>
        public string ProductCode { get; set; } = string.Empty;

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
        /// 订阅状态。
        /// Subscription status.
        /// </summary>
        public short Status { get; set; }

        /// <summary>
        /// 是否自动续费。
        /// Whether auto-renew is enabled.
        /// </summary>
        public bool AutoRenew { get; set; }

        /// <summary>
        /// 订阅结束时间。
        /// Subscription end time.
        /// </summary>
        public DateTimeOffset? EndDate { get; set; }

        /// <summary>
        /// 是否已删除。
        /// Whether the subscription is deleted.
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// 订阅创建时间。
        /// Subscription created time.
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }
    }

    /// <summary>
    /// 管理后台交易列表页视图模型。
    /// Admin transactions page view model.
    /// </summary>
    public class AdminTransactionsPageViewModel
    {
        /// <summary>
        /// 网关筛选。
        /// Gateway filter.
        /// </summary>
        public string? Gateway { get; set; }

        /// <summary>
        /// 状态筛选。
        /// Status filter.
        /// </summary>
        public short? Status { get; set; }

        /// <summary>
        /// 开始日期筛选。
        /// From date filter.
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// 结束日期筛选。
        /// To date filter.
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// 交易列表。
        /// Transaction items.
        /// </summary>
        public IReadOnlyList<AdminTransactionItemViewModel> Items { get; set; } = Array.Empty<AdminTransactionItemViewModel>();
    }

    /// <summary>
    /// 管理后台交易列表项。
    /// Admin transaction list item.
    /// </summary>
    public class AdminTransactionItemViewModel
    {
        /// <summary>
        /// 交易ID。
        /// Transaction ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 用户名。
        /// User name.
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// 用户邮箱。
        /// User email.
        /// </summary>
        public string UserEmail { get; set; } = string.Empty;

        /// <summary>
        /// 交易金额（分）。
        /// Transaction amount in cents.
        /// </summary>
        public long Amount { get; set; }

        /// <summary>
        /// 货币。
        /// Currency.
        /// </summary>
        public string Currency { get; set; } = string.Empty;

        /// <summary>
        /// 网关。
        /// Gateway.
        /// </summary>
        public string Gateway { get; set; } = string.Empty;

        /// <summary>
        /// 状态。
        /// Status.
        /// </summary>
        public short Status { get; set; }

        /// <summary>
        /// 外部交易ID。
        /// External transaction ID.
        /// </summary>
        public string? ExternalTransactionId { get; set; }

        /// <summary>
        /// 关联订阅ID。
        /// Associated subscription ID.
        /// </summary>
        public Guid? SubscriptionId { get; set; }

        /// <summary>
        /// 创建时间。
        /// Created time.
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }
    }
}
