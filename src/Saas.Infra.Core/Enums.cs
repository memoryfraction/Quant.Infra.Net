namespace Saas.Infra.Core
{
    /// <summary>
    /// 系统用户角色枚举。
    /// System user role enumeration.
    /// </summary>
    public enum UserRole
    {
        /// <summary>
        /// 超级管理员。
        /// Super administrator.
        /// </summary>
        Super_Admin = 1,

        /// <summary>
        /// 管理员。
        /// Administrator.
        /// </summary>
        Admin = 2,

        /// <summary>
        /// 普通用户。
        /// Standard user.
        /// </summary>
        User = 3
    }

    /// <summary>
    /// 用户状态枚举。
    /// User status enumeration.
    /// </summary>
    public enum UserStatus : short
    {
        /// <summary>
        /// 已禁用。
        /// Disabled.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// 已启用。
        /// Enabled.
        /// </summary>
        Enabled = 1
    }

    /// <summary>
    /// 订单状态枚举。
    /// Order status enumeration.
    /// </summary>
    public enum OrderStatus : short
    {
        /// <summary>
        /// 待支付。
        /// Pending payment.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// 已支付。
        /// Paid successfully.
        /// </summary>
        Paid = 1,

        /// <summary>
        /// 已取消。
        /// Cancelled.
        /// </summary>
        Cancelled = 2,

        /// <summary>
        /// 已退款。
        /// Refunded.
        /// </summary>
        Refunded = 3
    }

    /// <summary>
    /// 订阅状态枚举。
    /// Subscription status enumeration.
    /// </summary>
    public enum SubscriptionStatus : short
    {
        /// <summary>
        /// 待处理。
        /// Pending.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// 已激活。
        /// Active.
        /// </summary>
        Active = 1,

        /// <summary>
        /// 已取消。
        /// Cancelled.
        /// </summary>
        Cancelled = 2,

        /// <summary>
        /// 已过期。
        /// Expired.
        /// </summary>
        Expired = 3
    }

    /// <summary>
    /// 交易状态枚举。
    /// Transaction status enumeration.
    /// </summary>
    public enum TransactionStatus : short
    {
        /// <summary>
        /// 待处理。
        /// Pending.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// 成功。
        /// Success.
        /// </summary>
        Success = 1,

        /// <summary>
        /// 失败。
        /// Failed.
        /// </summary>
        Failed = 2,

        /// <summary>
        /// 已退款。
        /// Refunded.
        /// </summary>
        Refunded = 3
    }

    /// <summary>
    /// 运行环境枚举。
    /// Runtime environment enumeration.
    /// </summary>
    public enum RuntimeEnvironment
    {
        /// <summary>
        /// 本地 Windows 物理环境。
        /// Local Windows host environment.
        /// </summary>
        LocalWindows,

        /// <summary>
        /// 本地容器环境。
        /// Local container environment.
        /// </summary>
        LocalContainer,

        /// <summary>
        /// Azure Container Apps 环境。
        /// Azure Container Apps environment.
        /// </summary>
        AzureContainerApps,

        /// <summary>
        /// 其他 Linux 环境。
        /// Other Linux environment.
        /// </summary>
        OtherLinux
    }

    /// <summary>
    /// 系统角色代码常量。
    /// System role code constants.
    /// </summary>
    public static class RoleCodes
    {
        /// <summary>
        /// 超级管理员角色代码。
        /// Super administrator role code.
        /// </summary>
        public const string SuperAdmin = "SUPER_ADMIN";

        /// <summary>
        /// 管理员角色代码。
        /// Administrator role code.
        /// </summary>
        public const string Admin = "ADMIN";

        /// <summary>
        /// 普通用户角色代码。
        /// Standard user role code.
        /// </summary>
        public const string User = "USER";

        /// <summary>
        /// 仅超级管理员表达式。
        /// Super administrator only expression.
        /// </summary>
        public const string SuperAdminOnly = SuperAdmin;

        /// <summary>
        /// 管理员或超级管理员表达式。
        /// Administrator or super administrator expression.
        /// </summary>
        public const string AdminOrSuperAdmin = Admin + "," + SuperAdmin;
    }
}
