using Microsoft.AspNetCore.Identity.Data;
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
	/// 提供单点登录(SSO)相关的API端点。处理用户认证和JWT令牌生成请求。
	/// Provides API endpoints related to Single Sign-On (SSO), handling user authentication and JWT token generation requests.
	/// </summary>
	[ApiController]
	[Route("sso")]
	public class SsoController : ControllerBase
	{
		private readonly IConfiguration _config;
		private readonly ILogger<SsoController> _logger;

		/// <summary>
		/// 初始化 <see cref="SsoController"/> 类的新实例。 / Initializes a new instance of the <see cref="SsoController"/> class.
		/// </summary>
		/// <param name="config">应用程序配置实例，用于读取JWT配置。 / The application configuration instance used to read JWT settings.</param>
		/// <param name="logger">日志记录器实例，用于记录操作日志。 / The logger instance used to record operational logs.</param>
		/// <exception cref="ArgumentNullException">当 <paramref name="config"/> 或 <paramref name="logger"/> 为null时抛出。 / Thrown when <paramref name="config"/> or <paramref name="logger"/> is null.</exception>
		public SsoController(IConfiguration config, ILogger<SsoController> logger)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		/// <summary>
		/// 处理用户登录请求并生成JWT令牌。验证用户凭据后，生成包含用户信息的JWT访问令牌和刷新令牌。
		/// Handles user login requests and generates JWT tokens. After validating credentials, it generates an access token and a refresh token containing user information.
		/// </summary>
		/// <param name="request">包含用户凭据的登录请求对象。 / The login request object containing user credentials.</param>
		/// <returns>
		/// 成功时返回200 OK和JWT令牌信息；凭据无效时返回401 Unauthorized；请求无效时返回400 Bad Request；服务器错误时返回500 Internal Server Error。
		/// Returns 200 OK with JWT token info on success; 401 Unauthorized for invalid credentials; 400 Bad Request for invalid request; 500 Internal Server Error for server errors.
		/// </returns>
		/// <exception cref="ArgumentNullException">当 <paramref name="request"/> 为null时抛出。 / Thrown when <paramref name="request"/> is null.</exception>
		[HttpPost("generate-token")]
		[ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK)]
		[ProducesResponseType(StatusCodes.Status400BadRequest)]
		[ProducesResponseType(StatusCodes.Status401Unauthorized)]
		[ProducesResponseType(StatusCodes.Status500InternalServerError)]
		public IActionResult GenerateToken([FromBody] LoginRequest request)
		{
			// 参数验证
			if (request == null)
			{
				throw new ArgumentNullException(nameof(request));
			}

			if (!ModelState.IsValid)
			{
				_logger.LogWarning("Invalid model state for login request");
				return BadRequest(ModelState);
			}

			try
			{
				// 1. 模拟用户验证（实际项目替换为数据库/第三方验证）
				if (request.Username != "test" || request.Password != "123456")
				{
					_logger.LogWarning("Login failed for user: {Username}", request.Username);
					return Unauthorized(new { message = "用户名或密码错误" });
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
					new { message = "生成令牌时发生错误" });
			}
		}
	}


	/// <summary>
	/// 表示用户登录请求的数据传输对象。 / Represents the data transfer object for a user login request.
	/// </summary>
	public class LoginRequest
	{
		/// <summary>
		/// 获取或设置用户名。 / Gets or sets the username.
		/// </summary>
		/// <value>用户的登录名称。 / The username used for login.</value>
		[Required(ErrorMessage = "用户名不能为空")]
		[StringLength(100, MinimumLength = 3, ErrorMessage = "用户名长度必须在3到100个字符之间")]
		public string Username { get; set; } = string.Empty;

		/// <summary>
		/// 获取或设置密码。 / Gets or sets the password.
		/// </summary>
		/// <value>用户的登录密码。 / The password used for login.</value>
		[Required(ErrorMessage = "密码不能为空")]
		[StringLength(100, MinimumLength = 6, ErrorMessage = "密码长度必须在6到100个字符之间")]
		public string Password { get; set; } = string.Empty;

		/// <summary>
		/// 获取或设置客户端标识符。 / Gets or sets the client identifier.
		/// </summary>
		/// <value>可选的客户端ID，用于标识请求来源。 / Optional client ID used to identify the request source.</value>
		public string? ClientId { get; set; }
	}
}
