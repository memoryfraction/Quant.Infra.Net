using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Saas.Infra.Core;
using Serilog.Events;

namespace Saas.Infra.MVC.Services.Blazor
{
    /// <summary>
    /// Blazor circuit-scoped JWT token state service.
    /// Manages the current user's JWT token and claims for the duration of a Blazor Server circuit.
    /// </summary>
    public sealed class BlazorTokenService
    {
        private string? _accessToken;
        private List<Claim> _claims = new();

        /// <summary>Gets the current JWT access token.</summary>
        public string? AccessToken => _accessToken;

        /// <summary>Gets whether the current circuit has an authenticated token.</summary>
        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

        /// <summary>Gets the display name of the authenticated user.</summary>
        public string? UserName =>
            _claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == "unique_name" || c.Type == "name")?.Value;

        /// <summary>Gets the email of the authenticated user.</summary>
        public string? Email =>
            _claims.FirstOrDefault(c => c.Type == ClaimTypes.Email || c.Type == "email")?.Value;

        /// <summary>Raised when authentication state changes (login / logout).</summary>
        public event Action? OnChange;

        /// <summary>
        /// Sets the JWT access token and parses its claims.
        /// </summary>
        /// <param name="token">The JWT bearer token.</param>
        /// <exception cref="ArgumentNullException">Thrown when token is null or whitespace.</exception>
        public void SetToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentNullException(nameof(token));

            _accessToken = token;
            _claims = new List<Claim>();

            try
            {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(token))
                {
                    var jwt = handler.ReadJwtToken(token);
                    _claims.AddRange(jwt.Claims);

                    var name = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == "name")?.Value;
                    var email = jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email || c.Type == "email")?.Value;
                    var roles = string.Join(',', jwt.Claims.Where(c => c.Type == ClaimTypes.Role || c.Type == "role").Select(c => c.Value));

                    UtilityService.LogAndWriteLine(LogEventLevel.Information,
                        "[BLAZOR TOKEN] Token set. Name={Name}, Email={Email}, Roles={Roles}, ClaimsCount={Count}",
                        name ?? "(none)",
                        email ?? "(none)",
                        roles,
                        _claims.Count);
                }
            }
            catch
            {
                // If parsing fails, keep empty claims but store the token
            }

            NotifyStateChanged();
        }

        /// <summary>Clears the current token and all claims.</summary>
        public void ClearToken()
        {
            _accessToken = null;
            _claims = new List<Claim>();
            UtilityService.LogAndWriteLine("[BLAZOR TOKEN] Token cleared.", LogEventLevel.Information);
            NotifyStateChanged();
        }

        /// <summary>Returns all parsed claims from the current token.</summary>
        /// <returns>Read-only list of claims.</returns>
        public IReadOnlyList<Claim> GetClaims() => _claims.AsReadOnly();

        /// <summary>
        /// Checks whether the authenticated user has the specified role.
        /// </summary>
        /// <param name="role">Role name to check (e.g., "ADMIN", "SUPER_ADMIN").</param>
        /// <returns><c>true</c> if the user has the role; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when role is null or whitespace.</exception>
        public bool IsInRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
                throw new ArgumentNullException(nameof(role));

            return _claims.Any(c =>
                (c.Type == ClaimTypes.Role || c.Type == "role") &&
                c.Value.Equals(role, StringComparison.OrdinalIgnoreCase));
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
