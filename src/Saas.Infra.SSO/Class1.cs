using Saas.Infra.Core;
using System.Security.Claims;

namespace Saas.Infra.SSO
{
    public interface ISsoService
	{
		Task<JwtTokenResponse> GenerateTokensAsync(string userId, string clientId);
		Task<ClaimsPrincipal> ValidateTokenAsync(string token);
		Task RevokeRefreshTokenAsync(string refreshToken);
	}
}
