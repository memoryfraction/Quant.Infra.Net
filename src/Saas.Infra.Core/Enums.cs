namespace Saas.Infra.Core
{
    /// <summary>
    /// 系统用户角色枚举。
    /// System user role enumeration.
    /// </summary>
    public enum UserRole
    {
        Super_Admin = 1,
        Admin = 2,
        User = 3    
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
        /// Normal user role code.
        /// </summary>
        public const string User = "USER";
    }

}
