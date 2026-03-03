using System.ComponentModel.DataAnnotations;

namespace Saas.Infra.Core
{
	/// <summary>
	/// 表示JWT令牌响应的数据传输对象。包含访问令牌、刷新令牌、令牌类型和过期时间信息。
	/// Represents the data transfer object for a JWT token response, including access token, refresh token, token type and expiration information.
	/// </summary>
	public class JwtTokenResponse
	{
		/// <summary>
		/// 获取或设置JWT访问令牌字符串。 / Gets or sets the JWT access token string.
		/// </summary>
		/// <value>Base64编码的JWT令牌字符串。 / The Base64-encoded JWT token string.</value>
		[Required]
		public string AccessToken { get; set; } = string.Empty;

		/// <summary>
		/// 获取或设置刷新令牌字符串。 / Gets or sets the refresh token string.
		/// </summary>
		/// <value>用于续期AccessToken的刷新令牌。 / The refresh token used to renew the access token.</value>
		public string RefreshToken { get; set; } = string.Empty;

		/// <summary>
		/// 获取或设置令牌过期时间（秒）。 / Gets or sets the token expiration time in seconds.
		/// </summary>
		/// <value>从当前时间开始计算的过期秒数，必须大于0。 / The number of seconds until expiration from now; must be greater than 0.</value>
		[Range(1, int.MaxValue)]
		public int ExpiresIn { get; set; }

		/// <summary>
		/// 获取令牌类型，固定为 "Bearer"。 / Gets the token type, fixed to "Bearer".
		/// </summary>
		/// <value>令牌类型标识符，始终返回 "Bearer"。 / The token type identifier, always returns "Bearer".</value>
		[Required]
		public string TokenType => "Bearer";

		/// <summary>
		/// 获取令牌颁发时间（UTC）。 / Gets the token issuance time (UTC).
		/// </summary>
		/// <value>令牌颁发的UTC时间戳。 / The UTC timestamp when the token was issued.</value>
		public DateTime IssuedAt => DateTime.UtcNow;
	}
}
