using System;

namespace Saas.Infra.Core
{
    /// <summary>
    /// 刷新令牌记录 DTO，用于仓储与服务之间传递数据。
    /// Refresh token record DTO used between repository and services.
    /// </summary>
    public class RefreshTokenRecord
    {
        public int Id { get; set; }
        public long UserId { get; set; }
        public string TokenHash { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool Revoked { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ReplacedByHash { get; set; }
    }
}
