using System;

namespace Saas.Infra.Core.Schwab
{
    /// <summary>
    /// 嘉信理财 OAuth 令牌响应。
    /// Charles Schwab OAuth token response.
    /// </summary>
    public class SchwabTokenResponse
    {
        /// <summary>
        /// 访问令牌（30分钟有效期）。
        /// Access token (30 minutes validity).
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// 刷新令牌（7天有效期）。
        /// Refresh token (7 days validity).
        /// </summary>
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// 令牌类型（通常为 Bearer）。
        /// Token type (usually Bearer).
        /// </summary>
        public string TokenType { get; set; } = "Bearer";

        /// <summary>
        /// 访问令牌过期时间（秒）。
        /// Access token expiration in seconds.
        /// </summary>
        public int ExpiresIn { get; set; }

        /// <summary>
        /// 令牌作用域。
        /// Token scope.
        /// </summary>
        public string Scope { get; set; } = string.Empty;

        /// <summary>
        /// 令牌创建时间。
        /// Token creation time.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// 令牌过期时间。
        /// Token expiration time.
        /// </summary>
        public DateTimeOffset ExpiresAt => CreatedAt.AddSeconds(ExpiresIn);

        /// <summary>
        /// 检查访问令牌是否已过期。
        /// Checks if access token is expired.
        /// </summary>
        public bool IsAccessTokenExpired => DateTimeOffset.UtcNow >= ExpiresAt.AddMinutes(-1);
    }
}
