namespace Saas.Infra.Core
{
    /// <summary>
    /// UserRole
    /// System user role enumeration.
    /// </summary>
    public enum UserRole
    {
        Super_Admin = 1,
        Admin = 2,
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
        LocalWindows,       // 本地 Windows 物理环境 (F5 直接运行)
        LocalContainer,     // 本地 Docker 容器环境
        AzureContainerApps, // Azure ACA 云端环境
        OtherLinux          // 其他 Linux 环境 (如自建服务器)
    }

}
