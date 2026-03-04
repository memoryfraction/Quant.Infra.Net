using System;

namespace Saas.Infra.Core
{
    /// <summary>
    /// 刷新令牌记录 DTO，用于仓储与服务之间传递数据。
    /// Refresh token record DTO used between repository and services.
    /// </summary>
    public class RefreshTokenRecord
    {
        /// <summary>
        /// Refresh token record id (UUID).
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Numeric user id (bigserial) owning the token.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// SHA256 hash of the refresh token.
        /// </summary>
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>
        /// Expiration time of the refresh token.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Whether the token has been revoked.
        /// </summary>
        public bool Revoked { get; set; }

        /// <summary>
        /// Creation timestamp.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Optional hash of the replacement token.
        /// </summary>
        public string? ReplacedByHash { get; set; }

        public Guid CreatedBy { get; set; }
    }
}
