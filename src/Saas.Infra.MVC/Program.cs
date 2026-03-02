using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Saas.Infra.Core;
using System.Text;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// ===================== 1. 读取JWT配置 =====================
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
	?? throw new ArgumentNullException("Jwt:SigningKey 未配置，请检查appsettings.json");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? JwtConstants.Issuer;
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Saas.Infra.Clients";

// ===================== 2. 注册JWT认证服务（核心保留） =====================
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

	options.Events = new JwtBearerEvents
	{
		OnAuthenticationFailed = context =>
		{
			Console.WriteLine($"JWT认证失败：{context.Exception.Message}");
			return Task.CompletedTask;
		}
	};
});

// ===================== 3. 极简Swagger配置（只保留基础文档，避开复杂安全配置） =====================
builder.Services.AddEndpointsApiExplorer(); // Swagger必需
builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new() { Title = "Saas.Infra.MVC API", Version = "v1" });
	// ✅ 先把所有 AddSecurityDefinition/AddSecurityRequirement 全部注释掉！
});

// ===================== 4. 注册基础服务 =====================
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();

// ===================== 5. 构建应用并配置管道 =====================
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// 启用Swagger（仅开发环境，极简配置）
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI(options =>
	{
		options.SwaggerEndpoint("/swagger/v1/swagger.json", "Saas.Infra.MVC API v1");
		options.RoutePrefix = string.Empty; // Swagger作为首页
	});
}

// 核心：认证→授权
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Home}/{action=Index}/{id?}")
	.WithStaticAssets();

app.Run();