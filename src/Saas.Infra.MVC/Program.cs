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

                // 读取并导入RSA公钥（仅用于此处的Token验证）
                var rsaPublicKey = RSA.Create();
                string publicKeyText = File.ReadAllText(publicKeyPath);
                rsaPublicKey.ImportFromPem(publicKeyText); // 导入公钥（验证Token）

                // ===================== 4. 注册JWT认证服务（替换为RSA签名） =====================
                builder.Services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtIssuer,
                        ValidateAudience = true,
                        ValidAudience = jwtAudience,
                        ValidateIssuerSigningKey = true,
                        // ✅ 核心修改：替换为RSA公钥验证签名
                        IssuerSigningKey = new RsaSecurityKey(rsaPublicKey),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero, // 生产环境建议0偏移（严格过期）
                        RequireSignedTokens = true,
                        RequireExpirationTime = true
                    };
                    options.SaveToken = false; // 纯JWT，不存Cookie
                    options.IncludeErrorDetails = builder.Environment.IsDevelopment();

                    // 替换Console为Serilog日志（同时输出到控制台+文件）
                    options.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = context =>
                        {
                            Log.Error(context.Exception, "JWT authentication failed: {ErrorMessage}", context.Exception.Message);
                            return Task.CompletedTask;
                        },
                        OnTokenValidated = context =>
                        {
                            var username = context.Principal?.Identity?.Name ?? "unknown user";
                            Log.Information("JWT authentication succeeded for user: {Username}", username);
                            return Task.CompletedTask;
                        }
                    };
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

                // ===================== 7. 注册基础服务 =====================
                builder.Services.AddAuthorization();
                builder.Services.AddControllersWithViews();
                builder.Services.AddLogging(); // 供异常中间件使用

                // Add session support for MVC
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

                // ===================== 8. 构建应用并配置管道 =====================
                var app = builder.Build();

                // 【核心】注册全局异常中间件（必须放在管道最前面）
                app.UseMiddleware<ExceptionHandlingMiddleware>();

                // Add security headers middleware
                app.UseSecurityHeaders();

                // 环境配置
                if (!app.Environment.IsDevelopment())
                {
                    app.UseExceptionHandler("/Account/Login");
                    app.UseHsts();
                }

                app.UseHttpsRedirection();
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

                // 认证&授权
                app.UseAuthentication();
                app.UseAuthorization();

                // Add antiforgery middleware
                app.UseAntiforgery();

                app.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Account}/{action=Login}/{id?}");

                Log.Information("Saas.Infra.MVC started, listening on: {Urls}", string.Join("; ", app.Urls));
                
                // Initialize database and seed test data
                using (var scope = app.Services.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<Saas.Infra.Data.ApplicationDbContext>();
                    try
                    {
                        // Ensure database is created
                        dbContext.Database.EnsureCreated();
                        Log.Information("Database initialized successfully");
                        
                        // Seed test user if not exists
                        var userRepository = scope.ServiceProvider.GetRequiredService<Saas.Infra.Core.IUserRepository>();
                        var passwordHasher = scope.ServiceProvider.GetRequiredService<Saas.Infra.Core.IPasswordHasher>();
                        
                        var testUser = userRepository.GetByEmailAsync("test@126.com").Result;
                        if (testUser == null)
                        {
                            var newUser = new Saas.Infra.Core.User
                            {
                                Username = "test_user",
                                Email = "test@126.com",
                                PasswordHash = passwordHasher.HashPassword("Test@123456"),
                                CreatedTime = DateTime.UtcNow
                            };
                            userRepository.AddAsync(newUser).Wait();
                            Log.Information("Test user created: test@126.com");
                        }

                        // Seed sample products if Products table is empty (useful for local development)
                        try
                        {
                            if (!dbContext.Products.Any())
                            {
                                dbContext.Products.AddRange(
                                    new Saas.Infra.Data.ProductEntity
                                    {
                                        Id = "product_alpha",
                                        Name = "Product Alpha",
                                        Url = "/alpha",
                                        Description = "Sample product: Alpha"
                                    },
                                    new Saas.Infra.Data.ProductEntity
                                    {
                                        Id = "product_beta",
                                        Name = "Product Beta",
                                        Url = "/beta",
                                        Description = "Sample product: Beta"
                                    }
                                );
                                dbContext.SaveChanges();
                                Log.Information("Seeded sample products into Products table");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Failed to seed sample products");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error during database initialization");
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