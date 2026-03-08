using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Saas.Infra.MVC.Middleware
{
    /// <summary>
    /// 自定义JWT验证中间件，使用固定的RSA安全密钥验证传入的Bearer令牌并绕过令牌头部的kid匹配。
    /// Custom JWT validation middleware that validates incoming Bearer tokens using a fixed RSA security key and bypasses token header kid matching.
    /// </summary>
    public class CustomJwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly RsaSecurityKey _validationKey;

        /// <summary>
        /// 初始化<see cref="CustomJwtMiddleware"/>的新实例。
        /// Initializes a new instance of the <see cref="CustomJwtMiddleware"/> class.
        /// </summary>
        /// <param name="next">管道中的下一个中间件。 / The next middleware in the pipeline.</param>
        /// <param name="configuration">应用程序配置。 / Application configuration.</param>
        /// <param name="validationKey">用于验证传入令牌的RSA密钥。 / The RSA key used to validate incoming tokens.</param>
        /// <exception cref="ArgumentNullException">当必需的参数为null时抛出。 / Thrown when required parameters are null.</exception>
        public CustomJwtMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            RsaSecurityKey validationKey)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _validationKey = validationKey ?? throw new ArgumentNullException(nameof(validationKey));
        }

        /// <summary>
        /// 调用中间件以验证JWT并设置HttpContext.User主体。
        /// Invokes the middleware to validate JWT and set the HttpContext.User principal.
        /// </summary>
        /// <param name="context">当前请求的HTTP上下文。 / HTTP context for the current request.</param>
        /// <returns>表示请求处理完成的任务。 / A task that represents the completion of request processing.</returns>
        /// <exception cref="ArgumentNullException">当context为null时抛出。 / Thrown when context is null.</exception>
        public async Task InvokeAsync(HttpContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            // Prefer Authorization header (API calls from Blazor),
            // but also fall back to the AccessToken cookie for initial
            // document requests (e.g., GET /dashboard after login).
            string? token = null;

            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) &&
                authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader["Bearer ".Length..].Trim();
            }
            else if (context.Request.Cookies.TryGetValue("AccessToken", out var cookieToken))
            {
                token = cookieToken;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                await _next(context);
                return;
            }

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();

                JwtSecurityToken? jwtToken = null;
                try
                {
                    // Detailed JWT diagnostics are useful during development but too noisy for
                    // normal console output. Log them at Debug level so they can be enabled
                    // via configuration when needed.
                    Serilog.Log.Debug("[JWT DEBUG] Incoming raw token: {Token}", token);

                    jwtToken = tokenHandler.ReadJwtToken(token);
                    Serilog.Log.Debug("[JWT DEBUG] Token header alg={Alg}, kid={Kid}, typ={Typ}",
                        jwtToken.Header.Alg, jwtToken.Header.Kid ?? "(none)", jwtToken.Header.Typ ?? "(none)");
                    Serilog.Log.Debug("[JWT DEBUG] Token rawHeader={RawHeader}", jwtToken.RawHeader);
                    Serilog.Log.Debug("[JWT DEBUG] Token rawPayload={RawPayload}", jwtToken.RawPayload);
                }
                catch (Exception rex)
                {
                    Serilog.Log.Debug(rex, "[JWT DEBUG] Failed to read incoming token");
                }

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = (tokenString, securityToken, kid, parameters) =>
                    {
                        Serilog.Log.Debug("[JWT DEBUG] IssuerSigningKeyResolver called. Token kid={Kid}, ValidationKey KeyId={KeyId}", 
                            kid ?? "(none)", _validationKey?.KeyId ?? "(none)");
                        return new[] { _validationKey };
                    },
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"] ?? JwtConstants.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"] ?? "Saas.Infra.Clients",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    RequireSignedTokens = true,
                };

                try
                {
                    var keysCount = validationParameters.IssuerSigningKeys?.Count() ?? 0;
                    var hasResolver = validationParameters.IssuerSigningKeyResolver != null;
                    Serilog.Log.Debug("[JWT DEBUG] ValidationParameters: IssuerSigningKeysCount={Count}, HasResolver={HasResolver}, ValidationKey KeyId={KeyId}",
                        keysCount, hasResolver, _validationKey?.KeyId ?? "(none)");
                }
                catch (Exception lx)
                {
                    Serilog.Log.Debug(lx, "[JWT DEBUG] Failed to log ValidationParameters details");
                }

                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                if (principal?.Identity is ClaimsIdentity identity)
                {
                    var authenticatedIdentity = new ClaimsIdentity(
                        identity.Claims,
                        "Bearer",
                        identity.NameClaimType ?? ClaimTypes.Name,
                        identity.RoleClaimType ?? ClaimTypes.Role);

                    context.User = new ClaimsPrincipal(authenticatedIdentity);

                    Serilog.Log.Debug(
                        "[JWT DEBUG] JWT validation success. User Name={Name}, IsAuthenticated={IsAuth}, AuthenticationType={AuthType}",
                        context.User.Identity?.Name ?? "(unknown)",
                        context.User.Identity?.IsAuthenticated,
                        context.User.Identity?.AuthenticationType);
                }
                else
                {
                    Serilog.Log.Warning("[JWT DEBUG] Token validation passed but principal.Identity is null or not ClaimsIdentity");
                }
            }
            catch (SecurityTokenSignatureKeyNotFoundException ex)
            {
                Serilog.Log.Error(ex, "[JWT ERROR] Signature key not found: {Message}", ex.Message);
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                Serilog.Log.Error(ex, "[JWT ERROR] Invalid signature: {Message}", ex.Message);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "[JWT ERROR] JWT validation failed: {Message}", ex.Message);
            }

            await _next(context);
        }
    }
}