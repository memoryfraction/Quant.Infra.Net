using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saas.Infra.Core;
using Saas.Infra.MVC.Models;
using Serilog;
using System.Security.Claims;

namespace Saas.Infra.MVC.Controllers.Api
{
    /// <summary>
    /// 提供用户相关的受保护 API（个人资料、修改密码等），需验证RSA签名的JWT令牌。
    /// Protected user management endpoints (profile, change password, ...) requiring RSA-signed JWT token validation.
    /// </summary>
    [ApiController]
    [Route("api/users")]
    [Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
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
        /// <param name="ssoService">单点登录服务，用于生成RSA签名的Token等。/ The SSO service used to generate RSA-signed tokens.</param>
        public UserController(IUserRepository userRepository, IPasswordHasher passwordHasher, SSO.ISsoService ssoService)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _ssoService = ssoService ?? throw new ArgumentNullException(nameof(ssoService));
        }

        /// <summary>
        /// 获取当前登录用户的基本信息（从RSA JWT令牌中解析Claim）。
        /// Get current authenticated user's basic profile (parsed from RSA JWT token claims).
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            // 适配RSA JWT的Claim读取：优先读Sub（JWT标准），再读Name
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            var username = User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.PreferredUsername);

            // 双重校验：确保用户标识存在
            if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(username))
            {
                Log.Warning("Me endpoint called but no user identifier claim present (userId/username)");
                return Unauthorized(new { message = "Invalid token: missing user identifier" });
            }

            // 优先用ID查库；如果ID不是有效GUID（例如JWT的sub为非UUID的用户名），则退回按用户名查找
            Saas.Infra.Core.User? user = null;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                if (Guid.TryParse(userId, out var parsedId))
                {
                    user = await _userRepository.GetByIdAsync(parsedId);
                }
                else
                {
                    // userId 不是GUID，可能是用户名（某些令牌将sub设置为username），尝试按用户名查找
                    user = await _userRepository.GetByUsernameAsync(userId);
                }
            }

            if (user == null && !string.IsNullOrWhiteSpace(username))
            {
                user = await _userRepository.GetByUsernameAsync(username);
            }

            if (user == null)
            {
                Log.Warning("Authenticated user not found in database: UserId={UserId}, Username={Username}", userId, username);
                return NotFound(new { message = "User not found" });
            }

            // 返回安全的用户信息DTO
            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email, // 补充Email（按需）
                user.CreatedTime
            });
        }

        /// <summary>
        /// 修改当前用户密码（验证RSA JWT令牌后操作）。
        /// Change password for current authenticated user (after RSA JWT token validation).
        /// </summary>
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (request == null) return BadRequest("Request cannot be null.");
            if (string.IsNullOrWhiteSpace(request.OldPassword)) return BadRequest("Old password is required.");
            if (string.IsNullOrWhiteSpace(request.NewPassword)) return BadRequest("New password is required.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // 读取JWT中的用户标识
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrWhiteSpace(userId))
            {
                Log.Warning("ChangePassword called but no user ID claim present");
                return Unauthorized(new { message = "Invalid token: missing user ID" });
            }

            Saas.Infra.Core.User? user = null;
            if (Guid.TryParse(userId, out var parsedId))
            {
                user = await _userRepository.GetByIdAsync(parsedId);
            }
            else
            {
                // 如果claim中的userId不是GUID，尝试按用户名查找
                user = await _userRepository.GetByUsernameAsync(userId);
            }

            if (user == null)
            {
                Log.Warning("ChangePassword: user not found (UserId={UserId})", userId);
                return NotFound(new { message = "User not found" });
            }

            // 验证旧密码
            if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.OldPassword))
            {
                Log.Warning("ChangePassword: invalid old password for UserId={UserId}", userId);
                return BadRequest(new { message = "Old password is incorrect" });
            }

            // 更新密码
            var newHash = _passwordHasher.HashPassword(request.NewPassword);
            await _userRepository.UpdatePasswordAsync(user.Id, newHash);

            Log.Information("Password changed successfully for UserId={UserId}", userId);
            return NoContent();
        }

        /// <summary>
        /// 为新用户注册并返回RSA签名的自动登录令牌对。
        /// Registers a new user and returns RSA-signed tokens for immediate login.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // 参数有效性校验
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Email)) return BadRequest("Email is required.");
            if (string.IsNullOrWhiteSpace(request.Password)) return BadRequest("Password is required.");
            if (!ModelState.IsValid)
            {
                Log.Warning("Invalid model state for register request (Email={Email})", request.Email);
                return BadRequest(ModelState);
            }

            try
            {
                var tokenResponse = await _ssoService.RegisterUserAsync(
                    request.Email,
                    request.Password,
                    request.Username ?? request.Email.Split('@')[0], // 用户名默认值
                    request.ClientId ?? "default");

                Log.Information("User registered successfully (Email={Email}), RSA token generated", request.Email);
                return Ok(tokenResponse);
            }
            catch (InvalidOperationException ex)
            {
                // 如：用户已存在
                Log.Warning(ex, "Register failed for email: {Email}", request.Email);
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during user registration (Email={Email})", request.Email);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred during registration, please try again later" });
            }
        }
    }
}