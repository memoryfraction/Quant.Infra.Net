using Saas.Infra.Core;
using System.Security.Claims;

namespace Saas.Infra.SSO
{
    public interface ISsoService
    {
        /// <summary>
        /// [增强] 统一登录入口。
        /// 内部逻辑：1.查询用户 -> 2.PasswordHasher校验 -> 3.生成Token
        /// </summary>
        Task<JwtTokenResponse> GenerateTokensAsync(string userId, string password, string clientId);

        /// <summary>
        /// [新增] 刷新令牌续期。
        /// 当 Access Token 过期时，使用有效的 Refresh Token 获取新令牌，无需重新输入密码。
        /// </summary>
        Task<JwtTokenResponse> RefreshTokenAsync(string refreshToken, string clientId);

        /// <summary>
        /// [增强] 撤销令牌（登出）。
        /// 除了撤销刷新令牌，还应考虑将 Access Token 加入黑名单（如果使用了黑名单机制）。
        /// </summary>
        Task RevokeRefreshTokenAsync(string refreshToken);

        /// <summary>
        /// [保留] 验证令牌合法性。
        /// </summary>
        Task<ClaimsPrincipal> ValidateTokenAsync(string token);

        /// <summary>
        /// 注册新用户并返回登录令牌（自动登录）。
        /// Registers a new user and returns authentication tokens (auto-login).
        /// </summary>
        /// <param name="username">用户名，不能为空或空白。</param>
        /// <param name="password">明文密码，不能为空或空白。</param>
        /// <param name="displayName">可选显示名称。</param>
        /// <param name="clientId">可选客户端标识。</param>
        Task<JwtTokenResponse> RegisterUserAsync(string username, string password, string? displayName = null, string? clientId = null);
    }
}
