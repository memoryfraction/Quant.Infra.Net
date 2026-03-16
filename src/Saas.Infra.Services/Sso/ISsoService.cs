using Saas.Infra.Core;
using System.Security.Claims;

namespace Saas.Infra.Services.Sso
{
    /// <summary>
    /// 单点登录服务接口。
    /// Single sign-on service interface.
    /// </summary>
    public interface ISsoService
    {
        /// <summary>
        /// 处理用户登录并生成 JWT 令牌。
        /// Handles user login and generates JWT tokens.
        /// </summary>
        /// <param name="email">用户电子邮件地址。 / User email address.</param>
        /// <param name="password">用户密码。 / User password.</param>
        /// <param name="clientId">客户端标识。 / Client identifier.</param>
        /// <returns>JWT 令牌响应。 / JWT token response.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when parameters are invalid.</exception>
        Task<JwtTokenResponse> GenerateTokensAsync(string email, string password, string clientId);

        /// <summary>
        /// 刷新令牌续期。
        /// Refresh token renewal.
        /// </summary>
        /// <param name="refreshToken">刷新令牌。 / Refresh token.</param>
        /// <param name="clientId">客户端标识。 / Client identifier.</param>
        /// <returns>JWT 令牌响应。 / JWT token response.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when parameters are invalid.</exception>
        Task<JwtTokenResponse> RefreshTokenAsync(string refreshToken, string clientId);

        /// <summary>
        /// 撤销刷新令牌（登出）。
        /// Revokes the refresh token (logout).
        /// </summary>
        /// <param name="refreshToken">刷新令牌。 / Refresh token.</param>
        /// <returns>表示操作完成的任务。 / Task representing completion.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when parameters are invalid.</exception>
        Task RevokeRefreshTokenAsync(string refreshToken);

        /// <summary>
        /// 验证令牌合法性。
        /// Validates token legitimacy.
        /// </summary>
        /// <param name="token">待验证的令牌。 / Token to validate.</param>
        /// <returns>验证通过返回 ClaimsPrincipal，否则返回 null。 / ClaimsPrincipal if valid; otherwise null.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when parameters are invalid.</exception>
        Task<ClaimsPrincipal?> ValidateTokenAsync(string token);

        /// <summary>
        /// 注册新用户并返回登录令牌（自动登录）。
        /// Registers a new user and returns authentication tokens (auto-login).
        /// </summary>
        /// <param name="email">用户电子邮件地址，不能为空或空白。 / User email address; cannot be null or whitespace.</param>
        /// <param name="password">明文密码，不能为空或空白。 / Plain text password; cannot be null or whitespace.</param>
        /// <param name="username">可选用户名。如果未提供，系统将自动生成。 / Optional username. If not provided, system will auto-generate.</param>
        /// <param name="clientId">可选客户端标识。 / Optional client identifier.</param>
        /// <returns>JWT 令牌响应。 / JWT token response.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when parameters are invalid.</exception>
        Task<JwtTokenResponse> RegisterUserAsync(string email, string password, string? username = null, string? clientId = null);
    }
}
