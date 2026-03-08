using System;

namespace Saas.Infra.Data
{
    /// <summary>
    /// 产品实体，存储在数据库中遵循产品定价架构。
    /// Product entity stored in database following product pricing schema.
    /// </summary>
    public class ProductEntity
    {
        /// <summary>
        /// 主键（UUID）。
        /// Primary key (UUID).
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 业务代码（唯一），用作公共产品标识符。
        /// Business code (unique), used as public product identifier.
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 显示名称。
        /// Display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 产品描述（文本）。
        /// Description (text).
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 产品是否激活。
        /// Whether product is active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// JSON元数据（jsonb，以字符串形式存储）。
        /// JSON metadata (jsonb, stored as string).
        /// </summary>
        public string? Metadata { get; set; }

        /// <summary>
        /// 创建时间戳（带时区）。
        /// Created timestamp (with timezone).
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }
    }
}
