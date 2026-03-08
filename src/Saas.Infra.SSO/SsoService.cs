using Saas.Infra.Core;
using Serilog;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Saas.Infra.SSO
{
    /// <summary>
    /// 单点登录服务的实现，负责用户认证、RSA签名JWT令牌生成、刷新与撤销等操作。
    /// Implementation of SSO service responsible for user authentication, RSA-signed JWT token issuance, refresh and revocation operations.
    /// </summary>
    public class SsoService : ISsoService
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly RSA _rsa; // 注入RSA实例，供TokenService生成RSA签名

        public SsoService(
            IUserRepository userRepository,
            ITokenService tokenService,
            IPasswordHasher passwordHasher,
            IRefreshTokenRepository refreshTokenRepository,
            RSA rsa) // 新增：注入RSA实例
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
            _rsa = rsa ?? throw new ArgumentNullException(nameof(rsa), "RSA instance is required for JWT signing");
        }

        /// <summary>
        /// 注册新用户并生成RSA签名的JWT令牌
        /// Register new user and generate RSA-signed JWT tokens
        /// </summary>
        public async Task<JwtTokenResponse> RegisterUserAsync(string email, string password, string? username = null, string? clientId = null)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("email must not be null or whitespace", nameof(email));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("password must not be null or whitespace", nameof(password));

            // 检查邮箱是否已存在
            var existing = await _userRepository.GetByEmailAsync(email);
            if (existing != null)
                throw new InvalidOperationException("User with this email already exists.");

            // 自动生成唯一用户名
            if (string.IsNullOrWhiteSpace(username))
            {
                username = GenerateUsername();
                while (await _userRepository.GetByUsernameAsync(username) != null)
                {
                    username = GenerateUsername();
                }
            }
            else
            {
                var existingByUsername = await _userRepository.GetByUsernameAsync(username);
                if (existingByUsername != null)
                    throw new InvalidOperationException("Username already exists.");
            }

            // 创建新用户
            var newUser = new Saas.Infra.Core.User
            {
                Username = username,
                PasswordHash = _passwordHasher.HashPassword(password),
                Email = email,
                CreatedTime = DateTime.UtcNow
            };

            await _userRepository.AddAsync(newUser);

            // 生成RSA签名的JWT令牌（TokenService已适配RSA）
            var tokenResponse = _tokenService.GenerateToken(newUser.Username, clientId);

            // 保存刷新令牌（哈希存储，避免明文）
            var refreshHash = ComputeSha256(tokenResponse.RefreshToken);
            var record = new Saas.Infra.Core.RefreshTokenRecord
            {
                UserId = newUser.Id,
                TokenHash = refreshHash,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(JwtConstants.RefreshTokenExpirationDays),
                Revoked = false,
                CreatedTime = DateTimeOffset.UtcNow,
                CreatedBy = newUser.Id
            };
            await _refreshTokenRepository.AddAsync(record);

            Log.Information("User registered with email: {Email}, RSA token generated successfully", email);
            return tokenResponse;
        }

        /// <summary>
        /// 验证用户凭据并生成RSA签名的JWT令牌
        /// </summary>
        /// 验证用户凭据并生成RSA签名的JWT令牌
        /// Validate user credentials and generate RSA-signed JWT tokens
        /// </summary>
        /// <param name="email">邮箱地址。 / Email address.</param>
        /// <param name="password">密码。 / Password.</param>
        /// <param name="clientId">客户端ID。 / Client ID.</param>
        /// <returns>JWT令牌响应的任务。 / Task containing JWT token response.</returns>
        /// <exception cref="ArgumentException">当email为null或空白时抛出。 / Thrown when email is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">当password为null时抛出。 / Thrown when password is null.</exception>
        /// <exception cref="InvalidOperationException">当用户不存在或密码错误时抛出。 / Thrown when user does not exist or password is incorrect.</exception>
        public async Task<JwtTokenResponse> GenerateTokensAsync(string email, string password, string clientId)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("email must not be null or whitespace", nameof(email));
            if (password is null)
                throw new ArgumentNullException(nameof(password));

            var user = await _userRepository.GetByEmailAsync(email);

            // 用户不存在
            if (user == null)
            {
                Log.Warning("Login failed for email: {Email} (user does not exist)", email);
                throw new InvalidOperationException("User does not exist.");
            }

            // 密码错误
            if (!_passwordHasher.VerifyPassword(user.PasswordHash, password))
            {
                Log.Warning("Login failed for email: {Email} (incorrect password)", email);
                throw new InvalidOperationException("Incorrect password.");
            }

            // 生成RSA签名的Token
            var tokenResponse = _tokenService.GenerateToken(user.Username, clientId);
            Log.Information("RSA-signed token generated for user: {Username} (Email: {Email})", user.Username, email);

            return tokenResponse;
        }

        /// <summary>
        /// 刷新RSA签名的JWT令牌（吊销旧令牌，生成新令牌）
        /// Refresh RSA-signed JWT token (revoke old token, generate new one)
        /// </summary>
        public async Task<JwtTokenResponse> RefreshTokenAsync(string refreshToken, string clientId)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentException("Refresh token is required", nameof(refreshToken));

            var hash = ComputeSha256(refreshToken);
            var record = await _refreshTokenRepository.GetByHashAsync(hash);

            // 验证刷新令牌有效性
            if (record == null || record.Revoked || record.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                Log.Warning("Invalid refresh token (hash: {Hash})", hash);
                throw new InvalidOperationException("Invalid refresh token.");
            }

            var user = await _userRepository.GetByIdAsync(record.UserId);
            if (user == null)
            {
                Log.Error("User not found for refresh token (UserId: {UserId})", record.UserId);
                throw new InvalidOperationException("User not found for refresh token.");
            }

            // 吊销旧刷新令牌
            await _refreshTokenRepository.RevokeAsync(hash);
            Log.Information("Old refresh token revoked for user: {Username} (UserId: {UserId})", user.Username, user.Id);

            // 生成新的RSA签名令牌
            var newTokenResponse = _tokenService.GenerateToken(user.Username, clientId);

            // 保存新刷新令牌
            var newHash = ComputeSha256(newTokenResponse.RefreshToken);
            var newRecord = new RefreshTokenRecord
            {
                UserId = user.Id,
                TokenHash = newHash,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(JwtConstants.RefreshTokenExpirationDays),
                Revoked = false,
                CreatedTime = DateTimeOffset.UtcNow,
                CreatedBy = user.Id
            };

            await _refreshTokenRepository.AddAsync(newRecord);
            Log.Information("New RSA-signed token generated for user: {Username} (refresh token refreshed)", user.Username);

            return newTokenResponse;
        }

        /// <summary>
        /// 吊销刷新令牌
        /// Revoke refresh token
        /// </summary>
        public async Task RevokeRefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentNullException(nameof(refreshToken));

            var hash = ComputeSha256(refreshToken);
            await _refreshTokenRepository.RevokeAsync(hash);
            Log.Information("Refresh token revoked (hash: {Hash})", hash);
        }

        /// <summary>
        /// 验证RSA签名的JWT令牌有效性
        /// Validate RSA-signed JWT token
        /// </summary>
        public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentNullException(nameof(token));

            try
            {
                var principal = _tokenService.ValidateToken(token);
                Log.Information("RSA-signed token validated successfully (user: {Username})",
                    principal?.Identity?.Name ?? "unknown");
                return await Task.FromResult(principal);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to validate RSA-signed token");
                return await Task.FromResult<ClaimsPrincipal?>(null);
            }
        }

        /// <summary>
        /// SHA256哈希计算（用于刷新令牌存储）
        /// Compute SHA256 hash (for refresh token storage)
        /// </summary>
        private static string ComputeSha256(string input)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        /// <summary>
        /// 生成随机用户名
        /// Generates a random username
        /// </summary>
        private static string GenerateUsername()
        {
            return $"user_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }
    }
}