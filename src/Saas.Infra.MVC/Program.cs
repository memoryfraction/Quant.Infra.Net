using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Saas.Infra.MVC.Middleware; // 引入自定义异常中间件
using Serilog;
using System.Security.Cryptography;

namespace Saas.Infra.MVC
{
    /// <summary>
    /// 应用程序入口类
    /// </summary>
    public class Program
    {
        /// <summary>
        /// 程序主入口方法
        /// </summary>
        /// <param name="args">命令行参数</param>
        public static void Main(string[] args)
        {
            // ===================== 1. 配置Serilog全局日志（控制台+文件双输出） =====================
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console( // ✅ 核心新增：日志输出到控制台，解决黑框问题
                    outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}") // 控制台简洁格式
                .WriteTo.File( // 日志输出到文件（保留详细格式）
                    path: $"{AppContext.BaseDirectory}Logs\\saas-log-.log", // 按天滚动日志
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 365, // 保留365天
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            try
            {
                Log.Information("Saas.Infra.MVC application starting...");
                var builder = WebApplication.CreateBuilder(args);

                // ===================== 2. 替换默认日志为Serilog =====================
                builder.Host.UseSerilog(); // 关键：让ASP.NET Core使用Serilog作为日志提供器

                // ===================== 3. 读取JWT配置 + 加载RSA密钥（核心修改） =====================
                // JWT基础配置
                var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? JwtConstants.Issuer;
                var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Saas.Infra.Clients";

                // 加载RSA私钥（SSO签发用）和公钥（验证用）
                var privateKeyPath = builder.Configuration["Jwt:PrivateKeyPath"]
                    ?? throw new ArgumentNullException("Jwt:PrivateKeyPath is not configured. Check appsettings.json.");
                var publicKeyPath = builder.Configuration["Jwt:PublicKeyPath"]
                    ?? throw new ArgumentNullException("Jwt:PublicKeyPath is not configured. Check appsettings.json.");

                // 验证密钥文件存在
                if (!File.Exists(privateKeyPath))
                    throw new FileNotFoundException("RSA private key file not found", privateKeyPath);
                if (!File.Exists(publicKeyPath))
                    throw new FileNotFoundException("RSA public key file not found", publicKeyPath);

                // We will use a custom JWT middleware (CustomJwtMiddleware) instead of the built-in JwtBearer handler.
                // Register RSA private instance first (used by TokenService to sign tokens and by validation key below).
                builder.Services.AddSingleton(sp =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    var keyPath = configuration["Jwt:PrivateKeyPath"]
                        ?? throw new InvalidOperationException("Jwt:PrivateKeyPath is not configured.");
                    if (!File.Exists(keyPath))
                        throw new FileNotFoundException("RSA private key file not found", keyPath);

                    var keyText = File.ReadAllText(keyPath);
                    var rsaInstance = RSA.Create();
                    rsaInstance.ImportFromPem(keyText);
                    Log.Information("RSA private key loaded successfully from {KeyPath}", keyPath);
                    return rsaInstance;
                });

                // Register a single RsaSecurityKey based on the RSA singleton so TokenService and middleware share the same key.
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

                // ===================== 5. 注册RSA私钥实例到DI容器（供TokenService签发用） =====================
                // 使用 Singleton 生命周期，RSA实例在应用程序生命周期内保持不变。
                // RSA instances are thread-safe and should be reused across requests for better performance.
                builder.Services.AddSingleton(sp =>
                {
                    var configuration = sp.GetRequiredService<IConfiguration>();
                    var keyPath = configuration["Jwt:PrivateKeyPath"]
                        ?? throw new InvalidOperationException("Jwt:PrivateKeyPath is not configured.");
                    if (!File.Exists(keyPath))
                        throw new FileNotFoundException("RSA private key file not found", keyPath);

                    var keyText = File.ReadAllText(keyPath);
                    var rsaInstance = RSA.Create();
                    rsaInstance.ImportFromPem(keyText);
                    Log.Information("RSA private key loaded successfully from {KeyPath}", keyPath);
                    return rsaInstance;
                });

                // ===================== 6. 极简Swagger配置 =====================
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new() { Title = "Saas.Infra.MVC API", Version = "v1" });
                    // 新增：Swagger支持JWT授权
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
                                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                                {
                                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                    Id = "Bearer"
                                }
                            },
                            Array.Empty<string>()
                        }
                    });
                });

                // ===================== 7. 注册基础服务（API + Blazor 纯前端） =====================
                builder.Services.AddAuthorization();
                // Register a placeholder authentication handler for the "Bearer" scheme so
                // the framework can issue proper challenges/forbids when [Authorize] fails.
                builder.Services.AddAuthentication(options =>
                {
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Saas.Infra.MVC.Middleware.DummyJwtAuthenticationHandler>(JwtBearerDefaults.AuthenticationScheme, options => { });

                // API only – no Razor views. UI is implemented via Blazor components.
                builder.Services.AddControllers();

                // Blazor Server configuration
                builder.Services.AddRazorComponents()
                    .AddInteractiveServerComponents();

                // Blazor auth/token services (circuit scoped)
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Blazor.BlazorTokenService>();
                builder.Services.AddSingleton<Saas.Infra.MVC.Services.Blazor.BlazorAuthHandoffService>();
                builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider, Saas.Infra.MVC.Services.Blazor.BlazorAuthStateProvider>();
                builder.Services.AddHttpContextAccessor();

                builder.Services.AddLogging(); // 供异常中间件使用

                // Add in-memory distributed cache (required by session middleware)
                builder.Services.AddDistributedMemoryCache();

                // Add session support
                builder.Services.AddSession(options =>
                {
                    options.IdleTimeout = TimeSpan.FromMinutes(30);
                    options.Cookie.HttpOnly = true;
                    options.Cookie.IsEssential = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
                });

                // Add CSRF protection
                builder.Services.AddAntiforgery(options =>
                {
                    options.HeaderName = "X-CSRF-TOKEN";
                    options.FormFieldName = "__RequestVerificationToken";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
                });

                // Add HTTP client factory for API calls
                builder.Services.AddHttpClient();

                // Register application services and data access
                // Bind Jwt options and register token service
                builder.Services.Configure<Saas.Infra.Core.JwtOptions>(builder.Configuration.GetSection("Jwt"));
                // ITokenService is Scoped and depends on Singleton RSA instance
                builder.Services.AddScoped<Saas.Infra.Core.ITokenService, Saas.Infra.SSO.TokenService>();

                // Register EF Core DbContext and repositories
                var defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
                if (!string.IsNullOrWhiteSpace(defaultConn))
                {
                    // Use Npgsql (PostgreSQL) provider
                    builder.Services.AddDbContext<Saas.Infra.Data.ApplicationDbContext>(options =>
                        options.UseNpgsql(defaultConn));
                }

                // Repositories and helpers
                builder.Services.AddScoped<Saas.Infra.Core.IUserRepository, Saas.Infra.Data.UserRepository>();
                builder.Services.AddScoped<Saas.Infra.Core.IRefreshTokenRepository, Saas.Infra.Data.RefreshTokenRepository>();
                builder.Services.AddScoped<Saas.Infra.Core.IPasswordHasher, Saas.Infra.SSO.BCryptPasswordHasher>();

                // Register SSO service into DI container (scoped)
                builder.Services.AddScoped<Saas.Infra.SSO.ISsoService, Saas.Infra.SSO.SsoService>();

                // Register redirect validation and product configuration services
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Redirect.IRedirectValidator, Saas.Infra.MVC.Services.Redirect.RedirectValidator>();
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Product.IProductConfigService, Saas.Infra.MVC.Services.Product.ProductConfigService>();
                builder.Services.AddSingleton<Saas.Infra.MVC.Services.Errors.GlobalExceptionPageService>();

                // ===================== 支付服务注册 =====================
                // 注册支付网关（Stripe）
                builder.Services.AddSingleton<Saas.Infra.MVC.Services.Payment.IPaymentGateway>(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var secretKey = config["Stripe:SecretKey"] ?? throw new InvalidOperationException("Stripe:SecretKey not configured");
                    var webhookSecret = config["Stripe:WebhookSecret"] ?? throw new InvalidOperationException("Stripe:WebhookSecret not configured");
                    return new Saas.Infra.MVC.Services.Payment.StripePaymentGateway(secretKey, webhookSecret);
                });

                // 注册支付服务
                builder.Services.AddScoped<Saas.Infra.MVC.Services.Payment.IPaymentService, Saas.Infra.MVC.Services.Payment.PaymentService>();

                // ===================== 8. 构建应用并配置管道 =====================
                var app = builder.Build();

                // 【核心】注册全局异常中间件（必须放在管道最前面）
                app.UseMiddleware<ExceptionHandlingMiddleware>();

                // Add security headers middleware
                app.UseSecurityHeaders();

                // 环境配置
                if (!app.Environment.IsDevelopment())
                {
                    app.UseExceptionHandler("/error");
                    app.UseHsts();
                }

                app.UseHttpsRedirection();
                app.UseStaticFiles();
                app.UseRouting();

                // Add session middleware
                app.UseSession();

                // Swagger配置（仅开发环境）
                if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI(options =>
                    {
                        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Saas.Infra.MVC API v1");
                        options.RoutePrefix = "swagger";
                    });
                }

                // Custom JWT middleware performs token validation and sets HttpContext.User
                // Must run BEFORE UseAuthentication to properly set the user context
                app.UseMiddleware<CustomJwtMiddleware>();

                // 认证&授权
                app.UseAuthentication();
                app.UseAuthorization();

                app.UseStatusCodePages(async statusCodeContext =>
                {
                    var httpContext = statusCodeContext.HttpContext;
                    if (httpContext.Request.Path.StartsWithSegments("/api")
                        || httpContext.Request.Path.StartsWithSegments("/_blazor")
                        || httpContext.Request.Path.StartsWithSegments("/_framework"))
                    {
                        return;
                    }

                    var statusCode = httpContext.Response.StatusCode;
                    var acceptsHtml = httpContext.Request.Headers.Accept.Any(value => value.Contains("text/html", StringComparison.OrdinalIgnoreCase));
                    if (!acceptsHtml)
                    {
                        return;
                    }

                    var redirectUrl = statusCode switch
                    {
                        StatusCodes.Status401Unauthorized => "/account/login?message=Authentication%20required.",
                        StatusCodes.Status403Forbidden => "/access-denied?message=Bearer%20was%20forbidden.",
                        StatusCodes.Status404NotFound => "/error?statusCode=404&message=The%20requested%20page%20was%20not%20found.",
                        _ => null
                    };

                    if (!string.IsNullOrWhiteSpace(redirectUrl))
                    {
                        httpContext.Response.Redirect(redirectUrl);
                    }
                });

                // Add antiforgery middleware
                app.UseAntiforgery();

                // Map attribute-routed API controllers (under /api/* etc.)
                app.MapControllers();

                // Map Blazor components as the primary UI (no MVC views).
                app.MapRazorComponents<Saas.Infra.MVC.Components.App>()
                    .AddInteractiveServerRenderMode();

                Log.Information("Saas.Infra.MVC started, listening on: {Urls}", string.Join("; ", app.Urls));

                // Verify database connection on startup
                using (var scope = app.Services.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<Saas.Infra.Data.ApplicationDbContext>();
                    try
                    {
                        // Verify database is accessible
                        var canConnect = dbContext.Database.CanConnect();
                        if (canConnect)
                        {
                            Log.Information("Database connection verified successfully");
                        }
                        else
                        {
                            Log.Warning("Database connection verification failed");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error during database connection verification");
                    }
                }
                
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, $"Application startup failed, {ex.Message}");
            }
            finally
            {
                Log.CloseAndFlush(); // 确保日志写入文件
            }
        }
    }

    // 补充：如果项目中未定义 JwtConstants 类，需添加此辅助类（否则会编译报错）
    public static class JwtConstants
    {
        /// <summary>
        /// 默认JWT签发方
        /// </summary>
        public const string Issuer = "Saas.Infra.Server";
    }
}