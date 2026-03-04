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
        private readonly IPasswordHasher _passwordHasher; // 新增：注入密码哈希服务
        private readonly IRefreshTokenRepository _refreshTokenRepository;

        public SsoService(
            IUserRepository userRepository,
            ITokenService tokenService,
            IPasswordHasher passwordHasher,
            IRefreshTokenRepository refreshTokenRepository) // 构造函数注入
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _refreshTokenRepository = refreshTokenRepository ?? throw new ArgumentNullException(nameof(refreshTokenRepository));
        }

        /// <summary>
        /// 注册新用户并返回自动登录的令牌对。
        /// Registers a new user and returns authentication tokens for immediate login.
        /// </summary>
        /// <param name="username">要注册的用户名，不能为空或空白。/ The username to register; cannot be null or whitespace.</param>
        /// <param name="password">用于注册的明文密码，不能为空或空白。/ The plaintext password for registration; cannot be null or whitespace.</param>
        /// <param name="displayName">可选的显示名称。/ Optional display name.</param>
        /// <param name="clientId">可选的客户端标识。/ Optional client identifier.</param>
        /// <returns>返回包含访问令牌与刷新令牌的 <see cref="JwtTokenResponse"/>。/ Returns a <see cref="JwtTokenResponse"/> containing access and refresh tokens.</returns>
        /// <exception cref="ArgumentException">当 <paramref name="username"/> 或 <paramref name="password"/> 为空或仅包含空白字符时抛出。/ Thrown when username or password is null or whitespace.</exception>
        public async Task<JwtTokenResponse> RegisterUserAsync(string username, string password, string? displayName = null, string? clientId = null)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("username must not be null or whitespace", nameof(username));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("password must not be null or whitespace", nameof(password));

            // check existing
            var existing = await _userRepository.GetByUsernameAsync(username);
            if (existing != null)
                throw new InvalidOperationException("User already exists.");

            // create user DTO
            var newUser = new Saas.Infra.Core.User
            {
                Username = username,
                PasswordHash = _passwordHasher.HashPassword(password),
                DisplayName = displayName,
                Email = string.IsNullOrWhiteSpace(displayName) ? $"{username}@local" : displayName,
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(newUser);

            // generate tokens
            var tokenResponse = _tokenService.GenerateToken(newUser.Username, clientId);

            // store refresh token hash
            var refreshHash = ComputeSha256(tokenResponse.RefreshToken);
            var record = new Saas.Infra.Core.RefreshTokenRecord
            {
                UserId = newUser.Id,
                TokenHash = refreshHash,
                ExpiresAt = DateTime.UtcNow.AddDays(JwtConstants.RefreshTokenExpirationDays),
                Revoked = false,
                CreatedAt = DateTime.UtcNow
            };
            await _refreshTokenRepository.AddAsync(record);

            Log.Information("User registered: {Username}", username);
            return tokenResponse;
        }

        /// <summary>
        /// 为指定用户校验凭据并生成访问令牌与刷新令牌。
        /// Validates credentials for the specified user and generates access and refresh tokens.
        /// </summary>
        /// <param name="userId">用户名，不能为空或空白。/ The username; cannot be null or whitespace.</param>
        /// <param name="password">明文密码，用于验证。/ The plaintext password used for verification.</param>
        /// <param name="clientId">客户端标识。/ The client identifier.</param>
        /// <returns>返回包含访问令牌与刷新令牌的 <see cref="JwtTokenResponse"/>。/ Returns a <see cref="JwtTokenResponse"/> containing access and refresh tokens.</returns>
        /// <exception cref="ArgumentException">当 <paramref name="userId"/> 为空或仅包含空白时抛出。/ Thrown when userId is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">当 <paramref name="password"/> 为 null 时抛出。/ Thrown when password is null.</exception>
        public async Task<JwtTokenResponse> GenerateTokensAsync(string userId, string password, string clientId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId must not be null or whitespace", nameof(userId));
            if (password is null)
                throw new ArgumentNullException(nameof(password));

            // 1. 获取用户信息
            var user = await _userRepository.GetByUsernameAsync(userId);

            // 2. 验证密码
            // 安全优化：无论 user 是否为 null，逻辑上都应返回统一的“凭据无效”，防止用户名探测
            if (user == null || !_passwordHasher.VerifyPassword(user.PasswordHash, password))
            {
                // 统一报错信息
                throw new InvalidOperationException("Invalid credentials.");
            }

            // 3. 委托给 Token 服务生成结果
            // 注意：如果 ITokenService.GenerateToken 是异步的，请加上 await
            return _tokenService.GenerateToken(user.Username, clientId);
        }

        /// <summary>
        /// 使用有效的刷新令牌生成新的访问令牌与刷新令牌（刷新令牌旋转）。
        /// Uses a valid refresh token to generate a new access token and refresh token (rotating the refresh token).
        /// </summary>
        /// <param name="refreshToken">刷新令牌字符串，不能为空或空白。/ The refresh token string; cannot be null or whitespace.</param>
        /// <param name="clientId">客户端标识。/ The client identifier.</param>
        /// <returns>返回新的 <see cref="JwtTokenResponse"/>。/ Returns a new <see cref="JwtTokenResponse"/>.</returns>
        /// <exception cref="ArgumentException">当 <paramref name="refreshToken"/> 为空或仅包含空白时抛出。/ Thrown when refreshToken is null or whitespace.</exception>
        public async Task<JwtTokenResponse> RefreshTokenAsync(string refreshToken, string clientId)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentException("Refresh token is required", nameof(refreshToken));
            // compute hash
            var hash = ComputeSha256(refreshToken);

            var record = await _refreshTokenRepository.GetByHashAsync(hash);
            if (record == null || record.Revoked || record.ExpiresAt <= DateTime.UtcNow)
                throw new InvalidOperationException("Invalid refresh token.");

            // get user by id
            var user = await _userRepository.GetByIdAsync(record.UserId);
            if (user == null) throw new InvalidOperationException("User not found for refresh token.");

            // generate new tokens (tokenService returns raw refresh token which we will hash & store)
            var newTokenResponse = _tokenService.GenerateToken(user.Username, clientId);

            // store new refresh token and replace old
            var newHash = ComputeSha256(newTokenResponse.RefreshToken);
            var newRecord = new RefreshTokenRecord
            {
                UserId = user.Id,
                TokenHash = newHash,
                ExpiresAt = DateTime.UtcNow.AddDays(JwtConstants.RefreshTokenExpirationDays),
                Revoked = false,
                CreatedAt = DateTime.UtcNow
            };

            await _refreshTokenRepository.ReplaceAsync(hash, newRecord);

            return newTokenResponse;
        }

        /// <summary>
        /// 撤销指定的刷新令牌（登出操作的一部分）。
        /// Revokes the specified refresh token (part of logout flow).
        /// </summary>
        /// <param name="refreshToken">要撤销的刷新令牌字符串，不能为空或空白。/ The refresh token string to revoke; cannot be null or whitespace.</param>
        public async Task RevokeRefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentNullException(nameof(refreshToken));
            var hash = ComputeSha256(refreshToken);
            await _refreshTokenRepository.RevokeAsync(hash);
        }

        /// <summary>
        /// 验证给定的 JWT 令牌并返回 <see cref="ClaimsPrincipal"/>（如果令牌有效）。
        /// Validates the provided JWT token and returns a <see cref="ClaimsPrincipal"/> if the token is valid.
        /// </summary>
        /// <param name="token">要验证的 JWT 令牌字符串，不能为空或空白。/ The JWT token string to validate; cannot be null or whitespace.</param>
        /// <returns>令牌有效时返回 <see cref="ClaimsPrincipal"/>；否则抛出或返回 null（取决于实现）。/ Returns a <see cref="ClaimsPrincipal"/> when valid; otherwise returns null or throws.</returns>
        public async Task<ClaimsPrincipal> ValidateTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentNullException(nameof(token));

            // 建议在 ITokenService 中定义此逻辑
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