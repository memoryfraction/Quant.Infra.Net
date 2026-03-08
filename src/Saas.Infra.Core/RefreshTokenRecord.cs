using System;

namespace Saas.Infra.Core
{
    /// <summary>
    /// 刷新令牌记录领域模型，用于仓储与服务之间传递数据。
    /// Refresh token record domain model used between repository and services.
    /// </summary>
    public class RefreshTokenRecord
    {
        /// <summary>
        /// 刷新令牌记录ID（UUID）。
        /// Refresh token record ID (UUID).
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// 拥有该令牌的用户ID。
        /// User ID owning the token.
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// 刷新令牌的SHA256哈希值。
        /// SHA256 hash of the refresh token.
        /// </summary>
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>
        /// 刷新令牌过期时间。
        /// Expiration time of the refresh token.
        /// </summary>
        public DateTimeOffset ExpiresAt { get; set; }

        /// <summary>
        /// 令牌是否已撤销。
        /// Whether the token has been revoked.
        /// </summary>
        public bool Revoked { get; set; }

        /// <summary>
        /// 创建时间戳。
        /// Creation timestamp.
        /// </summary>
        public DateTimeOffset CreatedTime { get; set; }

        /// <summary>
        /// 创建此令牌的用户ID（可选）。
        /// User ID who created this token (optional).
        /// </summary>
        public Guid? CreatedBy { get; set; }
    }
}
