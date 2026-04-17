using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 嘉信理财 OAuth 令牌实体。
    /// Charles Schwab OAuth token entity.
    /// </summary>
    [Table("schwab_tokens")]
    public class SchwabTokenEntity
    {
        /// <summary>
        /// 主键 ID。
        /// Primary key ID.
        /// </summary>
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// 用户 ID（外键）。
        /// User ID (foreign key).
        /// </summary>
        [Required]
        [Column("user_id")]
        public Guid UserId { get; set; }

        /// <summary>
        /// 访问令牌。
        /// Access token.
        /// </summary>
        [Required]
        [Column("access_token")]
        [MaxLength(2000)]
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// 刷新令牌。
        /// Refresh token.
        /// </summary>
        [Required]
        [Column("refresh_token")]
        [MaxLength(2000)]
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// 令牌类型。
        /// Token type.
        /// </summary>
        [Required]
        [Column("token_type")]
        [MaxLength(50)]
        public string TokenType { get; set; } = "Bearer";

        /// <summary>
        /// 访问令牌过期时间（秒）。
        /// Access token expiration in seconds.
        /// </summary>
        [Column("expires_in")]
        public int ExpiresIn { get; set; }

        /// <summary>
        /// 令牌作用域。
        /// Token scope.
        /// </summary>
        [Column("scope")]
        [MaxLength(500)]
        public string Scope { get; set; } = string.Empty;

        /// <summary>
        /// 令牌创建时间。
        /// Token creation time.
        /// </summary>
        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// 令牌更新时间。
        /// Token update time.
        /// </summary>
        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// 是否已删除。
        /// Whether deleted.
        /// </summary>
        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        /// <summary>
        /// 用户导航属性。
        /// User navigation property.
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public virtual UserEntity? User { get; set; }
    }
}
