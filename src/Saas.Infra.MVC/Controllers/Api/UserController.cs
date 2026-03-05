using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saas.Infra.Core;
using Saas.Infra.MVC.Models;
using Serilog;
using System.Security.Claims;

namespace Saas.Infra.MVC.Controllers.Api
{
    /// <summary>
    /// 提供用户相关的受保护 API（个人资料、修改密码等）。
    /// Protected user management endpoints (profile, change password, ...).
    /// </summary>
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly SSO.ISsoService _ssoService;

        /// <summary>
        /// 初始化 <see cref="UserController"/> 的新实例。
        /// Initializes a new instance of the <see cref="UserController"/> class.
        /// </summary>
        /// <param name="userRepository">用户仓储实现。/ The user repository implementation.</param>
        /// <param name="passwordHasher">密码哈希实现。/ The password hasher implementation.</param>
        /// <param name="ssoService">单点登录服务，用于生成 Token 等。/ The SSO service used to generate tokens.</param>
        public UserController(IUserRepository userRepository, IPasswordHasher passwordHasher, SSO.ISsoService ssoService)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _ssoService = ssoService ?? throw new ArgumentNullException(nameof(ssoService));
        }

        /// <summary>
        /// 获取当前登录用户的基本信息。
        /// Get current authenticated user's basic profile.
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var username = User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrWhiteSpace(username))
            {
                Log.Warning("Me endpoint called but no username claim present");
                return Unauthorized();
            }

            var user = await _userRepository.GetByUsernameAsync(username);
            if (user == null)
            {
                Log.Warning("Authenticated user not found in database: {Username}", username);
                return NotFound();
            }

            // Return safe DTO
            return Ok(new
            {
                user.Id,
                user.Username,
                // user.DisplayName,
                user.CreatedTime
            });
        }

        /// <summary>
        /// 修改当前用户密码。
        /// Change password for current authenticated user.
        /// </summary>
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (request == null) return BadRequest();
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var username = User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrWhiteSpace(username))
            {
                Log.Warning("ChangePassword called but no username claim present");
                return Unauthorized();
            }

            var user = await _userRepository.GetByUsernameAsync(username);
            if (user == null)
            {
                Log.Warning("ChangePassword: user not found {Username}", username);
                return NotFound();
            }

            // verify old password
            if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.OldPassword))
            {
                Log.Warning("ChangePassword: invalid old password for {Username}", username);
                return BadRequest(new { message = "Old password is incorrect" });
            }

            var newHash = _passwordHasher.HashPassword(request.NewPassword);
            await _userRepository.UpdatePasswordAsync(user.Id, newHash);

            Log.Information("Password changed for user {Username}", username);
            return NoContent();
        }

        /// <summary>
        /// 为新用户注册并返回自动登录的令牌对。
        /// Registers a new user and returns tokens for immediate login.
        /// </summary>
        /// <param name="request">包含电子邮件、密码和可选用户名的注册请求。/ The registration request containing email, password and optional username.</param>
        /// <returns>返回 <see cref="JwtTokenResponse"/> 包含 access/refresh token。/ Returns <see cref="JwtTokenResponse"/> with access/refresh tokens.</returns>
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // 参数有效性校验
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!ModelState.IsValid)
            {
                Log.Warning("Invalid model state for register request");
                return BadRequest(ModelState);
            }

            try
            {
                var tokenResponse = await _ssoService.RegisterUserAsync(request.Email, request.Password, request.Username, request.ClientId ?? "default");
                Log.Information("User registered successfully with email: {Email}", request.Email);
                return Ok(tokenResponse);
            }
            catch (InvalidOperationException ex)
            {
                // e.g. user already exists
                Log.Warning(ex, "Register failed for email: {Email}", request.Email);
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during user registration for {Email}", request.Email);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred during registration, please try again later" });
            }
        }
    }
}
