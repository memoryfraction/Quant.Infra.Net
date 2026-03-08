namespace Saas.Infra.Core
{
    /// <summary>
    /// ?????????
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
    /// ?????????
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
    }
}
