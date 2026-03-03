using System.Collections.Generic;
using System.Security.Claims;

namespace Saas.Infra.Core
{
    /// <summary>
    /// 令牌服务接口，封装JWT令牌的生成逻辑。
    /// Token service interface that encapsulates JWT token generation logic.
    /// </summary>
    public interface ITokenService
    {
        /// <summary>
        /// 为指定的用户名和客户端ID生成JWT访问令牌及刷新令牌。
        /// Generates a JWT access token and refresh token for the specified username and client id.
        /// </summary>
        /// <param name="username">用户标识，不能为空或空白。 / The username; cannot be null or whitespace.</param>
        /// <param name="clientId">可选客户端标识符。/ Optional client identifier.</param>
        /// <param name="additionalClaims">可选的额外Claims。/ Optional additional claims.</param>
        /// <returns>包含访问令牌、刷新令牌和过期时间的响应对象。 / A response object that contains access token, refresh token and expiration.</returns>
        JwtTokenResponse GenerateToken(string username, string? clientId = null, IEnumerable<Claim>? additionalClaims = null);
    }
}
