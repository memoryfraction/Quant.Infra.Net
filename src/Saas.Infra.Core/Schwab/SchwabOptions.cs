namespace Saas.Infra.Core.Schwab
{
    /// <summary>
    /// 嘉信理财 API 配置选项。
    /// Charles Schwab API configuration options.
    /// </summary>
    public class SchwabOptions
    {
        /// <summary>
        /// 客户端 ID（App Key）。
        /// Client ID (App Key).
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// 客户端密钥（App Secret）。
        /// Client secret (App Secret).
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// 回调 URL。
        /// Redirect URI.
        /// </summary>
        public string RedirectUri { get; set; } = "https://127.0.0.1/schwab/callback";

        /// <summary>
        /// API 基础 URL。
        /// API base URL.
        /// </summary>
        public string BaseUrl { get; set; } = "https://api.schwabapi.com";

        /// <summary>
        /// OAuth 授权端点。
        /// OAuth authorization endpoint.
        /// </summary>
        public string AuthorizationEndpoint { get; set; } = "https://api.schwabapi.com/v1/oauth/authorize";

        /// <summary>
        /// OAuth 令牌端点。
        /// OAuth token endpoint.
        /// </summary>
        public string TokenEndpoint { get; set; } = "https://api.schwabapi.com/v1/oauth/token";

        /// <summary>
        /// Trader API 基础路径。
        /// Trader API base path.
        /// </summary>
        public string TraderApiPath { get; set; } = "/trader/v1";

        /// <summary>
        /// Market Data API 基础路径。
        /// Market Data API base path.
        /// </summary>
        public string MarketDataApiPath { get; set; } = "/marketdata/v1";

        /// <summary>
        /// 访问令牌过期时间（分钟）。
        /// Access token expiration in minutes.
        /// </summary>
        public int AccessTokenExpirationMinutes { get; set; } = 30;

        /// <summary>
        /// 刷新令牌过期时间（天）。
        /// Refresh token expiration in days.
        /// </summary>
        public int RefreshTokenExpirationDays { get; set; } = 7;

        /// <summary>
        /// 令牌刷新提前时间（分钟）。
        /// Token refresh advance time in minutes.
        /// </summary>
        public int TokenRefreshAdvanceMinutes { get; set; } = 1;

        /// <summary>
        /// API 请求超时时间（秒）。
        /// API request timeout in seconds.
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// 最大重试次数。
        /// Maximum retry count.
        /// </summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>
        /// 是否启用自动令牌刷新。
        /// Whether to enable automatic token refresh.
        /// </summary>
        public bool EnableAutoTokenRefresh { get; set; } = true;
    }
}
