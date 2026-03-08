using System;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 角色实体，定义系统中的角色类型。
    /// Role entity defining role types in the system.
    /// </summary>
    public class RoleEntity
    {
        /// <summary>
        /// 主键（UUID）。
        /// Primary key (UUID).
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 角色名称（唯一）。
        /// Role name (unique).
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 角色代码（唯一，如SUPER_ADMIN/ADMIN/USER）。
        /// Role code (unique, e.g., SUPER_ADMIN/ADMIN/USER).
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 角色描述。
        /// Role description.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 创建时间戳（带时区）。
        /// Created timestamp (with timezone).
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }
    }
}
