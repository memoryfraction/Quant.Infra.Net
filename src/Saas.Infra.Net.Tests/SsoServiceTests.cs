using System.Security.Claims;
using System.Security.Cryptography;
using Saas.Infra.Core;
using Saas.Infra.SSO;

namespace Saas.Infra.Net.Tests;

/// <summary>
/// SsoService 单元测试。
/// Unit tests for SsoService.
/// </summary>
[TestClass]
public class SsoServiceTests
{
    /// <summary>
    /// 用户不存在时应抛出异常。
    /// Should throw when user does not exist.
    /// </summary>
    [TestMethod]
    public async Task GenerateTokensAsync_ShouldThrow_WhenUserDoesNotExist()
    {
        var userRepository = new FakeUserRepository();
        var sut = CreateSut(userRepository: userRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GenerateTokensAsync("missing@126.com", "123456", "client"));
    }

    /// <summary>
    /// 密码不匹配时应抛出异常。
    /// Should throw when password is invalid.
    /// </summary>
    [TestMethod]
    public async Task GenerateTokensAsync_ShouldThrow_WhenPasswordIsInvalid()
    {
        var userRepository = new FakeUserRepository();
        var hasher = new FakePasswordHasher();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "tester",
            Email = "test@126.com",
            PasswordHash = hasher.HashPassword("correct")
        };
        userRepository.AddUser(user, [RoleCodes.Admin]);

        var sut = CreateSut(userRepository: userRepository, passwordHasher: hasher);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GenerateTokensAsync("test@126.com", "wrong", "client"));
    }

    /// <summary>
    /// 登录成功后应使用邮箱签发令牌并带上角色。
    /// Should issue token with email and role claims after successful login.
    /// </summary>
    [TestMethod]
    public async Task GenerateTokensAsync_ShouldIssueTokenWithEmailAndRoles_WhenCredentialsAreValid()
    {
        var userRepository = new FakeUserRepository();
        var hasher = new FakePasswordHasher();
        var tokenService = new FakeTokenService();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "tester",
            Email = "test@126.com",
            PasswordHash = hasher.HashPassword("correct")
        };
        userRepository.AddUser(user, [RoleCodes.SuperAdmin]);

        var sut = CreateSut(userRepository: userRepository, tokenService: tokenService, passwordHasher: hasher);

        var response = await sut.GenerateTokensAsync("test@126.com", "correct", "client");

        Assert.AreEqual("access-token", response.AccessToken);
        Assert.AreEqual("test@126.com", tokenService.LastEmail);
        Assert.IsTrue(tokenService.LastAdditionalClaims!.Any(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id.ToString()));
        Assert.IsTrue(tokenService.LastAdditionalClaims!.Any(c => c.Type == ClaimTypes.Role && c.Value == RoleCodes.SuperAdmin));
    }

    /// <summary>
    /// 刷新令牌无效时应抛出异常。
    /// Should throw when refresh token is invalid.
    /// </summary>
    [TestMethod]
    public async Task RefreshTokenAsync_ShouldThrow_WhenRefreshTokenIsInvalid()
    {
        var refreshRepo = new FakeRefreshTokenRepository();
        var sut = CreateSut(refreshTokenRepository: refreshRepo);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RefreshTokenAsync("invalid-token", "client"));
    }

    /// <summary>
    /// 验证令牌异常时应返回null。
    /// Should return null when token validation throws.
    /// </summary>
    [TestMethod]
    public async Task ValidateTokenAsync_ShouldReturnNull_WhenTokenValidationThrows()
    {
        var tokenService = new FakeTokenService { ThrowOnValidate = true };
        var sut = CreateSut(tokenService: tokenService);

        var principal = await sut.ValidateTokenAsync("token");

        Assert.IsNull(principal);
    }

    /// <summary>
    /// 创建SsoService测试实例。
    /// Creates SsoService test instance.
    /// </summary>
    /// <param name="userRepository">用户仓储。 / User repository.</param>
    /// <param name="tokenService">令牌服务。 / Token service.</param>
    /// <param name="passwordHasher">密码哈希器。 / Password hasher.</param>
    /// <param name="refreshTokenRepository">刷新令牌仓储。 / Refresh token repository.</param>
    /// <returns>SsoService实例。 / SsoService instance.</returns>
    private static SsoService CreateSut(
        FakeUserRepository? userRepository = null,
        FakeTokenService? tokenService = null,
        FakePasswordHasher? passwordHasher = null,
        FakeRefreshTokenRepository? refreshTokenRepository = null)
    {
        return new SsoService(
            userRepository ?? new FakeUserRepository(),
            tokenService ?? new FakeTokenService(),
            passwordHasher ?? new FakePasswordHasher(),
            refreshTokenRepository ?? new FakeRefreshTokenRepository(),
            RSA.Create());
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly Dictionary<string, User> _usersByEmail = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, IReadOnlyList<string>> _roles = new();

        public void AddUser(User user, IReadOnlyList<string> roles)
        {
            _usersByEmail[user.Email] = user;
            _roles[user.Id] = roles;
        }

        public Task<User?> GetByUsernameAsync(string username)
        {
            var user = _usersByEmail.Values.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(user);
        }

        public Task<User?> GetByEmailAsync(string email)
        {
            _usersByEmail.TryGetValue(email, out var user);
            return Task.FromResult(user);
        }

        public Task<User?> GetByIdAsync(Guid id)
        {
            var user = _usersByEmail.Values.FirstOrDefault(u => u.Id == id);
            return Task.FromResult(user);
        }

        public Task UpdatePasswordAsync(Guid userId, string newPasswordHash)
        {
            var user = _usersByEmail.Values.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                user.PasswordHash = newPasswordHash;
            }

            return Task.CompletedTask;
        }

        public Task AddAsync(User user)
        {
            if (user.Id == Guid.Empty)
            {
                user.Id = Guid.NewGuid();
            }

            _usersByEmail[user.Email] = user;
            _roles[user.Id] = [RoleCodes.User];
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetRoleCodesByUserIdAsync(Guid userId)
        {
            if (_roles.TryGetValue(userId, out var roles))
            {
                return Task.FromResult(roles);
            }

            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }
    }

    private sealed class FakeTokenService : ITokenService
    {
        public bool ThrowOnValidate { get; set; }
        public string? LastEmail { get; private set; }
        public IEnumerable<Claim>? LastAdditionalClaims { get; private set; }

        public JwtTokenResponse GenerateToken(string email, string? clientId = null, IEnumerable<Claim>? additionalClaims = null)
        {
            LastEmail = email;
            LastAdditionalClaims = additionalClaims;
            return new JwtTokenResponse
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                ExpiresIn = 3600
            };
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            if (ThrowOnValidate)
                throw new InvalidOperationException("Validation failed");

            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test@126.com")], "Bearer");
            return new ClaimsPrincipal(identity);
        }
    }

    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public string HashPassword(string password)
        {
            return $"HASH::{password}";
        }

        public bool VerifyPassword(string hashedPassword, string providedPassword)
        {
            return string.Equals(hashedPassword, HashPassword(providedPassword), StringComparison.Ordinal);
        }
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly Dictionary<string, RefreshTokenRecord> _recordsByHash = new(StringComparer.OrdinalIgnoreCase);

        public Task<RefreshTokenRecord?> GetByHashAsync(string tokenHash)
        {
            _recordsByHash.TryGetValue(tokenHash, out var record);
            return Task.FromResult(record);
        }

        public Task AddAsync(RefreshTokenRecord record)
        {
            _recordsByHash[record.TokenHash] = record;
            return Task.CompletedTask;
        }

        public Task RevokeAsync(string tokenHash)
        {
            if (_recordsByHash.TryGetValue(tokenHash, out var record))
            {
                record.Revoked = true;
            }

            return Task.CompletedTask;
        }
    }
}
