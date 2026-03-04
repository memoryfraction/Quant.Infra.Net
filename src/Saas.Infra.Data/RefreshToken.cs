using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 刷新令牌实体（数据库映射）。
    /// Refresh token entity mapped to the database RefreshTokens table.
    /// </summary>
    public class RefreshToken
    {
        [Key]
        [Column("Id")]
        public Guid Id { get; set; }

        [Column("UserId")]
        public Guid UserId { get; set; }

        [Column("TokenHash")]
        public string TokenHash { get; set; } = string.Empty; // stores SHA256 hash of the refresh token

        [Column("ExpiresAt")]
        public DateTime ExpiresAt { get; set; }

        [Column("Revoked")]
        public bool Revoked { get; set; }

        [Column("CreatedTime")]
        public DateTime CreatedTime { get; set; }

        [Column("ReplacedByHash")]
        public string? ReplacedByHash { get; set; }
    }
}
