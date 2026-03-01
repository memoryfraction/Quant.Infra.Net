namespace Saas.Infra.Core
{
	/// <summary>
	/// JWT Token 响应DTO（SSO返回给客户端的标准格式）
	/// </summary>
	public class JwtTokenResponse
	{
		/// <summary>
		/// 访问令牌（JWT）
		/// </summary>
		public string AccessToken { get; set; } = string.Empty;

		/// <summary>
		/// 刷新令牌（用于续期AccessToken）
		/// </summary>
		public string RefreshToken { get; set; } = string.Empty;

		/// <summary>
		/// AccessToken过期时间（秒）
		/// </summary>
		public int ExpiresIn { get; set; }

		/// <summary>
		/// Token类型（固定为Bearer）
		/// </summary>
		public string TokenType => "Bearer";

		/// <summary>
		/// 颁发时间（UTC）
		/// </summary>
		public DateTime IssuedAt => DateTime.UtcNow;
	}
}
