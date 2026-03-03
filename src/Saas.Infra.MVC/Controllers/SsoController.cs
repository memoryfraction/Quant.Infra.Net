using Microsoft.AspNetCore.Mvc;
using Saas.Infra.Core;
using Saas.Infra.MVC.Models;
using Serilog;
using System.ComponentModel.DataAnnotations;

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
		private readonly Saas.Infra.SSO.ISsoService _ssoService;

		/// <summary>
		/// 初始化 <see cref="SsoController"/> 类的新实例。 / Initializes a new instance of the <see cref="SsoController"/> class.
		/// </summary>
        /// <param name="config">应用程序配置实例，用于读取JWT配置。 / The application configuration instance used to read JWT settings.</param>
        /// <param name="ssoService">SSO 服务实例，用于处理令牌生成与验证。 / The SSO service instance used to handle token generation and validation.</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="config"/> 或 <paramref name="ssoService"/> 为null时抛出。 / Thrown when <paramref name="config"/> or <paramref name="ssoService"/> is null.</exception>
        public SsoController(IConfiguration config, Saas.Infra.SSO.ISsoService ssoService)
		{
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _ssoService = ssoService ?? throw new ArgumentNullException(nameof(ssoService));
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
		public async Task<IActionResult> GenerateToken([FromBody] LoginRequest request)
		{
			// 参数验证
			if (request == null)
			{
				throw new ArgumentNullException(nameof(request));
			}

            if (!ModelState.IsValid)
            {
                Log.Warning("Invalid model state for login request");
                return BadRequest(ModelState);
            }

            // todo 读取数据库，校验request， 如果失败，则日志 + 返回;  是否需要生成一个方法？

            try
            {               
                JwtTokenResponse tokenResponse;
				try
				{
					tokenResponse = await _ssoService.GenerateTokensAsync(request.Username, request.Password, request.ClientId ?? "default");
				}
				catch (InvalidOperationException)
				{
					Log.Warning("Login failed for user: {Username}", request.Username);
					return Unauthorized(new { message = "用户名或密码错误" });
				}

                Log.Information("Token generated successfully for user: {Username}", request.Username);
				return Ok(tokenResponse);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error during token generation for user: {Username}", request.Username);
				return StatusCode(StatusCodes.Status500InternalServerError, new { message = "生成令牌时发生错误" });
			}
		}
	}


	
}
