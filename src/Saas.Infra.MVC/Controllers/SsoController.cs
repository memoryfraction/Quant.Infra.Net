using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Saas.Infra.Core;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Saas.Infra.MVC.Controllers
{
	/// <summary>
	/// Provides API endpoints for Single Sign-On (SSO) operations
	/// 提供单点登录(SSO)相关的API端点
	/// </summary>
	[ApiController]
	[Route("sso")]
	public class SsoController : ControllerBase
	{
		/// <summary>
		/// Application configuration instance
		/// 应用程序配置实例
		/// </summary>
		private readonly IConfiguration _config;

		/// <summary>
		/// Logger instance for recording operational logs
		/// 日志记录器实例，用于记录操作日志
		/// </summary>
		private readonly ILogger<SsoController> _logger;

		/// <summary>
		/// Constructor for dependency injection
		/// 构造函数用于依赖注入
		/// </summary>
		/// <param name="config">Application configuration instance / 应用程序配置实例</param>
		/// <param name="logger">Logger instance / 日志记录器实例</param>
		/// <exception cref="ArgumentNullException">Thrown when config or logger is null / 当config或logger为null时抛出</exception>
		public SsoController(IConfiguration config, ILogger<SsoController> logger)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config), "IConfiguration cannot be null");
			_logger = logger ?? throw new ArgumentNullException(nameof(logger), "ILogger cannot be null");
		}

		/// <summary>
		/// Handles user login requests and generates JWT tokens
		/// 处理用户登录请求并生成JWT令牌
		/// </summary>
		/// <param name="request">Login request containing user credentials / 包含用户凭据的登录请求</param>
		/// <returns>
		/// 200 OK with JWT token info on success
		/// 401 Unauthorized for invalid credentials
		/// 400 Bad Request for invalid request
		/// 500 Internal Server Error for server errors
		/// </returns>
		/// <exception cref="ArgumentNullException">Thrown when request is null / 当request为null时抛出</exception>
		[HttpPost("generate-token")]
		[ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(StatusCodes.Status500InternalServerError)]
		public IActionResult GenerateToken([FromBody] LoginRequest request)
		{
			// Parameter validation
			if (request == null)
			{
				throw new ArgumentNullException(nameof(request), "LoginRequest cannot be null");
			}

			if (!ModelState.IsValid)
			{
				_logger.LogWarning("Invalid model state for login request");
				return BadRequest(ModelState);
			}

			try
			{
				// Validate user credentials (mock implementation - replace with database/third-party validation in production)
				if (request.Username != "test" || request.Password != "123456")
				{
					_logger.LogWarning("Login failed for user: {Username}", request.Username);
					return Unauthorized(new { message = "Invalid username or password" });
				}

				// Build JWT Claims
				var claims = new[]
				{
					new Claim(JwtRegisteredClaimNames.Sub, request.Username),
					new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
					new Claim("client_id", request.ClientId ?? "default"),
					new Claim(ClaimTypes.Role, "User")
				};

				// Generate JWT Token
				var signingKey = _config["Jwt:SigningKey"];
				if (string.IsNullOrEmpty(signingKey))
				{
					throw new InvalidOperationException("Jwt:SigningKey is not configured");
				}

				var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
				var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
				var expirationMinutes = int.Parse(_config["Jwt:AccessTokenExpirationMinutes"] ?? "60");
				var expires = DateTime.UtcNow.AddMinutes(expirationMinutes);

				var token = new JwtSecurityToken(
					issuer: _config["Jwt:Issuer"],
					audience: _config["Jwt:Audience"],
					claims: claims,
					expires: expires,
					signingCredentials: creds);

				// Return Token Response
				var tokenResponse = new JwtTokenResponse
				{
					AccessToken = new JwtSecurityTokenHandler().WriteToken(token),
					RefreshToken = Guid.NewGuid().ToString(),
					ExpiresIn = (int)(expires - DateTime.UtcNow).TotalSeconds
				};

				_logger.LogInformation("Token generated successfully for user: {Username}", request.Username);
				return Ok(tokenResponse);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during token generation for user: {Username}", request.Username);
				return StatusCode(StatusCodes.Status500InternalServerError, 
					new { message = "Error generating token" });
			}
		}
	}

	/// <summary>
	/// Data transfer object for user login request
	/// 用户登录请求的数据传输对象
	/// </summary>
	public class LoginRequest
	{
		/// <summary>
		/// Gets or sets the username
		/// 获取或设置用户名
		/// </summary>
		[Required(ErrorMessage = "Username is required")]
		[StringLength(100, MinimumLength = 3, ErrorMessage = "Username length must be between 3 and 100 characters")]
		public string Username { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the password
		/// 获取或设置密码
		/// </summary>
		[Required(ErrorMessage = "Password is required")]
		[StringLength(100, MinimumLength = 6, ErrorMessage = "Password length must be between 6 and 100 characters")]
		public string Password { get; set; } = string.Empty;

		/// <summary>
		/// Gets or sets the optional client identifier
		/// 获取或设置可选的客户端标识符
		/// </summary>
		public string? ClientId { get; set; }
	}
}
