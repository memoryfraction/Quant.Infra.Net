using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 刷新令牌实体，映射到数据库RefreshTokens表。
    /// Refresh token entity mapped to the database RefreshTokens table.
    /// </summary>
    public class RefreshToken
    {
        /// <summary>
        /// 主键（UUID）。
        /// Primary key (UUID).
        /// </summary>
        [Key]
        [Column("Id")]
        public Guid Id { get; set; }

        /// <summary>
        /// 关联的用户ID。
        /// Associated user ID.
        /// </summary>
        [Column("UserId")]
        public Guid UserId { get; set; }

        /// <summary>
        /// 令牌哈希值（用于验证，不存储明文）。
        /// Token hash (for validation, plain text not stored).
        /// </summary>
        [Column("TokenHash")]
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>
        /// 过期时间戳。
        /// Expiration timestamp.
        /// </summary>
        [Column("ExpiresAt")]
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>
        /// 是否已撤销。
        /// Whether revoked.
        /// </summary>
        [Column("Revoked")]
        public bool Revoked { get; set; }

        /// <summary>
        /// 创建时间戳。
        /// Created timestamp.
        /// </summary>
        [Column("CreatedTime")]
        public DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// 创建者用户ID（可选）。
        /// Creator user ID (optional).
        /// </summary>
        [Column("CreatedBy")]
        public Guid? CreatedBy { get; set; }
    }
}
