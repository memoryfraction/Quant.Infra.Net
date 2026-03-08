using System;

namespace Saas.Infra.Core
{
    /// <summary>
    /// 用户领域模型，用于服务层与数据层之间传输用户信息。
    /// User domain model used to transfer user information between layers.
    /// </summary>
    public class User
    {
        /// <summary>
        /// 用户唯一标识（UUID主键）。
        /// User unique identifier (UUID primary key).
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 用户名（UserName列）。
        /// Username (UserName column).
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 密码哈希。
        /// Password hash.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// 邮箱地址。
        /// Email address.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 电话号码（可选）。
        /// Phone number (optional).
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// 状态标志（1=正常，0=禁用）。
        /// Status flag (1=Active, 0=Disabled).
        /// </summary>
        public short Status { get; set; }

        /// <summary>
        /// 最后登录时间戳。
        /// Last login timestamp.
        /// </summary>
        public DateTimeOffset? LastLoginTime { get; set; }

        /// <summary>
        /// 创建时间戳。
        /// Created timestamp.
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// 更新时间戳（可选）。
        /// Updated timestamp (optional).
        /// </summary>
        public DateTimeOffset? UpdatedTime { get; set; }

        /// <summary>
        /// 软删除标记。
        /// Soft-delete flag.
        /// </summary>
        public bool IsDeleted { get; set; }
    }
}
