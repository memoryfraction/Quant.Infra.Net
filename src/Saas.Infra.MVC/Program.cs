using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Saas.Infra.Core;
using Saas.Infra.MVC.Middleware;
using Saas.Infra.MVC.Services.Blazor;
using Microsoft.AspNetCore.Components.Authorization;
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
                .WriteTo.Console(outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}")
                .WriteTo.File(
                    path: Path.Combine(AppContext.BaseDirectory, "Logs", "saas-log-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 365,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("Saas.Infra.MVC application starting...");

                // --- Startup diagnostics ---
                Log.Information("[STARTUP DIAG] AppContext.BaseDirectory={BaseDir}", AppContext.BaseDirectory);
                Log.Information("[STARTUP DIAG] CONTAINER_APP_NAME={Val}", Environment.GetEnvironmentVariable("CONTAINER_APP_NAME") ?? "(not set)");
                Log.Information("[STARTUP DIAG] DOTNET_RUNNING_IN_CONTAINER={Val}", Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") ?? "(not set)");
                Log.Information("[STARTUP DIAG] ASPNETCORE_ENVIRONMENT={Val}", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "(not set)");
                Log.Information("[STARTUP DIAG] rsa-private-key-content env present={Val}", !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("rsa-private-key-content")));
                Log.Information("[STARTUP DIAG] rsa-public-key-content env present={Val}", !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("rsa-public-key-content")));
                Log.Information("[STARTUP DIAG] ConnectionStrings__DefaultConnection env present={Val}", !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")));
                var builder = WebApplication.CreateBuilder(args);

                // --- 1. 转发头配置（解决 Ingress HTTPS 识别） ---
                builder.Services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders =
                        ForwardedHeaders.XForwardedFor |
                        ForwardedHeaders.XForwardedProto |
                        ForwardedHeaders.XForwardedHost;
                    options.KnownNetworks.Clear();
                    options.KnownProxies.Clear();
                });

                builder.Host.UseSerilog();

                var runtimeEnv = UtilityService.GetCurrentEnvironment();
                Log.Information("Detected runtime environment: {Env}", runtimeEnv);

                // --- 2. 绝对路径还原 RSA 文件 ---
                var baseDir = AppContext.BaseDirectory;
                var privateKeyPath = Path.Combine(baseDir, "Secrets", "sso_rsa_private.pem");
                var publicKeyPath = Path.Combine(baseDir, "PublicKeys", "sso_rsa_public.pem");

                if (runtimeEnv == RuntimeEnvironment.AzureContainerApps || runtimeEnv == RuntimeEnvironment.LocalContainer)
                {
                    var rsaPrivateKeyContent = builder.Configuration["RSA_PRIVATE_KEY_CONTENT"]
                                             ?? builder.Configuration["rsa-private-key-content"];
                    var rsaPublicKeyContent = builder.Configuration["RSA_PUBLIC_KEY_CONTENT"]
                                            ?? builder.Configuration["rsa-public-key-content"];

                    Log.Information("RSA_PRIVATE_KEY_CONTENT present: {HasPrivate}, RSA_PUBLIC_KEY_CONTENT present: {HasPublic}",
                        !string.IsNullOrEmpty(rsaPrivateKeyContent), !string.IsNullOrEmpty(rsaPublicKeyContent));

                    if (!string.IsNullOrEmpty(rsaPrivateKeyContent))
                    {
                        var dir = Path.GetDirectoryName(privateKeyPath);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        File.WriteAllText(privateKeyPath, rsaPrivateKeyContent);
                        Log.Information("RSA private key restored to {Path}", privateKeyPath);
                    }
                    else
                    {
                        Log.Error("RSA_PRIVATE_KEY_CONTENT is empty or not configured in ACA secrets. Ensure the secret 'rsa-private-key-content' is set in Azure Container Apps.");
                    }
                    if (!string.IsNullOrEmpty(rsaPublicKeyContent))
                    {
                        var dir = Path.GetDirectoryName(publicKeyPath);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        File.WriteAllText(publicKeyPath, rsaPublicKeyContent);
                        Log.Information("RSA public key restored to {Path}", publicKeyPath);
                    }
                }
                else
                {
                    Log.Information("Non-container environment detected, expecting RSA key files on disk.");
                }

                if (!File.Exists(privateKeyPath))
                    throw new FileNotFoundException(
                        $"RSA private key NOT FOUND at {privateKeyPath}. "  +
                        $"Environment: {runtimeEnv}. "  +
                        "If running in ACA, ensure the secret 'rsa-private-key-content' is configured in Azure Portal > Container Apps > Secrets.");

                // --- 3. 基础组件注册 (RSA & JwtOptions) ---
                builder.Services.AddSingleton(sp =>
                {
                    var keyText = File.ReadAllText(privateKeyPath);
                    var rsaInstance = RSA.Create();
                    rsaInstance.ImportFromPem(keyText);
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

                builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

                // --- 4. 核心业务服务注册 (解决注入报错的关键) ---
                // 注意：必须先注册 ITokenService，ISsoService 才能成功解析
                builder.Services.AddScoped<ITokenService, Saas.Infra.SSO.TokenService>();
                builder.Services.AddScoped<Saas.Infra.SSO.ISsoService, Saas.Infra.SSO.SsoService>();

                // 其他 Repository 和基础设施
                builder.Services.AddScoped<IUserRepository, Saas.Infra.Data.UserRepository>();
                builder.Services.AddScoped<IRefreshTokenRepository, Saas.Infra.Data.RefreshTokenRepository>();
                builder.Services.AddScoped<IPasswordHasher, Saas.Infra.SSO.BCryptPasswordHasher>();

                // 产品配置服务（Dashboard 页面依赖）
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Product.IProductConfigService, Saas.Infra.MVC.Services.Product.ProductConfigService>();

                // 全局异常页面状态服务（/error 页面依赖，Singleton 因为跨请求共享状态）
                builder.Services.AddSingleton<Saas.Infra.MVC.Services.Errors.GlobalExceptionPageService>();

                builder.Services.AddDbContext<Saas.Infra.Data.ApplicationDbContext>(options =>
                {
                    var conn = builder.Configuration.GetConnectionString("DefaultConnection")
                               ?? builder.Configuration["ConnectionStrings__DefaultConnection"];
                    options.UseNpgsql(conn);
                });

                // --- 5. 身份验证与中间件配置 ---
                builder.Services.AddAuthentication(options =>
                {
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DummyJwtAuthenticationHandler>(JwtBearerDefaults.AuthenticationScheme, _ => { });

                builder.Services.AddControllers();
                builder.Services.AddRazorComponents().AddInteractiveServerComponents();
                builder.Services.AddHttpContextAccessor();

                // Blazor circuit-scoped services
                builder.Services.AddScoped<BlazorTokenService>();
                builder.Services.AddScoped<AuthenticationStateProvider, BlazorAuthStateProvider>();
                builder.Services.AddSingleton<BlazorAuthHandoffService>();
                builder.Services.AddHttpClient();
                builder.Services.AddDistributedMemoryCache();
                builder.Services.AddSession(options => {
                    options.IdleTimeout = TimeSpan.FromMinutes(30);
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                });

                // Stripe
                builder.Services.AddSingleton<Saas.Infra.MVC.Services.Payment.IPaymentGateway>(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var secretKey =
                        config["Stripe:SecretKey"] ??
                        config["Stripe__SecretKey"] ??
                        Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY");
                    var webhookSecret =
                        config["Stripe:WebhookSecret"] ??
                        config["Stripe__WebhookSecret"] ??
                        Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET");

                    if (string.IsNullOrWhiteSpace(secretKey))
                    {
                        throw new InvalidOperationException(
                            "Stripe SecretKey is not configured. Set Stripe__SecretKey (or STRIPE_SECRET_KEY) in deployment environment.");
                    }

                    if (string.IsNullOrWhiteSpace(webhookSecret))
                    {
                        Log.Warning("Stripe WebhookSecret is empty. Webhook verification will fail until Stripe__WebhookSecret is configured.");
                    }

                    return new Saas.Infra.MVC.Services.Payment.StripePaymentGateway(secretKey, webhookSecret ?? string.Empty);
                });

                builder.Services.AddScoped<Saas.Infra.MVC.Services.Payment.IPaymentService, Saas.Infra.MVC.Services.Payment.PaymentService>();
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Payment.IStripeWebhookService, Saas.Infra.MVC.Services.Payment.StripeWebhookService>();
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Payment.ISubscriptionTokenService, Saas.Infra.MVC.Services.Payment.SubscriptionTokenService>();

                var app = builder.Build();

                // --- 6. 管道顺序 ---
                app.UseForwardedHeaders(); // 必须第一

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
                app.UseMiddleware<CustomJwtMiddleware>();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseAntiforgery();
                app.MapControllers();
                app.MapRazorComponents<Saas.Infra.MVC.Components.App>().AddInteractiveServerRenderMode();

                app.Run();
            }
            catch (Exception ex) { Log.Fatal(ex, "Start failed"); throw; }
            finally { Log.CloseAndFlush(); }
        }
    }
}
