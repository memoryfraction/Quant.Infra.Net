using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Saas.Infra.MVC.Middleware
{
    public class CustomJwtMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private readonly RsaSecurityKey _validationKey;

        public CustomJwtMiddleware(
            RequestDelegate next,
            IConfiguration configuration,
            RsaSecurityKey validationKey)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _validationKey = validationKey ?? throw new ArgumentNullException(nameof(validationKey));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

            if (string.IsNullOrEmpty(authHeader) ||
                !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var token = authHeader["Bearer ".Length..].Trim();

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();

                // 读取并记录 token header（诊断用途）
                JwtSecurityToken? jwtToken = null;
                try
                {
                    // Log full incoming token for diagnostics
                    Serilog.Log.Information("[JWT DEBUG] incoming raw token: {Token}", token);

                    jwtToken = tokenHandler.ReadJwtToken(token);
                    Serilog.Log.Information("[JWT DEBUG] ReadJwtToken header alg={Alg}, kid={Kid}, typ={Typ}", jwtToken.Header.Alg, jwtToken.Header.Kid ?? "(none)", jwtToken.Header.Typ ?? "(none)");
                    Serilog.Log.Information("[JWT DEBUG] ReadJwtToken rawHeader={RawHeader}", jwtToken.RawHeader);
                    Serilog.Log.Information("[JWT DEBUG] ReadJwtToken rawPayload={RawPayload}", jwtToken.RawPayload);
                }
                catch (Exception rex)
                {
                    Serilog.Log.Warning(rex, "[JWT DEBUG] ReadJwtToken failed");
                }

                var validationParameters = new TokenValidationParameters
                {
                    // 必须验证签名
                    ValidateIssuerSigningKey = true,

                    // 通过 Resolver 强制使用我们注入的固定公钥（绕过 token.kid）
                    IssuerSigningKeyResolver = (tokenString, securityToken, kid, parameters) =>
                    {
                        Serilog.Log.Information("[JWT DEBUG] IssuerSigningKeyResolver called. token.kid={Kid}, configuredKeyId={KeyId}", kid ?? "(none)", _validationKey?.KeyId ?? "(none)");
                        return new[] { _validationKey };
                    },

                    // issuer 和 audience 校验（根据你之前 token 的实际值）
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"] ?? JwtConstants.Issuer,

                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"] ?? "Saas.Infra.Clients",

                    // 时间校验
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,

                    // 要求 token 必须签名（默认就是，但显式写出来更安全）
                    RequireSignedTokens = true,
                };

                // 诊断：记录 validationParameters 关键信息
                try
                {
                    var keysCount = validationParameters.IssuerSigningKeys?.Count() ?? 0;
                    var hasResolver = validationParameters.IssuerSigningKeyResolver != null;
                    Serilog.Log.Information("[JWT DEBUG] ValidationParameters prepared. IssuerSigningKeysCount={Count}, HasResolver={HasResolver}, validationKey.KeyId={KeyId}", keysCount, hasResolver, _validationKey?.KeyId ?? "(none)");
                }
                catch (Exception lx)
                {
                    Serilog.Log.Warning(lx, "[JWT DEBUG] Failed to log ValidationParameters details");
                }

                // 执行验证
                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

                if (principal?.Identity is ClaimsIdentity identity)
                {
                    // 确保 AuthenticationType 正确，让 IsAuthenticated 为 true
                    var authenticatedIdentity = new ClaimsIdentity(
                        identity.Claims,
                        "Bearer",
                        identity.NameClaimType ?? ClaimTypes.Name,
                        identity.RoleClaimType ?? ClaimTypes.Role);

                    context.User = new ClaimsPrincipal(authenticatedIdentity);

                    Serilog.Log.Information(
                        "JWT 验证成功。User Name = {Name}, IsAuthenticated = {IsAuth}, AuthenticationType = {AuthType}",
                        context.User.Identity?.Name ?? "(unknown)",
                        context.User.Identity?.IsAuthenticated,
                        context.User.Identity?.AuthenticationType);
                }
                else
                {
                    Serilog.Log.Warning("Token 验证通过，但 principal.Identity 为 null 或不是 ClaimsIdentity");
                }
            }
            catch (SecurityTokenSignatureKeyNotFoundException ex)
            {
                Serilog.Log.Error(ex, "签名密钥未找到：{Message}", ex.Message);
            }
            catch (SecurityTokenInvalidSignatureException ex)
            {
                Serilog.Log.Error(ex, "签名无效：{Message}", ex.Message);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "JWT 验证失败：{Message}", ex.Message);
            }

            await _next(context);
        }
    }
}