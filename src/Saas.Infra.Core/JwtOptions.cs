namespace Saas.Infra.Core
{
    /// <summary>
    /// JWT 配置选项：保存签名密钥、颁发者、受众和过期时间（分钟）。
    /// JWT configuration options: holds signing key, issuer, audience and expiration minutes.
    /// </summary>
    public class JwtOptions
    {
        /// <summary>
        /// 对称签名密钥（至少 16 字节以上）。 / Symmetric signing key (should be at least 16 bytes).
        /// </summary>
        public string SigningKey { get; set; } = string.Empty;

        /// <summary>
        /// JWT 的发行者（issuer）。 / The issuer for the JWT.
        /// </summary>
        public string Issuer { get; set; } = JwtConstants.Issuer;

        /// <summary>
        /// JWT 的受众（audience）。 / The audience for the JWT.
        /// </summary>
        public string Audience { get; set; } = "Saas.Infra.Clients";

        /// <summary>
        /// 访问令牌过期时间（分钟）。 / Access token expiration in minutes.
        /// </summary>
        public int AccessTokenExpirationMinutes { get; set; } = 60;
    }
}
