using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Saas.Infra.Core;
using Saas.Infra.MVC.Middleware;
using Serilog;
using System.Security.Cryptography;
using Microsoft.AspNetCore.HttpOverrides; // 新增引用

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

                // --- 修改点 1: 配置转发头服务 ---
                builder.Services.Configure<ForwardedHeadersOptions>(options =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                    // 只有在知道代理服务器 IP 的情况下才需要清除 KnownNetworks 和 KnownProxies
                    options.KnownNetworks.Clear();
                    options.KnownProxies.Clear();
                });

                builder.Host.UseSerilog();

                var runtimeEnv = UtilityService.GetCurrentEnvironment();
                var envMessage = $"Runtime environment detected: {runtimeEnv}";
                Console.WriteLine(envMessage);
                Log.Information(envMessage);

                if (runtimeEnv == RuntimeEnvironment.AzureContainerApps
                    || runtimeEnv == RuntimeEnvironment.LocalContainer)
                {
                    var rsaPrivateKeyContent = builder.Configuration["rsa-private-key-content"]
                                             ?? builder.Configuration["RSA_PRIVATE_KEY_CONTENT"]
                                             ?? Environment.GetEnvironmentVariable("rsa-private-key-content");

                    var rsaPublicKeyContent = builder.Configuration["rsa-public-key-content"]
                                             ?? builder.Configuration["RSA_PUBLIC_KEY_CONTENT"]
                                             ?? Environment.GetEnvironmentVariable("rsa-public-key-content");

                    var privateKeyPathConfig = builder.Configuration["Jwt:PrivateKeyPath"] ?? "Secrets/sso_rsa_private.pem";
                    var publicKeyPathConfig = builder.Configuration["Jwt:PublicKeyPath"] ?? "PublicKeys/sso_rsa_public.pem";

                    if (!string.IsNullOrEmpty(rsaPrivateKeyContent))
                    {
                        var directory = Path.GetDirectoryName(privateKeyPathConfig);
                        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                        File.WriteAllText(privateKeyPathConfig, rsaPrivateKeyContent);
                        Log.Information("RSA private key file restored from environment variable to {Path}", privateKeyPathConfig);
                    }
                    else
                    {
                        Log.Warning("Container environment ({Env}) detected but RSA-PRIVATE-KEY-CONTENT is not set", runtimeEnv);
                    }

                    if (!string.IsNullOrEmpty(rsaPublicKeyContent))
                    {
                        var directory = Path.GetDirectoryName(publicKeyPathConfig);
                        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                        File.WriteAllText(publicKeyPathConfig, rsaPublicKeyContent);
                        Log.Information("RSA public key file restored from environment variable to {Path}", publicKeyPathConfig);
                    }
                    else
                    {
                        Log.Warning("Container environment ({Env}) detected but RSA-PUBLIC-KEY-CONTENT is not set", runtimeEnv);
                    }
                }

                var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? JwtConstants.Issuer;
                var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Saas.Infra.Clients";

                var privateKeyPath = builder.Configuration["Jwt:PrivateKeyPath"]
                    ?? throw new ArgumentNullException("Jwt:PrivateKeyPath is not configured.");
                var publicKeyPath = builder.Configuration["Jwt:PublicKeyPath"]
                    ?? throw new ArgumentNullException("Jwt:PublicKeyPath is not configured.");

                if (!File.Exists(privateKeyPath))
                    throw new FileNotFoundException("RSA private key file not found", privateKeyPath);
                if (!File.Exists(publicKeyPath))
                    throw new FileNotFoundException("RSA public key file not found", publicKeyPath);

                builder.Services.AddSingleton(sp =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    var keyPath = configuration["Jwt:PrivateKeyPath"] ?? throw new InvalidOperationException("Jwt:PrivateKeyPath not configured.");
                    var keyText = File.ReadAllText(keyPath);
                    var rsaInstance = RSA.Create();
                    rsaInstance.ImportFromPem(keyText);
                    Log.Information("RSA private key loaded successfully from {KeyPath}", keyPath);
                    return rsaInstance;
                });

                builder.Services.AddSingleton(sp =>
                {
                    var rsa = sp.GetRequiredService<RSA>();
                    var publicBytes = rsa.ExportSubjectPublicKeyInfo();
                    using var shaForKid = System.Security.Cryptography.SHA256.Create();
                    var kidBytes = shaForKid.ComputeHash(publicBytes);
                    var keyId = Base64UrlEncoder.Encode(kidBytes);
                    Log.Information("Computed JWT KeyId (kid): {KeyId}", keyId);
                    return new RsaSecurityKey(rsa) { KeyId = keyId };
                });

                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new() { Title = "Saas.Infra.MVC API", Version = "v1" });
                    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                        Description = "Enter 'Bearer' followed by a space and your JWT token."
                    });
                    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                    {
                        {
                            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                            {
                                Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
                            },
                            Array.Empty<string>()
                        }
                    });
                });

                builder.Services.AddAuthorization();
                builder.Services.AddAuthentication(options =>
                {
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Saas.Infra.MVC.Middleware.DummyJwtAuthenticationHandler>(JwtBearerDefaults.AuthenticationScheme, options => { });

                builder.Services.AddControllers();
                builder.Services.AddRazorComponents().AddInteractiveServerComponents();

                builder.Services.AddScoped<Saas.Infra.MVC.Services.Blazor.BlazorTokenService>();
                builder.Services.AddSingleton<Saas.Infra.MVC.Services.Blazor.BlazorAuthHandoffService>();
                builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, Saas.Infra.MVC.Services.Blazor.BlazorAuthStateProvider>();
                builder.Services.AddHttpContextAccessor();
                builder.Services.AddLogging();
                builder.Services.AddDistributedMemoryCache();

                builder.Services.AddSession(options =>
                {
                    options.IdleTimeout = TimeSpan.FromMinutes(30);
                    options.Cookie.HttpOnly = true;
                    options.Cookie.IsEssential = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
                });

                builder.Services.AddAntiforgery(options =>
                {
                    options.HeaderName = "X-CSRF-TOKEN";
                    options.FormFieldName = "__RequestVerificationToken";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
                });

                builder.Services.AddHttpClient();
                builder.Services.Configure<Saas.Infra.Core.JwtOptions>(builder.Configuration.GetSection("Jwt"));
                builder.Services.AddScoped<Saas.Infra.Core.ITokenService, Saas.Infra.SSO.TokenService>();

                var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection")
                    ?? Environment.GetEnvironmentVariable("connectionstrings-defaultconnection");
                if (!string.IsNullOrWhiteSpace(defaultConn))
                {
                    builder.Services.AddDbContext<Saas.Infra.Data.ApplicationDbContext>(options =>
                        options.UseNpgsql(defaultConn));
                }

                builder.Services.AddScoped<Saas.Infra.Core.IUserRepository, Saas.Infra.Data.UserRepository>();
                builder.Services.AddScoped<Saas.Infra.Core.IRefreshTokenRepository, Saas.Infra.Data.RefreshTokenRepository>();
                builder.Services.AddScoped<Saas.Infra.Core.IPasswordHasher, Saas.Infra.SSO.BCryptPasswordHasher>();
                builder.Services.AddScoped<Saas.Infra.SSO.ISsoService, Saas.Infra.SSO.SsoService>();
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Redirect.IRedirectValidator, Saas.Infra.MVC.Services.Redirect.RedirectValidator>();
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Product.IProductConfigService, Saas.Infra.MVC.Services.Product.ProductConfigService>();
                builder.Services.AddSingleton<Saas.Infra.MVC.Services.Errors.GlobalExceptionPageService>();

                builder.Services.AddSingleton<Saas.Infra.MVC.Services.Payment.IPaymentGateway>(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var secretKey = config["Stripe:SecretKey"] ?? config["Stripe__SecretKey"] ?? Environment.GetEnvironmentVariable("Stripe__SecretKey") ?? throw new InvalidOperationException("Stripe:SecretKey not configured");
                    var webhookSecret = config["Stripe:WebhookSecret"] ?? config["Stripe__WebhookSecret"] ?? Environment.GetEnvironmentVariable("Stripe__WebhookSecret") ?? throw new InvalidOperationException("Stripe:WebhookSecret not configured");
                    return new Saas.Infra.MVC.Services.Payment.StripePaymentGateway(secretKey, webhookSecret);
                });

                builder.Services.AddScoped<Saas.Infra.MVC.Services.Payment.IPaymentService, Saas.Infra.MVC.Services.Payment.PaymentService>();
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Payment.IStripeWebhookService, Saas.Infra.MVC.Services.Payment.StripeWebhookService>();
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Payment.ISubscriptionTokenService, Saas.Infra.MVC.Services.Payment.SubscriptionTokenService>();

                var app = builder.Build();

                // --- 修改点 2: 必须放在管道最前面以启用转发头 ---
                app.UseForwardedHeaders();

                app.UseMiddleware<ExceptionHandlingMiddleware>();
                app.UseSecurityHeaders();

                if (!app.Environment.IsDevelopment())
                {
                    app.UseExceptionHandler("/error");
                    app.UseHsts();
                }

                // --- 修改点 3: 容器内 Ingress 已处理 HTTPS，内部通常不再强制重定向以避免循环 ---
                if (app.Environment.IsDevelopment())
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
                        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Saas.Infra.MVC API v1");
                        options.RoutePrefix = "swagger";
                    });
                }

                app.UseMiddleware<CustomJwtMiddleware>();
                app.UseAuthentication();
                app.UseAuthorization();

                app.UseStatusCodePages(async statusCodeContext =>
                {
                    var httpContext = statusCodeContext.HttpContext;
                    if (httpContext.Request.Path.StartsWithSegments("/api") || httpContext.Request.Path.StartsWithSegments("/_blazor") || httpContext.Request.Path.StartsWithSegments("/_framework")) return;
                    var statusCode = httpContext.Response.StatusCode;
                    var acceptsHtml = httpContext.Request.Headers.Accept.Any(value => value.Contains("text/html", StringComparison.OrdinalIgnoreCase));
                    if (!acceptsHtml) return;
                    var redirectUrl = statusCode switch
                    {
                        StatusCodes.Status401Unauthorized => "/account/login?message=Authentication%20required.",
                        StatusCodes.Status403Forbidden => "/access-denied?message=Bearer%20was%20forbidden.",
                        StatusCodes.Status404NotFound => "/error?statusCode=404&message=The%20requested%20page%20was%20not%20found.",
                        _ => null
                    };
                    if (!string.IsNullOrWhiteSpace(redirectUrl)) httpContext.Response.Redirect(redirectUrl);
                });

                app.UseAntiforgery();
                app.MapControllers();
                app.MapRazorComponents<Saas.Infra.MVC.Components.App>().AddInteractiveServerRenderMode();

                using (var scope = app.Services.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<Saas.Infra.Data.ApplicationDbContext>();
                    try
                    {
                        if (dbContext.Database.CanConnect()) Log.Information("Database connection verified successfully");
                        else Log.Warning("Database connection verification failed");
                    }
                    catch (Exception ex) { Log.Error(ex, "Error during database connection verification"); }
                }

                app.Run();
            }
            catch (Exception ex) { Log.Fatal(ex, $"Application startup failed, {ex.Message}"); }
            finally { Log.CloseAndFlush(); }
        }
    }

    public static class JwtConstants
    {
        public const string Issuer = "Saas.Infra.Server";
    }
}