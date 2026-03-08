using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 用户实体，映射到数据库中的 `Users` 表。
    /// Represents the user entity mapped to the `Users` table.
    /// </summary>
    public class UserEntity
    {
        /// <summary>
        /// 用户唯一标识（UUID）。
        /// User unique identifier (UUID).
        /// </summary>
        [Key]
        [Column("Id")]
        public Guid Id { get; set; }

        /// <summary>
        /// 用户名（唯一）。
        /// Username (unique).
        /// </summary>
        [Required]
        [Column("UserName")]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// 密码哈希。
        /// Password hash.
        /// </summary>
        [Required]
        [Column("PasswordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// 邮箱地址（唯一）。
        /// Email address (unique).
        /// </summary>
        [Column("Email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 电话号码。
        /// Phone number.
        /// </summary>
        [Column("PhoneNumber")]
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// 用户状态（1=正常，0=禁用）。
        /// User status (1=Active, 0=Disabled).
        /// </summary>
        [Column("Status")]
        public short Status { get; set; } = 1;

        /// <summary>
        /// 最后登录时间。
        /// Last login timestamp.
        /// </summary>
        [Column("LastLoginTime")]
        public DateTimeOffset? LastLoginTime { get; set; }

        /// <summary>
        /// 创建时间戳。
        /// Created timestamp.
        /// </summary>
        [Column("CreatedTime")]
        public DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// 更新时间戳。
        /// Updated timestamp.
        /// </summary>
        [Column("UpdatedTime")]
        public DateTimeOffset? UpdatedTime { get; set; }

        /// <summary>
        /// 软删除标记。
        /// Soft delete flag.
        /// </summary>
        [Column("IsDeleted")]
        public bool IsDeleted { get; set; } = false;
    }
}
