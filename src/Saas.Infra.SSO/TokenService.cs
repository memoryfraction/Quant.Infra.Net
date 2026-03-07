using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Saas.Infra.Core;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Saas.Infra.SSO // 迁移到SSO层（核心！）
{
    /// <summary>
    /// RSA签名的JWT令牌生成服务实现。
    /// RSA-signed implementation for JWT token generation service.
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly JwtOptions _options;
        private readonly Microsoft.IdentityModel.Tokens.RsaSecurityKey _signingKey; // 注入带 KeyId 的签名密钥

        /// <summary>
        /// 初始化 <see cref="TokenService"/> 的新实例（适配RSA签名）。
        /// Initializes a new instance of <see cref="TokenService"/> (RSA-signed version).
        /// </summary>
        /// <param name="options">JWT配置选项 / JWT configuration options</param>
        /// <param name="rsa">RSA加密实例 / RSA encryption instance</param>
        /// <exception cref="ArgumentNullException">参数为空时抛出 / Thrown when parameters are null</exception>
        public TokenService(IOptions<JwtOptions> options, Microsoft.IdentityModel.Tokens.RsaSecurityKey signingKey)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _signingKey = signingKey ?? throw new ArgumentNullException(nameof(signingKey), "RsaSecurityKey is required for JWT signing");
        }

        /// <summary>
        /// 生成RSA签名的JWT访问令牌及刷新令牌。
        /// Generates RSA-signed JWT access token and refresh token.
        /// </summary>
        /// <param name="username">用户标识（必填）/ Username (required)</param>
        /// <param name="clientId">客户端ID（可选）/ Client ID (optional)</param>
        /// <param name="additionalClaims">额外Claim（可选）/ Additional claims (optional)</param>
        /// <returns>RSA签名的令牌响应 / RSA-signed token response</returns>
        /// <exception cref="ArgumentException">用户名为空时抛出 / Thrown when username is empty</exception>
        public JwtTokenResponse GenerateToken(string username, string? clientId = null, IEnumerable<Claim>? additionalClaims = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("username must not be null or whitespace", nameof(username));

            // 构建Claim集合（保留原有逻辑，补充用户ID/角色）
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, username), // JWT标准用户标识
                new Claim(ClaimTypes.Name, username), // ensure Name claim so ClaimsIdentity.Name is populated
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // 令牌唯一ID
                new Claim("client_id", clientId ?? "default"), // 客户端ID
                new Claim(ClaimTypes.Role, "User") // 默认角色
            };

            // 添加额外Claim
            if (additionalClaims != null)
            {
                claims.AddRange(additionalClaims.Where(c => c != null));
            }

            // 核心修改：使用RSA非对称加密签名（替换原有的对称加密）
            var signingCreds = new SigningCredentials(
                key: _signingKey,
                algorithm: SecurityAlgorithms.RsaSha256 // RSA-SHA256签名算法
            );

            // 令牌过期时间（从配置读取）
            var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenExpirationMinutes);

            // 生成RSA签名的JWT令牌
            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                expires: expires,
                signingCredentials: signingCreds
            );

            // Ensure kid present in header when signing key has KeyId
            try
            {
                if (!token.Header.ContainsKey("kid") && !string.IsNullOrEmpty(_signingKey?.KeyId))
                {
                    token.Header["kid"] = _signingKey.KeyId;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to ensure kid in JWT header");
            }

            // 序列化令牌为字符串
            var tokenHandler = new JwtSecurityTokenHandler();
            var accessToken = tokenHandler.WriteToken(token);

            // Diagnostic logging: show full token and header/payload to help debug mismatches
            try
            {
                var produced = tokenHandler.ReadJwtToken(accessToken);
                Log.Information("[TOKEN DEBUG] full token: {Token}", accessToken);
                Log.Information("[TOKEN DEBUG] produced token header alg={Alg}, kid={Kid}", produced.Header.Alg, produced.Header.Kid ?? "(none)");
                Log.Information("[TOKEN DEBUG] produced token rawHeader={RawHeader}", produced.RawHeader);
                Log.Information("[TOKEN DEBUG] produced token rawPayload={RawPayload}", produced.RawPayload);
                Log.Information("[TOKEN DEBUG] signingKey KeyId={KeyId}, has RSA={HasRsa}", _signingKey?.KeyId ?? "(none)", _signingKey?.Rsa != null);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[TOKEN DEBUG] failed to read produced token header");
            }

            Log.Information("RSA-signed token generated for user {Username}", username);

            // 返回令牌响应（刷新令牌逻辑不变）
            return new JwtTokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = Guid.NewGuid().ToString("N"), // 简化GUID格式
                ExpiresIn = (int)(expires - DateTime.UtcNow).TotalSeconds
            };
        }

        /// <summary>
        /// 验证RSA签名的JWT令牌有效性。
        /// Validates RSA-signed JWT token.
        /// </summary>
        /// <param name="token">待验证的令牌 / Token to validate</param>
        /// <returns>验证通过返回ClaimsPrincipal，否则返回null / ClaimsPrincipal if valid, null otherwise</returns>
        /// <exception cref="ArgumentNullException">令牌为空时抛出 / Thrown when token is null</exception>
        public ClaimsPrincipal? ValidateToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentNullException(nameof(token));

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var validationParameters = new TokenValidationParameters
                {
                    // 基础验证配置
                    ValidateIssuer = true,
                    ValidIssuer = _options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _options.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero, // 严格验证过期时间（无偏移）
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,

                // 核心修改：使用注入的 signing key 验证签名
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = _signingKey
                };

                // 验证令牌并解析Claims
                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                // 额外校验：确保使用RSA-SHA256算法
                if (validatedToken is JwtSecurityToken jwtToken &&
                    !string.Equals(jwtToken.Header.Alg, SecurityAlgorithms.RsaSha256, StringComparison.Ordinal))
                {
                    Log.Warning("Unexpected JWT algorithm: {Alg} (expected RS256)", jwtToken.Header.Alg);
                    return null;
                }

                Log.Information("RSA-signed token validated successfully for user {Username}",
                    principal.Identity?.Name ?? "unknown");
                return principal;
            }
            catch (SecurityTokenExpiredException ex)
            {
                Log.Warning(ex, "Token validation failed: expired for user");
                return null;
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                Log.Warning(ex, "Token validation failed: invalid RSA signature");
                return null;
            }
            catch (SecurityTokenException stEx)
            {
                Log.Warning(stEx, "Token validation failed: {Message}", stEx.Message);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error during RSA token validation");
                return null;
            }
        }
    }
}