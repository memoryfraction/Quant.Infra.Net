using System;

namespace Saas.Infra.Core
{
    /// <summary>
    /// 用户 DTO，用于服务层与数据层之间传输用户信息。
    /// User DTO used to transfer user information between layers.
    /// </summary>
    public class User
    {
        /// <summary>
        /// Database numeric id (bigserial -> long).
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Stable user GUID (UserId in DB).
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Username (UserName column).
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Display name (optional).
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Password hash.
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Email address.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Phone number (optional).
        /// </summary>
        public string? PhoneNumber { get; set; }

        /// <summary>
        /// Status flag (smallint).
        /// </summary>
        public short Status { get; set; }

        /// <summary>
        /// Created timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Updated timestamp (nullable).
        /// </summary>
        public DateTime? UpdatedTime { get; set; }

        /// <summary>
        /// CreatedBy user id.
        /// </summary>
        public long? CreatedBy { get; set; }

        /// <summary>
        /// UpdatedBy user id.
        /// </summary>
        public long? UpdatedBy { get; set; }

        /// <summary>
        /// Soft-delete flag.
        /// </summary>
        public bool IsDeleted { get; set; }
    }
}
