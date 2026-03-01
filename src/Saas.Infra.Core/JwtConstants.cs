using System;
using System.Collections.Generic;
using System.Text;

namespace Saas.Infra.Core
{
	public static class JwtConstants
	{
		public const string Issuer = "Saas.Infra.SSO";
		public const int AccessTokenExpirationMinutes = 60;
		public const int RefreshTokenExpirationDays = 7;
	}
}
