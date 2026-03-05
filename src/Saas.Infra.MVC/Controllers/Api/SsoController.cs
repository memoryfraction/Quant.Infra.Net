using Microsoft.AspNetCore.Mvc;
using Saas.Infra.Core;
using Saas.Infra.MVC.Models;
using Serilog;

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
        private readonly SSO.ISsoService _ssoService;

        /// <summary>
        /// 初始化 <see cref="SsoController"/> 类的新实例。 / Initializes a new instance of the <see cref="SsoController"/> class.
        /// </summary>
        /// <param name="config">应用程序配置实例，用于读取JWT配置。 / The application configuration instance used to read JWT settings.</param>
        /// <param name="ssoService">SSO 服务实例，用于处理令牌生成与验证。 / The SSO service instance used to handle token generation and validation.</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="config"/> 或 <paramref name="ssoService"/> 为null时抛出。 / Thrown when <paramref name="config"/> or <paramref name="ssoService"/> is null.</exception>
        public SsoController(SSO.ISsoService ssoService)
        {
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
            // 1. 基础参数验证 (防御性编程)
            if (request == null) return BadRequest("Request cannot be null.");

            if (!ModelState.IsValid)
            {
                Log.Warning("Invalid model state for login request: {Username}", request.Username);
                return BadRequest(ModelState);
            }

            try
            {
                // 2. 调用 SSO 服务处理核心逻辑
                // 核心：数据库读取、密码哈希校验、状态检查都在 GenerateTokensAsync 内部完成
                var tokenResponse = await _ssoService.GenerateTokensAsync(
                    request.Username,
                    request.Password,
                    request.ClientId ?? "default");

                Log.Information("Token generated successfully for user: {Username}", request.Username);
                return Ok(tokenResponse);
            }
            catch (InvalidOperationException ex)
            {
                // 3. 处理预期的业务异常 (如：用户名密码不匹配、账号锁定等)
                // 统一返回 401，并隐藏具体细节以防攻击
                Log.Warning("Login failed for user: {Username}. Reason: {Reason}", request.Username, ex.Message);
                return Unauthorized(new { message = "Invalid username or password" });
            }
            catch (Exception ex)
            {
                // 4. 处理未预期的系统异常
                Log.Error(ex, "Unexpected error during token generation for user: {Username}", request.Username);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Internal server error, please try again later" });
            }
        }



    }
}
