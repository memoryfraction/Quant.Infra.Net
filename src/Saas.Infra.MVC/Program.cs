using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Saas.Infra.Core;
using Saas.Infra.MVC.Middleware;
using Serilog;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Application startup and configuration
/// 应用启动和配置
/// </summary>

// ===================== 1. Configure Serilog global logging (console + file dual output) =====================
// ===================== 1. 配置Serilog全局日志（控制台+文件双输出） =====================
Log.Logger = new LoggerConfiguration()
	.WriteTo.Console(
		outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}")
	.WriteTo.File(
		path: $"{AppContext.BaseDirectory}Logs\\saas-log-.log",
		rollingInterval: RollingInterval.Day,
		retainedFileCountLimit: 365,
		outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
	.CreateLogger();

try
{
	Log.Information("Application starting...");
	var builder = WebApplication.CreateBuilder(args);

	// ===================== 2. Replace default logging with Serilog =====================
	// ===================== 2. 替换默认日志为Serilog =====================
	builder.Host.UseSerilog();

	// ===================== 3. Read JWT configuration =====================
	// ===================== 3. 读取JWT配置 =====================
	var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
	if (string.IsNullOrEmpty(jwtSigningKey))
		throw new ArgumentNullException("Jwt:SigningKey", "Jwt:SigningKey is not configured in appsettings.json");
	
	var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? JwtConstants.Issuer;
	var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Saas.Infra.Clients";

	// ===================== 4. Register JWT authentication service =====================
	// ===================== 4. 注册JWT认证服务 =====================
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
		options.SaveToken = false;
		options.IncludeErrorDetails = builder.Environment.IsDevelopment();

		options.Events = new JwtBearerEvents
		{
			OnAuthenticationFailed = context =>
			{
				Log.Error(context.Exception, "JWT authentication failed: {ErrorMessage}", context.Exception.Message);
				return Task.CompletedTask;
			},
			OnTokenValidated = context =>
			{
				var username = context.Principal?.Identity?.Name ?? "Unknown user";
				Log.Information("JWT authentication succeeded, user: {Username}", username);
				return Task.CompletedTask;
			}
		};
	});

	// ===================== 5. Register Swagger =====================
	// ===================== 5. 注册Swagger =====================
	builder.Services.AddEndpointsApiExplorer();
	builder.Services.AddSwaggerGen(c =>
	{
		c.SwaggerDoc("v1", new() { Title = "Saas.Infra.MVC API", Version = "v1" });
	});

	// ===================== 6. Register basic services =====================
	// ===================== 6. 注册基础服务 =====================
	builder.Services.AddAuthorization();
	builder.Services.AddControllersWithViews();
	builder.Services.AddLogging();

	// ===================== 7. Build application and configure pipeline =====================
	// ===================== 7. 构建应用并配置管道 =====================
	var app = builder.Build();

	// Register global exception handling middleware
	app.UseMiddleware<ExceptionHandlingMiddleware>();

	// Environment configuration
	if (!app.Environment.IsDevelopment())
	{
		app.UseExceptionHandler("/Home/Error");
		app.UseHsts();
		app.UseHttpsRedirection();
	}

	app.UseRouting();

	// Static files middleware
	app.UseStaticFiles();

	// Swagger configuration (development only)
	if (app.Environment.IsDevelopment())
	{
		app.UseSwagger();
		app.UseSwaggerUI(options =>
		{
			options.SwaggerEndpoint("/swagger/v1/swagger.json", "Saas.Infra.MVC API v1");
			options.RoutePrefix = string.Empty;
		});
	}

	// Authentication & Authorization
	app.UseAuthentication();
	app.UseAuthorization();

	app.MapStaticAssets();
	app.MapControllerRoute(
		name: "default",
		pattern: "{controller=Home}/{action=Index}/{id?}")
		.WithStaticAssets();

	Log.Information("Application started successfully, listening on: {Urls}", string.Join("; ", app.Urls));
	app.Run();
}
catch (Exception ex)
{
	Log.Fatal(ex, "Application startup failed");
}
finally
{
	Log.CloseAndFlush();
}
