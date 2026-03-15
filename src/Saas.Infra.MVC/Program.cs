using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Saas.Infra.Core;
using Saas.Infra.MVC.Middleware;
using Serilog;
using System.Security.Cryptography;
using Microsoft.AspNetCore.HttpOverrides;

namespace Saas.Infra.MVC
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(
                    outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}")
                .WriteTo.File(
                    path: Path.Combine(AppContext.BaseDirectory, "Logs", "saas-log-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 365,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("Saas.Infra.MVC application starting...");
                var builder = WebApplication.CreateBuilder(args);

                // --- 修改点 1: 配置转发头 ---
                builder.Services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    options.KnownNetworks.Clear();
                    options.KnownProxies.Clear();
                });

                builder.Host.UseSerilog();

                var runtimeEnv = UtilityService.GetCurrentEnvironment();
                Log.Information("Runtime environment detected: {RuntimeEnv}", runtimeEnv);

                // --- 修改点 2: 增强的 RSA 文件还原逻辑 ---
                var baseDir = AppContext.BaseDirectory;
                // 获取配置的相对路径并转为绝对路径
                string relPrivateKeyPath = builder.Configuration["Jwt:PrivateKeyPath"] ?? "Secrets/sso_rsa_private.pem";
                string relPublicKeyPath = builder.Configuration["Jwt:PublicKeyPath"] ?? "PublicKeys/sso_rsa_public.pem";

                var privateKeyPath = Path.Combine(baseDir, relPrivateKeyPath);
                var publicKeyPath = Path.Combine(baseDir, relPublicKeyPath);

                if (runtimeEnv == RuntimeEnvironment.AzureContainerApps || runtimeEnv == RuntimeEnvironment.LocalContainer)
                {
                    // 兼容多种命名格式
                    var rsaPrivateKeyContent = builder.Configuration["RSA_PRIVATE_KEY_CONTENT"]
                                             ?? builder.Configuration["rsa-private-key-content"]
                                             ?? Environment.GetEnvironmentVariable("RSA_PRIVATE_KEY_CONTENT");

                    var rsaPublicKeyContent = builder.Configuration["RSA_PUBLIC_KEY_CONTENT"]
                                            ?? builder.Configuration["rsa-public-key-content"]
                                            ?? Environment.GetEnvironmentVariable("RSA_PUBLIC_KEY_CONTENT");

                    if (!string.IsNullOrEmpty(rsaPrivateKeyContent))
                    {
                        var dir = Path.GetDirectoryName(privateKeyPath);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        File.WriteAllText(privateKeyPath, rsaPrivateKeyContent);
                        Log.Information("RSA private key restored to {Path}", privateKeyPath);
                    }

                    if (!string.IsNullOrEmpty(rsaPublicKeyContent))
                    {
                        var dir = Path.GetDirectoryName(publicKeyPath);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        File.WriteAllText(publicKeyPath, rsaPublicKeyContent);
                        Log.Information("RSA public key restored to {Path}", publicKeyPath);
                    }
                }

                // --- 严格校验文件是否存在 ---
                if (!File.Exists(privateKeyPath))
                    throw new FileNotFoundException($"CRITICAL: RSA private key NOT FOUND at {privateKeyPath}");
                if (!File.Exists(publicKeyPath))
                    throw new FileNotFoundException($"CRITICAL: RSA public key NOT FOUND at {publicKeyPath}");

                builder.Services.AddSingleton(sp =>
                {
                    var keyText = File.ReadAllText(privateKeyPath);
                    var rsaInstance = RSA.Create();
                    rsaInstance.ImportFromPem(keyText);
                    Log.Information("RSA private key loaded successfully");
                    return rsaInstance;
                });

                builder.Services.AddSingleton(sp =>
                {
                    var rsa = sp.GetRequiredService<RSA>();
                    var publicBytes = rsa.ExportSubjectPublicKeyInfo();
                    using var shaForKid = SHA256.Create();
                    var kidBytes = shaForKid.ComputeHash(publicBytes);
                    var keyId = Base64UrlEncoder.Encode(kidBytes);
                    return new RsaSecurityKey(rsa) { KeyId = keyId };
                });

                // --- 其他业务服务保持不变 ---
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen();
                builder.Services.AddAuthorization();
                builder.Services.AddAuthentication(options =>
                {
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DummyJwtAuthenticationHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });

                builder.Services.AddControllers();
                builder.Services.AddRazorComponents().AddInteractiveServerComponents();
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Blazor.BlazorTokenService>();
                builder.Services.AddSingleton<Saas.Infra.MVC.Services.Blazor.BlazorAuthHandoffService>();
                builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, Saas.Infra.MVC.Services.Blazor.BlazorAuthStateProvider>();
                builder.Services.AddHttpContextAccessor();
                builder.Services.AddDistributedMemoryCache();

                builder.Services.AddSession(options =>
                {
                    options.IdleTimeout = TimeSpan.FromMinutes(30);
                    options.Cookie.HttpOnly = true;
                    options.Cookie.IsEssential = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                });

                builder.Services.AddAntiforgery(options =>
                {
                    options.HeaderName = "X-CSRF-TOKEN";
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                });

                builder.Services.AddHttpClient();
                builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
                builder.Services.AddScoped<ITokenService, Saas.Infra.SSO.TokenService>();

                var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection")
                    ?? builder.Configuration["ConnectionStrings:DefaultConnection"]
                    ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

                if (!string.IsNullOrWhiteSpace(defaultConn))
                {
                    builder.Services.AddDbContext<Saas.Infra.Data.ApplicationDbContext>(options =>
                        options.UseNpgsql(defaultConn));
                }

                builder.Services.AddScoped<IUserRepository, Saas.Infra.Data.UserRepository>();
                builder.Services.AddScoped<IRefreshTokenRepository, Saas.Infra.Data.RefreshTokenRepository>();
                builder.Services.AddScoped<IPasswordHasher, Saas.Infra.SSO.BCryptPasswordHasher>();
                builder.Services.AddScoped<Saas.Infra.SSO.ISsoService, Saas.Infra.SSO.SsoService>();
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Redirect.IRedirectValidator, Saas.Infra.MVC.Services.Redirect.RedirectValidator>();
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Product.IProductConfigService, Saas.Infra.MVC.Services.Product.ProductConfigService>();
                builder.Services.AddSingleton<Saas.Infra.MVC.Services.Errors.GlobalExceptionPageService>();

                builder.Services.AddSingleton<Saas.Infra.MVC.Services.Payment.IPaymentGateway>(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var secretKey = config["Stripe:SecretKey"] ?? config["Stripe__SecretKey"] ?? throw new InvalidOperationException("Stripe:SecretKey not found");
                    var webhookSecret = config["Stripe:WebhookSecret"] ?? config["Stripe__WebhookSecret"] ?? throw new InvalidOperationException("Stripe:WebhookSecret not found");
                    return new Saas.Infra.MVC.Services.Payment.StripePaymentGateway(secretKey, webhookSecret);
                });

                builder.Services.AddScoped<Saas.Infra.MVC.Services.Payment.IPaymentService, Saas.Infra.MVC.Services.Payment.PaymentService>();
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Payment.IStripeWebhookService, Saas.Infra.MVC.Services.Payment.StripeWebhookService>();
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Payment.ISubscriptionTokenService, Saas.Infra.MVC.Services.Payment.SubscriptionTokenService>();

                var app = builder.Build();

                // --- 修改点 3: 管道配置 ---
                app.UseForwardedHeaders();
                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.UseSecurityHeaders();

                if (!app.Environment.IsDevelopment())
                {
                    app.UseExceptionHandler("/error");
                    app.UseHsts();
                }
                else
                {
                    app.UseHttpsRedirection();
                }

                app.UseStaticFiles();
                app.UseRouting();
                app.UseSession();

                if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI(options =>
                    {
                        options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
                    });
                }

                app.UseMiddleware<CustomJwtMiddleware>();
                app.UseAuthentication();
                app.UseAuthorization();

                app.UseStatusCodePages(async context => {
                    // 原有重定向逻辑...
                    var response = context.HttpContext.Response;
                    if (response.StatusCode == StatusCodes.Status401Unauthorized)
                        response.Redirect("/account/login?message=Authentication%20required.");
                });

                app.UseAntiforgery();
                app.MapControllers();
                app.MapRazorComponents<Saas.Infra.MVC.Components.App>().AddInteractiveServerRenderMode();

                using (var scope = app.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<Saas.Infra.Data.ApplicationDbContext>();
                    if (db.Database.CanConnect()) Log.Information("DB Connected");
                }

                app.Run();
            }
            catch (Exception ex) { Log.Fatal(ex, "Start failed"); }
            finally { Log.CloseAndFlush(); }
        }
    }

    public static class JwtConstants { public const string Issuer = "Saas.Infra.Server"; }
}