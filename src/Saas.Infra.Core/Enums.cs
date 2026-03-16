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
        /// Normal user.
        /// </summary>
        User = 3
    }

    /// <summary>
    /// 用户状态
    /// User status enumeration.
    /// </summary>
    public enum UserStatus : short
    {
        /// <summary>
        /// Disabled
        /// Disabled status.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Enabled
        /// Enabled status.
        /// </summary>
        Enabled = 1
    }

    /// <summary>
    /// RoleCodes
    /// System role code constants.
    /// </summary>
    public static class RoleCodes
    {
        /// <summary>
        /// SuperAdmin
        /// Super administrator role code.
        /// </summary>
        public const string SuperAdmin = "SUPER_ADMIN";

        /// <summary>
        /// Admin
        /// Administrator role code.
        /// </summary>
        public const string Admin = "ADMIN";

        /// <summary>
        /// User
        /// Normal user role code.
        /// </summary>
        public const string User = "USER";

        /// <summary>
        /// SuperAdminOnly
        /// Super administrator only role expression.
        /// </summary>
        public const string SuperAdminOnly = SuperAdmin;

        /// <summary>
        /// AdminOrSuperAdmin
        /// Admin or super administrator role expression.
        /// </summary>
        public const string AdminOrSuperAdmin = Admin + "," + SuperAdmin;
    }


    public enum RuntimeEnvironment
    {
        /// <summary>
        /// 本地 Windows 物理环境 (F5 直接运行)。
        /// Local Windows physical environment (F5 debug run).
        /// </summary>
        LocalWindows,

        /// <summary>
        /// 本地 Docker 容器环境。
        /// Local Docker container environment.
        /// </summary>
        LocalContainer,

        /// <summary>
        /// Azure Container Apps 云端环境。
        /// Azure Container Apps cloud environment.
        /// </summary>
        AzureContainerApps,

        /// <summary>
        /// 其他 Linux 环境 (如自建服务器)。
        /// Other Linux environment (e.g. self-hosted server).
        /// </summary>
        OtherLinux
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
        /// Paid.
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

}
