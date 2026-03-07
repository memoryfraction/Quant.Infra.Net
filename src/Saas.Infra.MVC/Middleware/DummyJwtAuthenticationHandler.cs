using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Saas.Infra.MVC.Middleware
{
    /// <summary>
    /// A minimal authentication handler that acts as a placeholder for the "Bearer" scheme.
    /// It does not perform authentication; authentication is handled by CustomJwtMiddleware.
    /// This handler only participates in Challenge/Forbid to produce appropriate HTTP responses.
    /// </summary>
    public class DummyJwtAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public DummyJwtAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Do not claim any identity here. CustomJwtMiddleware will set HttpContext.User.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            // Ensure WWW-Authenticate header for Bearer
            if (!Response.Headers.ContainsKey("WWW-Authenticate"))
            {
                Response.Headers.Append("WWW-Authenticate", "Bearer");
            }
            return Task.CompletedTask;
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
    }
}
