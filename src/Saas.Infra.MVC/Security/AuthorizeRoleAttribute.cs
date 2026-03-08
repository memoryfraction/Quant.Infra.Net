using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Saas.Infra.Core;

namespace Saas.Infra.MVC.Security
{

    /// <summary>
    /// 基于角色层级的授权特性。
    /// Hierarchical role-based authorization attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class AuthorizeRoleAttribute : TypeFilterAttribute
    {
        /// <summary>
        /// 初始化 <see cref="AuthorizeRoleAttribute"/> 的新实例。
        /// Initializes a new instance of <see cref="AuthorizeRoleAttribute"/>.
        /// </summary>
        /// <param name="minimumRole">最小所需角色。 / Minimum required role.</param>
        public AuthorizeRoleAttribute(UserRole minimumRole)
            : base(typeof(RoleAuthorizationFilter))
        {
            if (!Enum.IsDefined(minimumRole))
                throw new ArgumentOutOfRangeException(nameof(minimumRole));

            Arguments = new object[] { minimumRole };
        }
    }

    /// <summary>
    /// 角色授权过滤器，支持 SUPER_ADMIN > ADMIN > USER 的层级判断。
    /// Role authorization filter supporting hierarchy SUPER_ADMIN > ADMIN > USER.
    /// </summary>
    public sealed class RoleAuthorizationFilter : IAuthorizationFilter
    {
        private readonly UserRole _minimumRole;

        /// <summary>
        /// 初始化 <see cref="RoleAuthorizationFilter"/> 的新实例。
        /// Initializes a new instance of <see cref="RoleAuthorizationFilter"/>.
        /// </summary>
        /// <param name="minimumRole">最小所需角色。 / Minimum required role.</param>
        public RoleAuthorizationFilter(UserRole minimumRole)
        {
            if (!Enum.IsDefined(minimumRole))
                throw new ArgumentOutOfRangeException(nameof(minimumRole));

            _minimumRole = minimumRole;
        }

        /// <summary>
        /// 执行授权校验。
        /// Executes authorization check.
        /// </summary>
        /// <param name="context">授权过滤器上下文。 / Authorization filter context.</param>
        /// <exception cref="ArgumentNullException">当 context 为空时抛出。 / Thrown when context is null.</exception>
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var user = context.HttpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var maxRoleLevel = user.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role && !string.IsNullOrWhiteSpace(c.Value))
                .Select(c => ToRoleLevel(c.Value))
                .DefaultIfEmpty(0)
                .Max();

            if (maxRoleLevel < ToRoleLevel(_minimumRole))
            {
                context.Result = new ForbidResult();
            }
        }

        private static int ToRoleLevel(UserRole role)
        {
            return role switch
            {
                UserRole.Super_Admin => 3,
                UserRole.Admin => 2,
                UserRole.User => 1,
                _ => 0
            };
        }

        private static int ToRoleLevel(string roleCode)
        {
            if (string.IsNullOrWhiteSpace(roleCode))
                return 0;

            var normalized = roleCode.Trim().ToUpperInvariant();
            return normalized switch
            {
                RoleCodes.SuperAdmin => ToRoleLevel(UserRole.Super_Admin),
                RoleCodes.Admin => ToRoleLevel(UserRole.Admin),
                RoleCodes.User => ToRoleLevel(UserRole.User),
                _ => 0
            };
        }
    }
}

