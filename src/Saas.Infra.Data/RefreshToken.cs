using System;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 刷新令牌实体，存储刷新令牌的哈希与元数据。
    /// Refresh token entity storing hash and metadata.
    /// </summary>
    public class RefreshToken
    {
        public int Id { get; set; }
        public long UserId { get; set; }
        public string TokenHash { get; set; } = string.Empty; // 存储 Token 的 SHA256 哈希
        public DateTime ExpiresAt { get; set; }
        public bool Revoked { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ReplacedByHash { get; set; }
    }
}
