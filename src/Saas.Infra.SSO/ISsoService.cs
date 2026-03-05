using Saas.Infra.Core;
using System.Security.Claims;

namespace Saas.Infra.SSO
{
    public interface ISsoService
    {
        /// <summary>
        /// 处理用户登录并生成JWT令牌。
        /// Handles user login and generates JWT tokens.
        /// 内部逻辑：1.根据电子邮件查询用户 -> 2.PasswordHasher校验 -> 3.生成Token
        /// Internal logic: 1. Query user by email -> 2. Verify password -> 3. Generate token
        /// </summary>
        /// <param name="email">用户电子邮件地址 / User email address</param>
        /// <param name="password">用户密码 / User password</param>
        /// <param name="clientId">客户端标识 / Client identifier</param>
        Task<JwtTokenResponse> GenerateTokensAsync(string email, string password, string clientId);

        /// <summary>
        /// 刷新令牌续期。
        /// Refresh token renewal.
        /// 当 Access Token 过期时，使用有效的 Refresh Token 获取新令牌，无需重新输入密码。
        /// When Access Token expires, use valid Refresh Token to get new tokens without re-entering password.
        /// </summary>
        Task<JwtTokenResponse> RefreshTokenAsync(string refreshToken, string clientId);

        /// <summary>
        /// 撤销令牌（登出）。
        /// Revoke token (logout).
        /// 除了撤销刷新令牌，还应考虑将 Access Token 加入黑名单（如果使用了黑名单机制）。
        /// In addition to revoking refresh tokens, consider adding Access Token to blacklist if using blacklist mechanism.
        /// </summary>
        Task RevokeRefreshTokenAsync(string refreshToken);

        /// <summary>
        /// 验证令牌合法性。
        /// Validate token legitimacy.
        /// </summary>
        Task<ClaimsPrincipal?> ValidateTokenAsync(string token);

        /// <summary>
        /// 注册新用户并返回登录令牌（自动登录）。
        /// Registers a new user and returns authentication tokens (auto-login).
        /// </summary>
        /// <param name="email">用户电子邮件地址，不能为空或空白。/ User email address, cannot be null or whitespace.</param>
        /// <param name="password">明文密码，不能为空或空白。/ Plain text password, cannot be null or whitespace.</param>
        /// <param name="username">可选用户名。如果未提供，系统将自动生成。/ Optional username. If not provided, system will auto-generate.</param>
        /// <param name="clientId">可选客户端标识。/ Optional client identifier.</param>
        Task<JwtTokenResponse> RegisterUserAsync(string email, string password, string? username = null, string? clientId = null);
    }
}
