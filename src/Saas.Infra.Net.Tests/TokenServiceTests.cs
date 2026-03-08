using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Saas.Infra.Core;
using Saas.Infra.SSO;

namespace Saas.Infra.Net.Tests;

/// <summary>
/// TokenService 单元测试。
/// Unit tests for TokenService.
/// </summary>
[TestClass]
public class TokenServiceTests
{
    /// <summary>
    /// 当邮箱无效时应抛出参数异常。
    /// Should throw argument exception when email is invalid.
    /// </summary>
    [TestMethod]
    public void GenerateToken_ShouldThrow_WhenEmailIsInvalid()
    {
        var sut = CreateSut();

        Assert.Throws<ArgumentException>(() => sut.GenerateToken(" "));
    }

    /// <summary>
    /// 无额外角色时应注入默认用户角色代码。
    /// Should inject default user role code when no additional role exists.
    /// </summary>
    [TestMethod]
    public void GenerateToken_ShouldSetDefaultUserRole_WhenNoRoleClaimProvided()
    {
        var sut = CreateSut();

        var response = sut.GenerateToken("test@126.com");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);

        var role = jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value;
        Assert.AreEqual(RoleCodes.User, role);
    }

    /// <summary>
    /// 角色代码为SUPER_ADMIN时应保留系统角色代码格式。
    /// Should keep the SUPER_ADMIN role in canonical role code format.
    /// </summary>
    [TestMethod]
    public void GenerateToken_ShouldMapRoleCode_WhenAdditionalRoleClaimProvided()
    {
        var sut = CreateSut();
        var claims = new[] { new Claim(ClaimTypes.Role, RoleCodes.SuperAdmin) };

        var response = sut.GenerateToken("test@126.com", "client", claims);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);

        var role = jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value;
        Assert.AreEqual(RoleCodes.SuperAdmin, role);
    }

    /// <summary>
    /// 使用生成的令牌应能成功验证。
    /// Should validate successfully with generated token.
    /// </summary>
    [TestMethod]
    public void ValidateToken_ShouldReturnPrincipal_WhenTokenIsValid()
    {
        var sut = CreateSut();
        var response = sut.GenerateToken("test@126.com");

        var principal = sut.ValidateToken(response.AccessToken);

        Assert.IsNotNull(principal);
        Assert.AreEqual("test@126.com", principal.Identity?.Name);
    }

    /// <summary>
    /// 被篡改令牌应验证失败并返回null。
    /// Should return null when token is tampered.
    /// </summary>
    [TestMethod]
    public void ValidateToken_ShouldReturnNull_WhenTokenIsTampered()
    {
        var sut = CreateSut();
        var response = sut.GenerateToken("test@126.com");
        var tamperedToken = response.AccessToken + "tamper";

        var principal = sut.ValidateToken(tamperedToken);

        Assert.IsNull(principal);
    }

    /// <summary>
    /// 创建TokenService测试实例。
    /// Creates TokenService test instance.
    /// </summary>
    /// <returns>TokenService实例。 / TokenService instance.</returns>
    private static TokenService CreateSut()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = Saas.Infra.Core.JwtConstants.Issuer,
            Audience = "Saas.Infra.Clients",
            AccessTokenExpirationMinutes = 60
        });

        var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "test-kid" };

        return new TokenService(options, key);
    }
}
