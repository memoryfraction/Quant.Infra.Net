using System;
using System.Collections.Generic;
using System.Text;

namespace Saas.Infra.Core
{
	/// <summary>
	/// 定义JWT令牌处理相关的常量值。包含令牌颁发者、访问令牌过期时间和刷新令牌过期时间等配置常量。
	/// Defines constant values related to JWT token handling, including issuer, access token expiration and refresh token expiration.
	/// </summary>
	public static class JwtConstants
	{
		/// <summary>
		/// JWT令牌的颁发者标识符，值为 "Saas.Infra.SSO"。用于JWT令牌的issuer声明，标识令牌的颁发方。
		/// The issuer identifier for JWT tokens, set to "Saas.Infra.SSO". Used as the issuer claim to identify the token issuer.
		/// </summary>
		public const string Issuer = "Saas.Infra.SSO";

		/// <summary>
		/// 访问令牌的默认过期时间（分钟），值为 60。用于设置JWT访问令牌的有效期，过期后需要使用刷新令牌获取新的访问令牌。
		/// Default expiration time for access tokens in minutes (60). Used to set the validity period for JWT access tokens; after expiration a refresh token is required.
		/// </summary>
		public const int AccessTokenExpirationMinutes = 60;

		/// <summary>
		/// 刷新令牌的默认过期时间（天），值为 7。用于设置JWT刷新令牌的有效期，过期后用户需要重新登录。
		/// Default expiration time for refresh tokens in days (7). Used to set the validity period for JWT refresh tokens; after expiration the user must re-authenticate.
		/// </summary>
		public const int RefreshTokenExpirationDays = 7;
	}
}
