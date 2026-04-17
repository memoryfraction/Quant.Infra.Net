using System.Threading.Tasks;

namespace Saas.Infra.Core.Schwab
{
    /// <summary>
    /// 嘉信理财 OAuth 认证服务接口。
    /// Charles Schwab OAuth authentication service interface.
    /// </summary>
    public interface ISchwabAuthService
    {
        /// <summary>
        /// 生成 OAuth 授权 URL。
        /// Generates OAuth authorization URL.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>授权 URL。 / Authorization URL.</returns>
        string GenerateAuthorizationUrl(System.Guid userId);

        /// <summary>
        /// 使用授权码交换访问令牌和刷新令牌。
        /// Exchanges authorization code for access token and refresh token.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="authorizationCode">授权码。 / Authorization code.</param>
        /// <returns>令牌响应。 / Token response.</returns>
        Task<SchwabTokenResponse> ExchangeAuthorizationCodeAsync(System.Guid userId, string authorizationCode);

        /// <summary>
        /// 刷新访问令牌。
        /// Refreshes access token.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>新的令牌响应。 / New token response.</returns>
        Task<SchwabTokenResponse> RefreshAccessTokenAsync(System.Guid userId);

        /// <summary>
        /// 获取当前有效的访问令牌。
        /// Gets current valid access token.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <returns>访问令牌。 / Access token.</returns>
        Task<string> GetValidAccessTokenAsync(System.Guid userId);
    }
}
