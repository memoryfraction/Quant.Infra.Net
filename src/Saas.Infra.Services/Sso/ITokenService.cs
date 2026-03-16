using Saas.Infra.Core;
using System.Security.Claims;

namespace Saas.Infra.Services.Sso
{
    /// <summary>
    /// JWT 令牌生成与验证服务接口。
    /// JWT token generation and validation service interface.
    /// </summary>
    public interface ITokenService
    {
        /// <summary>
        /// 生成访问令牌与刷新令牌。
        /// Generates an access token and a refresh token.
        /// </summary>
        /// <param name="email">用户邮箱（稳定标识）。 / User email (stable identifier).</param>
        /// <param name="clientId">客户端标识。 / Client identifier.</param>
        /// <param name="additionalClaims">额外声明。 / Additional claims.</param>
        /// <returns>JWT 令牌响应。 / JWT token response.</returns>
        JwtTokenResponse GenerateToken(string email, string? clientId = null, IEnumerable<Claim>? additionalClaims = null);

        /// <summary>
        /// 验证令牌合法性。
        /// Validates a token.
        /// </summary>
        /// <param name="token">待验证的令牌。 / Token to validate.</param>
        /// <returns>验证通过返回 ClaimsPrincipal，否则返回 null。 / ClaimsPrincipal if valid; otherwise null.</returns>
        ClaimsPrincipal? ValidateToken(string token);
    }
}
