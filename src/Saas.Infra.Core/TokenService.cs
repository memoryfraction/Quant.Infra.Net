using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Saas.Infra.Core
{
    /// <summary>
    /// 默认的 JWT 令牌生成服务实现。
    /// Default implementation for JWT token generation service.
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly JwtOptions _options;

        /// <summary>
        /// 初始化 <see cref="TokenService"/> 的新实例。
        /// Initializes a new instance of <see cref="TokenService"/>.
        /// </summary>
        /// <param name="options">绑定的 JWT 配置。 / Bound JWT configuration.</param>
        /// <param name="logger">可选的日志器。 / Optional logger.</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="options"/> 为 null 时抛出。 / Thrown when <paramref name="options"/> is null.</exception>
        public TokenService(IOptions<JwtOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// 为指定的用户名和客户端ID生成JWT访问令牌及刷新令牌。
        /// Generates a JWT access token and refresh token for the specified username and client id.
        /// </summary>
        /// <param name="username">用户标识，不能为空或空白。 / The username; cannot be null or whitespace.</param>
        /// <param name="clientId">可选客户端标识符。/ Optional client identifier.</param>
        /// <param name="additionalClaims">可选的额外Claims。/ Optional additional claims.</param>
        /// <returns>包含访问令牌、刷新令牌和过期时间的响应对象。 / A response object that contains access token, refresh token and expiration.</returns>
        /// <exception cref="ArgumentException">当 <paramref name="username"/> 为空或仅包含空白时抛出。 / Thrown when <paramref name="username"/> is null or whitespace.</exception>
        public JwtTokenResponse GenerateToken(string username, string? clientId = null, IEnumerable<Claim>? additionalClaims = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("username must not be null or whitespace", nameof(username));

            if (string.IsNullOrWhiteSpace(_options.SigningKey))
                throw new InvalidOperationException("JWT signing key is not configured.");

            // build claims
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("client_id", clientId ?? "default"),
                new Claim(ClaimTypes.Role, "User")
            };

            if (additionalClaims != null)
            {
                claims.AddRange(additionalClaims.Where(c => c != null));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes);

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                expires: expires,
                signingCredentials: creds);

            var tokenHandler = new JwtSecurityTokenHandler();
            var accessToken = tokenHandler.WriteToken(token);

            Log.Information("Generated token for user {Username}", username);

            return new JwtTokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = Guid.NewGuid().ToString(),
                ExpiresIn = (int)(expires - DateTime.UtcNow).TotalSeconds
            };
        }

        /// <summary>
        /// 验证给定的 JWT 字符串并返回声明主体（如果验证成功）。
        /// Validates the provided JWT string and returns the claims principal if validation succeeds.
        /// </summary>
        /// <param name="token">要验证的 JWT 字符串，不能为空或空白。/ The JWT string to validate; cannot be null or whitespace.</param>
        /// <returns>如果令牌有效则返回 <see cref="ClaimsPrincipal"/>，否则返回 null。</returns>
        public ClaimsPrincipal? ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentNullException(nameof(token));

            if (string.IsNullOrWhiteSpace(_options.SigningKey))
                throw new InvalidOperationException("JWT signing key is not configured.");

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5),
                    RequireSignedTokens = true,
                    RequireExpirationTime = true
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                // optional: ensure token uses expected algorithm
                if (validatedToken is JwtSecurityToken jwt &&
                    !string.Equals(jwt.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.Ordinal))
                {
                    Log.Warning("Unexpected JWT algorithm: {Alg}", jwt.Header.Alg);
                    return null;
                }

                return principal;
            }
            catch (SecurityTokenException stEx)
            {
                Log.Warning(stEx, "Token validation failed: {Message}", stEx.Message);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error during token validation");
                return null;
            }
        }
    }

}
