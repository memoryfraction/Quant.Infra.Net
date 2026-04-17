using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 嘉信理财账户实体。
    /// Charles Schwab account entity.
    /// </summary>
    [Table("schwab_accounts")]
    public class SchwabAccountEntity
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
        /// 账户号码。
        /// Account number.
        /// </summary>
        [Required]
        [Column("account_number")]
        [MaxLength(100)]
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// 账户哈希值（用于 API 请求）。
        /// Account hash value (used for API requests).
        /// </summary>
        [Required]
        [Column("hash_value")]
        [MaxLength(200)]
        public string HashValue { get; set; } = string.Empty;

        /// <summary>
        /// 账户类型。
        /// Account type.
        /// </summary>
        [Column("account_type")]
        [MaxLength(50)]
        public string AccountType { get; set; } = string.Empty;

        /// <summary>
        /// 账户昵称。
        /// Account nickname.
        /// </summary>
        [Column("nickname")]
        [MaxLength(200)]
        public string? Nickname { get; set; }

        /// <summary>
        /// 是否为主账户。
        /// Whether primary account.
        /// </summary>
        [Column("is_primary")]
        public bool IsPrimary { get; set; }

        /// <summary>
        /// 创建时间。
        /// Created time.
        /// </summary>
        [Required]
        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// 更新时间。
        /// Updated time.
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
