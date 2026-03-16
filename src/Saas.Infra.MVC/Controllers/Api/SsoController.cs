using Microsoft.AspNetCore.Mvc;
using Saas.Infra.Core;
using Saas.Infra.Services.Sso;
using Saas.Infra.MVC.Models;
using Serilog.Events;

namespace Saas.Infra.MVC.Controllers.Api
{
    /// <summary>
    /// 提供单点登录(SSO)相关的API端点。处理用户认证和JWT令牌生成请求。
    /// Provides API endpoints related to Single Sign-On (SSO), handling user authentication and JWT token generation requests.
    /// </summary>
    [ApiController]
    [Route("sso")]
    public class SsoController : ControllerBase
    {
        private readonly ISsoService _ssoService;

        /// <summary>
        /// 初始化 <see cref="SsoController"/> 类的新实例。 / Initializes a new instance of the <see cref="SsoController"/> class.
        /// </summary>
        /// <param name="ssoService">SSO 服务实例，用于处理RSA签名的JWT令牌生成与验证。 / The SSO service instance used to handle RSA-signed JWT token generation and validation.</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="ssoService"/> 为null时抛出。 / Thrown when <paramref name="ssoService"/> is null.</exception>
        public SsoController(ISsoService ssoService)
        {
            _ssoService = ssoService ?? throw new ArgumentNullException(nameof(ssoService));
        }

        /// <summary>
        /// 处理用户登录请求并生成RSA签名的JWT令牌。验证用户凭据后，生成包含用户信息的JWT访问令牌和刷新令牌。
        /// Handles user login requests and generates RSA-signed JWT tokens. After validating credentials, it generates an access token and a refresh token containing user information.
        /// </summary>
        /// <param name="request">包含用户凭据的登录请求对象。 / The login request object containing user credentials.</param>
        /// <returns>
        /// 成功时返回200 OK和JWT令牌信息；凭据无效时返回401 Unauthorized；请求无效时返回400 Bad Request；服务器错误时返回500 Internal Server Error。
        /// Returns 200 OK with JWT token info on success; 401 Unauthorized for invalid credentials; 400 Bad Request for invalid request; 500 Internal Server Error for server errors.
        /// </returns>
        [HttpPost("generate-token")]
        [ProducesResponseType(typeof(JwtTokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GenerateToken([FromBody] LoginRequest request)
        {
            // 1. 基础参数验证 (防御性编程)
            if (request == null) return BadRequest("Request cannot be null.");
            if (string.IsNullOrWhiteSpace(request.Email)) return BadRequest("Email is required.");
            if (string.IsNullOrWhiteSpace(request.Password)) return BadRequest("Password is required.");

            if (!ModelState.IsValid)
            {
                UtilityService.LogAndWriteLine(LogEventLevel.Warning, "Invalid model state for login request: {Email}", request.Email);
                return BadRequest(ModelState);
            }

            try
            {
                // 2. 调用 SSO 服务生成RSA签名的Token（核心逻辑在ISsoService实现中）
                var tokenResponse = await _ssoService.GenerateTokensAsync(
                    request.Email,
                    request.Password,
                    request.ClientId ?? "default");

                UtilityService.LogAndWriteLine(LogEventLevel.Information, "RSA-signed token generated successfully for email: {Email}", request.Email);
                return Ok(tokenResponse);
            }
            catch (InvalidOperationException ex)
            {
                // 3. 处理预期的业务异常（返回具体的错误消息）
                UtilityService.LogAndWriteLine(LogEventLevel.Warning, "Login failed for email: {Email}. Reason: {Reason}", request.Email, ex.Message);
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                // 4. 处理未预期的系统异常
                UtilityService.LogAndWriteLine(ex, LogEventLevel.Error, "Unexpected error during RSA token generation for email: {Email}. Exception: {ExceptionMessage}", request.Email, ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Internal server error, please try again later" });
            }
        }
    }
}