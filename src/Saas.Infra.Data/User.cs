using System;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 用户实体，映射到数据库中的 `Users` 表。
    /// Represents the user entity mapped to the `Users` table.
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
