using Saas.Infra.Core;
using Serilog;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Saas.Infra.Services.Sso
{
    /// <summary>
    /// 单点登录服务实现，负责用户认证、JWT 令牌生成、刷新与撤销。
    /// SSO service implementation responsible for user authentication, JWT issuance, refresh, and revocation.
    /// </summary>
    public class SsoService : ISsoService
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IRefreshTokenRepository _refreshTokenRepository;

        /// <summary>
        /// 初始化 <see cref="SsoService"/> 的新实例。
        /// Initializes a new instance of <see cref="SsoService"/>.
        /// </summary>
        /// <param name="userRepository">用户仓储。 / User repository.</param>
        /// <param name="tokenService">令牌服务。 / Token service.</param>
        /// <param name="passwordHasher">密码哈希器。 / Password hasher.</param>
        /// <param name="refreshTokenRepository">刷新令牌仓储。 / Refresh token repository.</param>
        /// <exception cref="ArgumentNullException">当参数为 null 时抛出。 / Thrown when arguments are null.</exception>
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

        /// <summary>
        /// 注册新用户并返回登录令牌（自动登录）。
        /// Registers a new user and returns authentication tokens (auto-login).
        /// </summary>
        /// <param name="email">用户电子邮件地址。 / User email address.</param>
        /// <param name="password">明文密码。 / Plain text password.</param>
        /// <param name="username">可选用户名。 / Optional username.</param>
        /// <param name="clientId">可选客户端标识。 / Optional client identifier.</param>
        /// <returns>JWT 令牌响应。 / JWT token response.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when parameters are invalid.</exception>
        /// <exception cref="InvalidOperationException">当邮箱或用户名已存在时抛出。 / Thrown when email or username already exists.</exception>
        public async Task<JwtTokenResponse> RegisterUserAsync(string email, string password, string? username = null, string? clientId = null)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be null or whitespace", nameof(email));
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password cannot be null or whitespace", nameof(password));

            var existing = await _userRepository.GetByEmailAsync(email);
            if (existing != null)
                throw new InvalidOperationException("User with this email already exists.");

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

            var newUser = new User
            {
                Username = username,
                PasswordHash = _passwordHasher.HashPassword(password),
                Email = email,
                Status = UserStatus.Enabled,
                CreatedTime = DateTime.UtcNow
            };

            await _userRepository.AddAsync(newUser);

            var roleCodes = await _userRepository.GetRoleCodesByUserIdAsync(newUser.Id);
            var claims = BuildTokenClaims(newUser.Id, roleCodes);

            var tokenResponse = _tokenService.GenerateToken(newUser.Email, clientId, claims);

            var refreshHash = ComputeSha256(tokenResponse.RefreshToken);
            var record = new RefreshTokenRecord
            {
                UserId = newUser.Id,
                TokenHash = refreshHash,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(JwtConstants.RefreshTokenExpirationDays),
                Revoked = false,
                CreatedTime = DateTimeOffset.UtcNow,
                CreatedBy = newUser.Id
            };

            await _refreshTokenRepository.AddAsync(record);

            Log.Information("User registered with email {Email}", email);
            return tokenResponse;
        }

        /// <summary>
        /// 处理用户登录并生成 JWT 令牌。
        /// Handles user login and generates JWT tokens.
        /// </summary>
        /// <param name="email">用户电子邮件地址。 / User email address.</param>
        /// <param name="password">用户密码。 / User password.</param>
        /// <param name="clientId">客户端标识。 / Client identifier.</param>
        /// <returns>JWT 令牌响应。 / JWT token response.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when parameters are invalid.</exception>
        /// <exception cref="InvalidOperationException">当用户不存在或密码错误时抛出。 / Thrown when user does not exist or password is incorrect.</exception>
        public async Task<JwtTokenResponse> GenerateTokensAsync(string email, string password, string clientId)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be null or whitespace", nameof(email));
            if (password is null)
                throw new ArgumentNullException(nameof(password));
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentException("ClientId cannot be null or whitespace", nameof(clientId));

            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                Log.Warning("Login failed for email {Email}: user not found", email);
                throw new InvalidOperationException("User does not exist.");
            }

            if (!_passwordHasher.VerifyPassword(user.PasswordHash, password))
            {
                Log.Warning("Login failed for email {Email}: incorrect password", email);
                throw new InvalidOperationException("Incorrect password.");
            }

            var roleCodes = await _userRepository.GetRoleCodesByUserIdAsync(user.Id);
            var claims = BuildTokenClaims(user.Id, roleCodes);

            var tokenResponse = _tokenService.GenerateToken(user.Email, clientId, claims);
            Log.Information("RSA-signed token generated for email {Email}", user.Email);

            return tokenResponse;
        }

        /// <summary>
        /// 刷新令牌续期（吊销旧刷新令牌并生成新令牌）。
        /// Refreshes tokens by revoking the old refresh token and issuing a new one.
        /// </summary>
        /// <param name="refreshToken">刷新令牌。 / Refresh token.</param>
        /// <param name="clientId">客户端标识。 / Client identifier.</param>
        /// <returns>JWT 令牌响应。 / JWT token response.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when parameters are invalid.</exception>
        /// <exception cref="InvalidOperationException">当刷新令牌无效或用户不存在时抛出。 / Thrown when refresh token is invalid or user does not exist.</exception>
        public async Task<JwtTokenResponse> RefreshTokenAsync(string refreshToken, string clientId)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentException("Refresh token is required", nameof(refreshToken));
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentException("ClientId cannot be null or whitespace", nameof(clientId));

            var hash = ComputeSha256(refreshToken);
            var record = await _refreshTokenRepository.GetByHashAsync(hash);

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

            await _refreshTokenRepository.RevokeAsync(hash);
            Log.Information("Old refresh token revoked for user {UserId}", user.Id);

            var roleCodes = await _userRepository.GetRoleCodesByUserIdAsync(user.Id);
            var claims = BuildTokenClaims(user.Id, roleCodes);

            var newTokenResponse = _tokenService.GenerateToken(user.Email, clientId, claims);

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
            Log.Information("New refresh token issued for user {UserId}", user.Id);

            return newTokenResponse;
        }

        /// <summary>
        /// 撤销刷新令牌（登出）。
        /// Revokes the refresh token (logout).
        /// </summary>
        /// <param name="refreshToken">刷新令牌。 / Refresh token.</param>
        /// <returns>表示操作完成的任务。 / Task representing completion.</returns>
        /// <exception cref="ArgumentException">当 refreshToken 为空或空白时抛出。 / Thrown when refreshToken is null or whitespace.</exception>
        public async Task RevokeRefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                throw new ArgumentException("Refresh token is required", nameof(refreshToken));

            var hash = ComputeSha256(refreshToken);
            await _refreshTokenRepository.RevokeAsync(hash);
            Log.Information("Refresh token revoked (hash: {Hash})", hash);
        }

        /// <summary>
        /// 验证令牌合法性。
        /// Validates token legitimacy.
        /// </summary>
        /// <param name="token">待验证的令牌。 / Token to validate.</param>
        /// <returns>验证通过返回 ClaimsPrincipal，否则返回 null。 / ClaimsPrincipal if valid; otherwise null.</returns>
        /// <exception cref="ArgumentException">当 token 为空或空白时抛出。 / Thrown when token is null or whitespace.</exception>
        public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Token cannot be null or whitespace", nameof(token));

            try
            {
                var principal = _tokenService.ValidateToken(token);
                Log.Information("Token validated successfully (user: {Username})", principal?.Identity?.Name ?? "unknown");
                return await Task.FromResult(principal);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to validate token");
                return await Task.FromResult<ClaimsPrincipal?>(null);
            }
        }

        private static string ComputeSha256(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                throw new ArgumentException("Input cannot be null or whitespace", nameof(input));

            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = sha.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static IEnumerable<Claim> BuildTokenClaims(Guid userId, IReadOnlyList<string> roleCodes)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("Invalid userId", nameof(userId));
            if (roleCodes is null)
                throw new ArgumentNullException(nameof(roleCodes));

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString())
            };

            foreach (var role in roleCodes.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                claims.Add(new Claim(ClaimTypes.Role, TokenService.NormalizeRoleCode(role)));
            }

            return claims;
        }

        private static string GenerateUsername()
        {
            var buffer = RandomNumberGenerator.GetBytes(10);
            var base64 = Convert.ToBase64String(buffer);
            var sanitized = base64.Replace("+", string.Empty).Replace("/", string.Empty).Replace("=", string.Empty);
            return $"user_{sanitized[..8].ToLowerInvariant()}";
        }
    }
}
