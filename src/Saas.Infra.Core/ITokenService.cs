using System.Collections.Generic;
using System.Security.Claims;

namespace Saas.Infra.Core
{
    public interface ITokenService
    {
        /// <summary>
        /// 生成 JWT 访问令牌及刷新令牌。
        /// </summary>
        /// <param name="email">用户邮箱（稳定标识）。 / User email (stable identifier).</param>
        /// <param name="clientId">客户端ID。 / Client ID.</param>
        /// <param name="additionalClaims">附加声明。 / Additional claims.</param>
        /// <returns>JWT令牌响应。 / JWT token response.</returns>
        JwtTokenResponse GenerateToken(string email, string? clientId = null, IEnumerable<Claim>? additionalClaims = null);

        /// <summary>
        /// [新增] 验证 JWT 令牌并提取声明主体。
        /// </summary>
        /// <param name="token">要验证的 JWT 字符串。</param>
        /// <returns>如果有效则返回 ClaimsPrincipal，否则返回 null 或抛出异常。</returns>
        ClaimsPrincipal? ValidateToken(string token);
    }
}