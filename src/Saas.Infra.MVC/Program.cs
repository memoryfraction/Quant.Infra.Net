using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Saas.Infra.MVC.Middleware; // 引入自定义异常中间件
using Serilog;
using System.Text;

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

                // ===================== 3. 读取JWT配置 =====================
                var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
                    ?? throw new ArgumentNullException("Jwt:SigningKey is not configured. Please check appsettings.json or user-secrets.");
                var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? JwtConstants.Issuer;
                var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Saas.Infra.Clients";

                // ===================== 4. 注册JWT认证服务（补充Serilog日志） =====================
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
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(5),
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

                // ===================== 5. 极简Swagger配置 =====================
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new() { Title = "Saas.Infra.MVC API", Version = "v1" });
                });

                // ===================== 6. 注册基础服务 =====================
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
                });
                
                // Add HTTP client factory for API calls
                builder.Services.AddHttpClient();

                // Register application services and data access
                // Bind Jwt options and register token service
                builder.Services.Configure<Saas.Infra.Core.JwtOptions>(builder.Configuration.GetSection("Jwt"));
                builder.Services.AddSingleton<Saas.Infra.Core.ITokenService, Saas.Infra.Core.TokenService>();

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

                // ===================== 7. 构建应用并配置管道 =====================
                var app = builder.Build();

                // 【核心】注册全局异常中间件（必须放在管道最前面）
                app.UseMiddleware<ExceptionHandlingMiddleware>();

                // 环境配置
                if (!app.Environment.IsDevelopment())
                {
                    app.UseExceptionHandler("/Home/Error");
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

                app.MapStaticAssets();
                app.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}")
                    .WithStaticAssets();

                Log.Information("Saas.Infra.MVC started, listening on: {Urls}", string.Join("; ", app.Urls));
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