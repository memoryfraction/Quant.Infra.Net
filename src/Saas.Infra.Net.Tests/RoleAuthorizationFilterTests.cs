using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Saas.Infra.Core;
using Saas.Infra.MVC.Security;

namespace Saas.Infra.Net.Tests;

/// <summary>
/// 角色授权过滤器单元测试。
/// Unit tests for role authorization filter behavior.
/// </summary>
[TestClass]
public class RoleAuthorizationFilterTests
{
    /// <summary>
    /// 当上下文为null时应抛出异常。
    /// Should throw exception when context is null.
    /// </summary>
    [TestMethod]
    public void OnAuthorization_ShouldThrow_WhenContextIsNull()
    {
        var sut = new RoleAuthorizationFilter(UserRole.User);

        Assert.Throws<ArgumentNullException>(() => sut.OnAuthorization(null!));
    }

    /// <summary>
    /// 当用户未认证时应返回401。
    /// Should return 401 when user is not authenticated.
    /// </summary>
    [TestMethod]
    public void OnAuthorization_ShouldSetUnauthorized_WhenUserIsNotAuthenticated()
    {
        var sut = new RoleAuthorizationFilter(UserRole.User);
        var context = CreateAuthorizationFilterContext(isAuthenticated: false);

        sut.OnAuthorization(context);

        Assert.IsInstanceOfType<UnauthorizedResult>(context.Result);
    }

    /// <summary>
    /// 当角色不足时应返回403。
    /// Should return 403 when user role is insufficient.
    /// </summary>
    [TestMethod]
    public void OnAuthorization_ShouldSetForbid_WhenUserRoleIsInsufficient()
    {
        var sut = new RoleAuthorizationFilter(UserRole.Admin);
        var context = CreateAuthorizationFilterContext(
            isAuthenticated: true,
            roleCodes: [RoleCodes.User]);

        sut.OnAuthorization(context);

        Assert.IsInstanceOfType<ForbidResult>(context.Result);
    }

    /// <summary>
    /// 当角色满足要求时不应设置拒绝结果。
    /// Should not set forbid result when role requirement is satisfied.
    /// </summary>
    [TestMethod]
    public void OnAuthorization_ShouldAllow_WhenUserRoleIsSufficient()
    {
        var sut = new RoleAuthorizationFilter(UserRole.Admin);
        var context = CreateAuthorizationFilterContext(
            isAuthenticated: true,
            roleCodes: [RoleCodes.Admin]);

        sut.OnAuthorization(context);

        Assert.IsNull(context.Result);
    }

    /// <summary>
    /// 应支持小写角色代码并正确授权。
    /// Should support lowercase role code and authorize correctly.
    /// </summary>
    [TestMethod]
    public void OnAuthorization_ShouldAllow_WhenRoleCodeIsLowercase()
    {
        var sut = new RoleAuthorizationFilter(UserRole.Admin);
        var context = CreateAuthorizationFilterContext(
            isAuthenticated: true,
            roleCodes: ["super_admin"]);

        sut.OnAuthorization(context);

        Assert.IsNull(context.Result);
    }

    /// <summary>
    /// 构造特性时传入非法角色应抛出异常。
    /// Should throw when attribute is constructed with invalid role.
    /// </summary>
    [TestMethod]
    public void AuthorizeRoleAttribute_ShouldThrow_WhenRoleIsInvalid()
    {
        var invalidRole = (UserRole)999;

        Assert.Throws<ArgumentOutOfRangeException>(() => new AuthorizeRoleAttribute(invalidRole));
    }

    /// <summary>
    /// 创建授权过滤上下文。
    /// Creates authorization filter context.
    /// </summary>
    /// <param name="isAuthenticated">是否已认证。 / Whether user is authenticated.</param>
    /// <param name="roleCodes">角色代码集合。 / Role code collection.</param>
    /// <returns>授权过滤上下文。 / Authorization filter context.</returns>
    /// <exception cref="ArgumentNullException">当 roleCodes 为null时抛出。 / Thrown when roleCodes is null.</exception>
    private static AuthorizationFilterContext CreateAuthorizationFilterContext(bool isAuthenticated, IEnumerable<string>? roleCodes = null)
    {
        if (roleCodes == null)
            roleCodes = Array.Empty<string>();

        var claims = new List<Claim>();
        foreach (var roleCode in roleCodes)
        {
            claims.Add(new Claim(ClaimTypes.Role, roleCode));
        }

        var identity = isAuthenticated
            ? new ClaimsIdentity(claims, "TestAuth")
            : new ClaimsIdentity();

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }
}
