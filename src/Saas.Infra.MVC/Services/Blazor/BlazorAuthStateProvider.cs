using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Components.Authorization;
using Saas.Infra.SSO;

namespace Saas.Infra.MVC.Services.Blazor
{
    /// <summary>
    /// Custom Blazor <see cref="AuthenticationStateProvider"/> backed by <see cref="BlazorTokenService"/>.
    /// Normalises JWT role claims so that <c>[Authorize(Roles=...)]</c> and <see cref="Microsoft.AspNetCore.Components.Authorization.AuthorizeView"/> work correctly.
    /// </summary>
    public sealed class BlazorAuthStateProvider : AuthenticationStateProvider
    {
        private readonly BlazorTokenService _tokenService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of <see cref="BlazorAuthStateProvider"/>.
        /// </summary>
        /// <param name="tokenService">The circuit-scoped token service.</param>
        /// <param name="httpContextAccessor">HTTP context accessor used to hydrate auth state from the current request.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when dependencies are null.</exception>
        public BlazorAuthStateProvider(BlazorTokenService tokenService, IHttpContextAccessor httpContextAccessor)
        {
            _tokenService = tokenService ?? throw new System.ArgumentNullException(nameof(tokenService));
            _httpContextAccessor = httpContextAccessor ?? throw new System.ArgumentNullException(nameof(httpContextAccessor));
            _tokenService.OnChange += OnTokenChanged;
        }

        /// <inheritdoc />
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            TryHydrateFromHttpContext();

            var isAuthenticated = _tokenService.IsAuthenticated;
            var rawClaims = _tokenService.GetClaims();

            Serilog.Log.Information(
                "[BLAZOR AUTH] GetAuthenticationStateAsync called. IsAuthenticated={IsAuth}, ClaimsCount={ClaimsCount}",
                isAuthenticated,
                rawClaims?.Count ?? 0);

            if (!isAuthenticated)
            {
                var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
                Serilog.Log.Information("[BLAZOR AUTH] Returning anonymous AuthenticationState.");
                return Task.FromResult(new AuthenticationState(anonymous));
            }

            // Normalise role claims so they always live under ClaimTypes.Role for [Authorize(Roles=...)]
            var otherClaims = rawClaims.Where(c => c.Type != "role" && c.Type != ClaimTypes.Role);
            var roleClaims = rawClaims
                .Where(c => c.Type == "role" || c.Type == ClaimTypes.Role)
                .Select(c => new Claim(ClaimTypes.Role, TokenService.NormalizeRoleCode(c.Value)));

            var identity = new ClaimsIdentity(otherClaims.Concat(roleClaims), "jwt");
            var user = new ClaimsPrincipal(identity);

            Serilog.Log.Information(
                "[BLAZOR AUTH] Returning authenticated AuthenticationState. Name={Name}, Roles={Roles}",
                user.Identity?.Name ?? "(none)",
                string.Join(',', user.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value)));

            return Task.FromResult(new AuthenticationState(user));
        }

        private void TryHydrateFromHttpContext()
        {
            if (_tokenService.IsAuthenticated)
            {
                return;
            }

            try
            {
                var context = _httpContextAccessor.HttpContext;
                var cookieToken = context?.Request.Cookies["AccessToken"];
                if (!string.IsNullOrWhiteSpace(cookieToken))
                {
                    Serilog.Log.Information("[BLAZOR AUTH] Hydrating token service from HttpContext cookie.");
                    _tokenService.SetToken(cookieToken);
                    return;
                }

                if (context?.User?.Identity?.IsAuthenticated == true)
                {
                    Serilog.Log.Information(
                        "[BLAZOR AUTH] HttpContext user is authenticated but no access token cookie was found. Name={Name}",
                        context.User.Identity?.Name ?? "(none)");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[BLAZOR AUTH] Failed to hydrate token service from HttpContext.");
            }
        }

        private void OnTokenChanged() =>
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
