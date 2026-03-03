using System;

namespace Saas.Infra.Core
{
    /// <summary>
    /// 用户 DTO，用于服务层与数据层之间传输用户信息。
    /// User DTO used to transfer user information between layers.
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
