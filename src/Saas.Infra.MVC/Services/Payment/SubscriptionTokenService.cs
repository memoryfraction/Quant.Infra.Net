using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace Saas.Infra.MVC.Services.Payment
{
    /// <summary>
    /// 订阅令牌服务实现，使用RSA签名生成订阅访问JWT。
    /// Subscription token service implementation that generates RSA-signed subscription JWTs.
    /// </summary>
    public class SubscriptionTokenService : ISubscriptionTokenService
    {
        private readonly IConfiguration _configuration;
        private readonly RsaSecurityKey _jwtSigningKey;

        /// <summary>
        /// 初始化<see cref="SubscriptionTokenService"/>的新实例。
        /// Initializes a new instance of the <see cref="SubscriptionTokenService"/> class.
        /// </summary>
        /// <param name="configuration">配置。 / Configuration.</param>
        /// <param name="jwtSigningKey">RSA签名密钥。 / RSA signing key.</param>
        /// <exception cref="ArgumentNullException">当参数为null时抛出。 / Thrown when parameters are null.</exception>
        public SubscriptionTokenService(IConfiguration configuration, RsaSecurityKey jwtSigningKey)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _jwtSigningKey = jwtSigningKey ?? throw new ArgumentNullException(nameof(jwtSigningKey));
        }

        /// <summary>
        /// 生成订阅访问令牌（JWT），包含身份、业务、完整性校验等安全声明。
        /// Generates a subscription access token (JWT) containing identity, business, and integrity claims.
        /// </summary>
        /// <param name="request">令牌生成请求参数。 / Token generation request parameters.</param>
        /// <returns>访问令牌和过期秒数。 / Access token and expiry in seconds.</returns>
        /// <exception cref="ArgumentNullException">当request为null时抛出。 / Thrown when request is null.</exception>
        public SubscriptionTokenResult GenerateToken(SubscriptionTokenRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var now = DateTimeOffset.UtcNow;
            var expires = now.AddHours(24);

            // SHA256 context fingerprint: binds token to specific user + subscription + order
            var ctxHash = ComputeContextHash($"{request.UserId}|{request.SubscriptionId}|{request.OrderId}");

            var claims = new List<Claim>
            {
                // ── identity ──
                new Claim(JwtRegisteredClaimNames.Sub, request.UserId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, request.UserId.ToString()),
                new Claim(ClaimTypes.Name, request.UserEmail),

                // ── token meta ──
                new Claim("token_type", "subscription"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),

                // ── business ──
                new Claim("productId", request.ProductId.ToString()),
                new Claim("productName", request.ProductName),
                new Claim("subscriptionId", request.SubscriptionId.ToString()),
                new Claim("subscriptionStatus", request.SubscriptionStatus.ToString()),
                new Claim("subscriptionStartUtc", request.SubscriptionStartUtc.UtcDateTime.ToString("O")),
                new Claim("subscriptionEndUtc", (request.SubscriptionEndUtc ?? now).UtcDateTime.ToString("O")),
                new Claim("orderId", request.OrderId.ToString()),

                // ── integrity ──
                new Claim("ctx_hash", ctxHash)
            };

            var signingCredentials = new SigningCredentials(_jwtSigningKey, SecurityAlgorithms.RsaSha256);
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"] ?? JwtConstants.Issuer,
                audience: _configuration["Jwt:SubscriptionAudience"] ?? "Saas.Infra.Subscription",
                claims: claims,
                notBefore: now.UtcDateTime,
                expires: expires.UtcDateTime,
                signingCredentials: signingCredentials);

            if (!token.Header.ContainsKey("kid") && !string.IsNullOrWhiteSpace(_jwtSigningKey.KeyId))
            {
                token.Header["kid"] = _jwtSigningKey.KeyId;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var accessToken = tokenHandler.WriteToken(token);
            var expiresIn = (int)(expires - now).TotalSeconds;

            Log.Debug("Subscription token generated for user {UserId}, subscription {SubscriptionId}",
                request.UserId, request.SubscriptionId);

            return new SubscriptionTokenResult
            {
                AccessToken = accessToken,
                ExpiresIn = expiresIn
            };
        }

        /// <summary>
        /// 计算上下文指纹哈希（SHA256），用于令牌完整性校验。
        /// Computes a SHA256 context fingerprint for token integrity verification.
        /// </summary>
        private static string ComputeContextHash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToBase64String(bytes);
        }
    }
}
