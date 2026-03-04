using System.Collections.Generic;
using System.Security.Claims;

namespace Saas.Infra.Core
{
    public interface ITokenService
    {
        /// <summary>
        /// 生成 JWT 访问令牌及刷新令牌。
        /// </summary>
        JwtTokenResponse GenerateToken(string username, string? clientId = null, IEnumerable<Claim>? additionalClaims = null);

        /// <summary>
        /// [新增] 验证 JWT 令牌并提取声明主体。
        /// </summary>
        /// <param name="token">要验证的 JWT 字符串。</param>
        /// <returns>如果有效则返回 ClaimsPrincipal，否则返回 null 或抛出异常。</returns>
        ClaimsPrincipal? ValidateToken(string token);
    }
}