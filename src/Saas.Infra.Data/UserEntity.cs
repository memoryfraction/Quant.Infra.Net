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
        [Key]
        [Column("Id")]
        public long Id { get; set; }

        [Required]
        [Column("UserId")]
        public Guid UserId { get; set; }

        [Required]
        [Column("UserName")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [Column("PasswordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [Column("Email")]
        public string Email { get; set; } = string.Empty;

        [Column("PhoneNumber")]
        public string? PhoneNumber { get; set; }

        [Column("Status")]
        public short Status { get; set; } = 1;

        [Column("CreatedTime")]
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;

        [Column("UpdatedTime")]
        public DateTime? UpdatedTime { get; set; }

        [Column("CreatedBy")]
        public long? CreatedBy { get; set; }

        [Column("UpdatedBy")]
        public long? UpdatedBy { get; set; }

        [Column("IsDeleted")]
        public bool IsDeleted { get; set; } = false;
    }
}
