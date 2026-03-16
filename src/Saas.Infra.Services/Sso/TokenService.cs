using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Saas.Infra.Core;
using Serilog.Events;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Saas.Infra.Services.Sso
{
    /// <summary>
    /// RSA 签名的 JWT 令牌生成与验证服务实现。
    /// RSA-signed JWT token generation and validation service implementation.
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly JwtOptions _options;
        private readonly RsaSecurityKey _signingKey;

        /// <summary>
        /// 初始化 <see cref="TokenService"/> 的新实例。
        /// Initializes a new instance of <see cref="TokenService"/>.
        /// </summary>
        /// <param name="options">JWT 配置选项。 / JWT configuration options.</param>
        /// <param name="signingKey">RSA 签名密钥。 / RSA signing key.</param>
        /// <exception cref="ArgumentNullException">当参数为 null 时抛出。 / Thrown when arguments are null.</exception>
        public TokenService(IOptions<JwtOptions> options, RsaSecurityKey signingKey)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            _options = options.Value ?? throw new ArgumentNullException(nameof(options));
            _signingKey = signingKey ?? throw new ArgumentNullException(nameof(signingKey));
        }

        /// <summary>
        /// 生成访问令牌与刷新令牌。
        /// Generates an access token and a refresh token.
        /// </summary>
        /// <param name="email">用户邮箱（稳定标识）。 / User email (stable identifier).</param>
        /// <param name="clientId">客户端标识。 / Client identifier.</param>
        /// <param name="additionalClaims">额外声明。 / Additional claims.</param>
        /// <returns>JWT 令牌响应。 / JWT token response.</returns>
        /// <exception cref="ArgumentException">当 email 为空或空白时抛出。 / Thrown when email is null or whitespace.</exception>
        public JwtTokenResponse GenerateToken(string email, string? clientId = null, IEnumerable<Claim>? additionalClaims = null)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be null or whitespace", nameof(email));

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, email),
                new(ClaimTypes.Name, email),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new("client_id", clientId ?? "default")
            };

            if (additionalClaims != null)
            {
                claims.AddRange(additionalClaims.Where(c => c != null));
            }

            var existingRoleClaims = claims
                .Where(c => c.Type == ClaimTypes.Role && !string.IsNullOrWhiteSpace(c.Value))
                .ToList();

            if (existingRoleClaims.Any())
            {
                var roleClaim = existingRoleClaims.First();
                claims.Remove(roleClaim);
                claims.Add(new Claim(ClaimTypes.Role, NormalizeRoleCode(roleClaim.Value)));
            }
            else
            {
                claims.Add(new Claim(ClaimTypes.Role, RoleCodes.User));
            }

            var signingCreds = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256);
            var expiresUtc = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes);

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                expires: expiresUtc,
                signingCredentials: signingCreds);

            if (!token.Header.ContainsKey("kid") && !string.IsNullOrEmpty(_signingKey.KeyId))
            {
                token.Header["kid"] = _signingKey.KeyId;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var accessToken = tokenHandler.WriteToken(token);

            try
            {
                var produced = tokenHandler.ReadJwtToken(accessToken);
                UtilityService.LogAndWriteLine(LogEventLevel.Information, "[TOKEN DEBUG] full token: {Token}", accessToken);
                UtilityService.LogAndWriteLine(LogEventLevel.Information, "[TOKEN DEBUG] produced token header alg={Alg}, kid={Kid}", produced.Header.Alg, produced.Header.Kid ?? "(none)");
                UtilityService.LogAndWriteLine(LogEventLevel.Information, "[TOKEN DEBUG] produced token rawHeader={RawHeader}", produced.RawHeader);
                UtilityService.LogAndWriteLine(LogEventLevel.Information, "[TOKEN DEBUG] produced token rawPayload={RawPayload}", produced.RawPayload);
                UtilityService.LogAndWriteLine(LogEventLevel.Information, "[TOKEN DEBUG] signingKey KeyId={KeyId}, has RSA={HasRsa}", _signingKey.KeyId ?? "(none)", _signingKey.Rsa != null);
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine(ex, LogEventLevel.Warning, "[TOKEN DEBUG] Failed to read produced token header");
            }

            UtilityService.LogAndWriteLine(LogEventLevel.Information, "RSA-signed token generated for email {Email}", email);

            return new JwtTokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = Guid.NewGuid().ToString("N"),
                ExpiresIn = (int)(expiresUtc - DateTime.UtcNow).TotalSeconds
            };
        }

        /// <summary>
        /// 验证 RSA 签名的 JWT 令牌有效性。
        /// Validates an RSA-signed JWT token.
        /// </summary>
        /// <param name="token">待验证的令牌。 / Token to validate.</param>
        /// <returns>验证通过返回 ClaimsPrincipal，否则返回 null。 / ClaimsPrincipal if valid; otherwise null.</returns>
        /// <exception cref="ArgumentNullException">当 token 为空或空白时抛出。 / Thrown when token is null or whitespace.</exception>
        public ClaimsPrincipal? ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentNullException(nameof(token));

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _options.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = _signingKey
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                if (validatedToken is JwtSecurityToken jwtToken &&
                    !string.Equals(jwtToken.Header.Alg, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal))
                {
                    UtilityService.LogAndWriteLine(LogEventLevel.Warning, "Unexpected JWT algorithm: {Alg} (expected RS256)", jwtToken.Header.Alg);
                    return null;
                }

                UtilityService.LogAndWriteLine(LogEventLevel.Information, "RSA-signed token validated successfully for user {Username}", principal.Identity?.Name ?? "unknown");
                return principal;
            }
            catch (SecurityTokenExpiredException ex)
            {
                UtilityService.LogAndWriteLine(ex, LogEventLevel.Warning, "Token validation failed: expired");
                return null;
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                UtilityService.LogAndWriteLine(ex, LogEventLevel.Warning, "Token validation failed: invalid signature");
                return null;
            }
            catch (SecurityTokenException ex)
            {
                UtilityService.LogAndWriteLine(ex, LogEventLevel.Warning, "Token validation failed: {Message}", ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine(ex, LogEventLevel.Error, "Unexpected error during token validation");
                return null;
            }
        }

        /// <summary>
        /// 规范化角色代码为系统统一格式。
        /// Normalizes a role code to the canonical system format.
        /// </summary>
        /// <param name="roleValue">角色值。 / Role value.</param>
        /// <returns>规范化后的角色代码。 / Normalized role code.</returns>
        /// <exception cref="ArgumentNullException">当 roleValue 为空或空白时抛出。 / Thrown when roleValue is null or whitespace.</exception>
        public static string NormalizeRoleCode(string roleValue)
        {
            if (string.IsNullOrWhiteSpace(roleValue))
                throw new ArgumentNullException(nameof(roleValue));

            var normalized = roleValue.Trim();

            if (string.Equals(normalized, RoleCodes.SuperAdmin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, UserRole.Super_Admin.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return RoleCodes.SuperAdmin;
            }

            if (string.Equals(normalized, RoleCodes.Admin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return RoleCodes.Admin;
            }

            if (string.Equals(normalized, RoleCodes.User, StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, UserRole.User.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return RoleCodes.User;
            }

            return RoleCodes.User;
        }
    }
}
