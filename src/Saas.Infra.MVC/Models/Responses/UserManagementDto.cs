using System;

namespace Saas.Infra.MVC.Models.Responses
{
    /// <summary>
    /// 用户管理响应DTO。
    /// User management response DTO.
    /// </summary>
    public class UserManagementDto
    {
        /// <summary>
        /// 用户ID。
        /// User ID.
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
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 用户状态值。
        /// User status value.
        /// </summary>
        public short Status { get; set; }

        /// <summary>
        /// 用户状态文本。
        /// User status text.
        /// </summary>
        public string StatusText { get; set; } = string.Empty;

        /// <summary>
        /// 角色代码。
        /// Role code.
        /// </summary>
        public string RoleCode { get; set; } = string.Empty;

        /// <summary>
        /// 角色显示名称。
        /// Role display name.
        /// </summary>
        public string RoleDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间。
        /// Created time.
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// 当前操作者是否可管理该用户。
        /// Whether the current operator can manage the user.
        /// </summary>
        public bool CanManage { get; set; }

        /// <summary>
        /// 是否已删除。
        /// Whether the user is deleted.
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// 是否为当前登录用户。
        /// Whether the row is the current signed-in user.
        /// </summary>
        public bool IsCurrentUser { get; set; }
    }
}
