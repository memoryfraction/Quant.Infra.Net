using Saas.Infra.Core;
using Serilog;
using System.Security.Claims;

namespace Saas.Infra.SSO
{
    /// <summary>
    /// 单点登录服务的实现，负责用户认证、令牌生成、刷新与撤销等操作。
    /// Implementation of SSO service responsible for user authentication, token issuance, refresh and revocation operations.
    /// </summary>
    public class SsoService : ISsoService
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IRefreshTokenRepository _refreshTokenRepository;

        public SsoService(
            IUserRepository userRepository,
            ITokenService tokenService,
            IPasswordHasher passwordHasher,
            IRefreshTokenRepository refreshTokenRepository)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        }

        public async Task<JwtTokenResponse> RegisterUserAsync(string username, string password, string? displayName = null, string? clientId = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("username must not be null or whitespace", nameof(username));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("password must not be null or whitespace", nameof(password));

            var existing = await _userRepository.GetByUsernameAsync(username);
            if (existing != null)
                throw new InvalidOperationException("User already exists.");

            var newUser = new Saas.Infra.Core.User
            {
                Username = username,
                PasswordHash = _passwordHasher.HashPassword(password),
                Email = string.IsNullOrWhiteSpace(displayName) ? $"{username}@local" : displayName,
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(newUser);

            var tokenResponse = _tokenService.GenerateToken(newUser.Username, clientId);

            var refreshHash = ComputeSha256(tokenResponse.RefreshToken);
            var record = new Saas.Infra.Core.RefreshTokenRecord
            {
                UserId = newUser.Id,
                TokenHash = refreshHash,
                ExpiresAt = DateTime.UtcNow.AddDays(JwtConstants.RefreshTokenExpirationDays),
                Revoked = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = newUser.Id
            };
            await _refreshTokenRepository.AddAsync(record);

            Log.Information("User registered: {Username}", username);
            return tokenResponse;
        }

        public async Task<JwtTokenResponse> GenerateTokensAsync(string userId, string password, string clientId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId must not be null or whitespace", nameof(userId));
            if (password is null)
                throw new ArgumentNullException(nameof(password));

            var user = await _userRepository.GetByUsernameAsync(userId);

            if (user == null || !_passwordHasher.VerifyPassword(user.PasswordHash, password))
                throw new InvalidOperationException("Invalid credentials.");

            return _tokenService.GenerateToken(user.Username, clientId);
        }

        public async Task<JwtTokenResponse> RefreshTokenAsync(string refreshToken, string clientId)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentException("Refresh token is required", nameof(refreshToken));

            var hash = ComputeSha256(refreshToken);
            var record = await _refreshTokenRepository.GetByHashAsync(hash);
            if (record == null || record.Revoked || record.ExpiresAt <= DateTime.UtcNow)
                throw new InvalidOperationException("Invalid refresh token.");

            var user = await _userRepository.GetByIdAsync(record.UserId);
            if (user == null) throw new InvalidOperationException("User not found for refresh token.");

            // Revoke old token
            await _refreshTokenRepository.RevokeAsync(hash);

            var newTokenResponse = _tokenService.GenerateToken(user.Username, clientId);

            var newHash = ComputeSha256(newTokenResponse.RefreshToken);
            var newRecord = new RefreshTokenRecord
            {
                UserId = user.Id,
                TokenHash = newHash,
                ExpiresAt = DateTime.UtcNow.AddDays(JwtConstants.RefreshTokenExpirationDays),
                Revoked = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = user.Id
            };

            await _refreshTokenRepository.AddAsync(newRecord);

            return newTokenResponse;
        }

        public async Task RevokeRefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentNullException(nameof(refreshToken));
            var hash = ComputeSha256(refreshToken);
            await _refreshTokenRepository.RevokeAsync(hash);
        }

        public async Task<ClaimsPrincipal> ValidateTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentNullException(nameof(token));

            return await Task.FromResult(_tokenService.ValidateToken(token));
        }

        private static string ComputeSha256(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
