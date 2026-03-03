using Saas.Infra.Core;
using System.Security.Claims;

namespace Saas.Infra.SSO
{
    public class SsoService : ISsoService
    {
        private readonly Saas.Infra.Core.IUserRepository _userRepository;
        private readonly Saas.Infra.Core.ITokenService _tokenService;

        public SsoService(Saas.Infra.Core.IUserRepository userRepository, Saas.Infra.Core.ITokenService tokenService)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        }

        public async Task<JwtTokenResponse> GenerateTokensAsync(string userId, string password, string clientId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId must not be null or whitespace", nameof(userId));
            if (password is null)
                throw new ArgumentNullException(nameof(password));

            var user = await _userRepository.GetByUsernameAsync(userId);
            if (user == null)
                throw new InvalidOperationException("User not found.");

            // Verify password using BCrypt (stored hash expected to be BCrypt)
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new InvalidOperationException("Invalid credentials.");

            // Delegate token generation to token service
            return _tokenService.GenerateToken(user.Username, clientId);
        }

        public Task RevokeRefreshTokenAsync(string refreshToken)
        {
            if (refreshToken is null)
                throw new ArgumentNullException(nameof(refreshToken));

            // TODO: Persist and revoke refresh tokens in a storage (DB/Cache).
            // For now just throw NotImplementedException to indicate future work.
            throw new NotImplementedException();
        }

        public Task<ClaimsPrincipal> ValidateTokenAsync(string token)
        {
            if (token is null)
                throw new ArgumentNullException(nameof(token));

            // Token validation is handled by JWT middleware in most cases.
            // If needed, implement token validation using TokenValidationParameters and JwtSecurityTokenHandler.
            throw new NotImplementedException();
        }
    }
}
