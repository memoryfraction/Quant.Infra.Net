using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Saas.Infra.Core;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Saas.Infra.MVC.Controllers
{
	[ApiController]
	[Route("sso")]
	public class SsoController : ControllerBase
	{
		private readonly IConfiguration _config;

		public SsoController(IConfiguration config)
		{
			_config = config;
		}

		// 测试用Token生成接口（实际项目替换为登录验证逻辑）
		[HttpPost("generate-token")]
		public IActionResult GenerateToken([FromBody] LoginRequest request)
		{
			// 1. 模拟用户验证（实际项目替换为数据库/第三方验证）
			if (request.Username != "test" || request.Password != "123456")
			{
				return Unauthorized("用户名或密码错误");
			}

			// 2. 构建JWT Claims（用户信息）
			var claims = new[]
			{
			new Claim(JwtRegisteredClaimNames.Sub, request.Username), // 用户唯一标识
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // 唯一Token ID
            new Claim("client_id", request.ClientId ?? "default"), // 自定义Claim
            new Claim(ClaimTypes.Role, "User") // 角色Claim
        };

			// 3. 生成JWT Token
			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:SigningKey"]!));
			var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
			var expires = DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:AccessTokenExpirationMinutes"] ?? "60"));

			var token = new JwtSecurityToken(
				issuer: _config["Jwt:Issuer"],
				audience: _config["Jwt:Audience"],
				claims: claims,
				expires: expires,
				signingCredentials: creds);

			// 4. 返回Token（匹配Core层的JwtTokenResponse）
			return Ok(new JwtTokenResponse
			{
				AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
				RefreshToken = Guid.NewGuid().ToString(),
				ExpiresIn = (int)(expires - DateTime.UtcNow).TotalSeconds
			});
		}
	}


	// 辅助类：登录请求DTO
	public class LoginRequest
	{
		public string Username { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
		public string? ClientId { get; set; }
	}
}
