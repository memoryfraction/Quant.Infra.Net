using System;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 用户角色关联实体，存储用户与角色的多对多关系。
    /// User-role association entity storing many-to-many relationship between users and roles.
    /// </summary>
    public class UserRoleEntity
    {
        /// <summary>
        /// 主键（UUID）。
        /// Primary key (UUID).
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 用户ID。
        /// User ID.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// 角色ID。
        /// Role ID.
        /// </summary>
        public Guid RoleId { get; set; }

        /// <summary>
        /// 创建时间戳（带时区）。
        /// Created timestamp (with timezone).
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// 关联的用户实体（导航属性）。
        /// Associated user entity (navigation property).
        /// </summary>
        public UserEntity? User { get; set; }

        /// <summary>
        /// 关联的角色实体（导航属性）。
        /// Associated role entity (navigation property).
        /// </summary>
        public RoleEntity? Role { get; set; }
    }
}
