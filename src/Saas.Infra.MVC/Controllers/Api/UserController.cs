using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Infra.Core;
using Saas.Infra.Data;
using Saas.Infra.MVC.Models;
using Saas.Infra.MVC.Models.Requests;
using Saas.Infra.MVC.Models.Responses;
using Saas.Infra.MVC.Security;
using Saas.Infra.Services.Sso;
using Serilog;
using System.Security.Claims;

namespace Saas.Infra.MVC.Controllers.Api
{
    /// <summary>
    /// æä¾›ç”¨æˆ·ç›¸å…³çš„å—ä¿æŠ¤ APIï¼ˆä¸ªäººèµ„æ–™ã€ä¿®æ”¹å¯†ç ç­‰ï¼‰ï¼Œéœ€éªŒè¯RSAç­¾åçš„JWTä»¤ç‰Œã€‚
    /// Protected user management endpoints (profile, change password, and admin management) requiring RSA-signed JWT token validation.
    /// </summary>
    [ApiController]
    [Route("api/users")]
    [Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
    public class UserController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ISsoService _ssoService;
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// åˆå§‹åŒ– <see cref="UserController"/> çš„æ–°å®žä¾‹ã€‚
        /// Initializes a new instance of the <see cref="UserController"/> class.
        /// </summary>
        /// <param name="userRepository">ç”¨æˆ·ä»“å‚¨å®žçŽ°ã€‚/ The user repository implementation.</param>
        /// <param name="passwordHasher">å¯†ç å“ˆå¸Œå®žçŽ°ã€‚/ The password hasher implementation.</param>
        /// <param name="ssoService">å•ç‚¹ç™»å½•æœåŠ¡ï¼Œç”¨äºŽç”ŸæˆRSAç­¾åçš„Tokenç­‰ã€‚/ The SSO service used to generate RSA-signed tokens.</param>
        /// <param name="db">æ•°æ®åº“ä¸Šä¸‹æ–‡ã€‚ / Database context.</param>
        public UserController(IUserRepository userRepository, IPasswordHasher passwordHasher, ISsoService ssoService, ApplicationDbContext db)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
            _ssoService = ssoService ?? throw new ArgumentNullException(nameof(ssoService));
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// èŽ·å–å½“å‰ç™»å½•ç”¨æˆ·çš„åŸºæœ¬ä¿¡æ¯ï¼ˆä»ŽRSA JWTä»¤ç‰Œä¸­è§£æžClaimï¼‰ã€‚
        /// Get current authenticated user's basic profile (parsed from RSA JWT token claims).
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
            var username = User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.PreferredUsername);

            if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(username))
            {
                Log.Warning("Me endpoint called but no user identifier claim present (userId/username)");
                return Unauthorized(new { message = "Invalid token: missing user identifier" });
            }

            Saas.Infra.Core.User? user = null;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                if (Guid.TryParse(userId, out var parsedId))
                {
                    user = await _userRepository.GetByIdAsync(parsedId);
                }
                else
                {
                    user = await _userRepository.GetByEmailAsync(userId)
                        ?? await _userRepository.GetByUsernameAsync(userId);
                }
            }

            if (user == null && !string.IsNullOrWhiteSpace(username))
            {
                user = await _userRepository.GetByEmailAsync(username)
                    ?? await _userRepository.GetByUsernameAsync(username);
            }

            if (user == null)
            {
                Log.Warning("Authenticated user not found in database: UserId={UserId}, Username={Username}", userId, username);
                return NotFound(new { message = "User not found" });
            }

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.CreatedTime,
                user.Status
            });
        }

        /// <summary>
        /// èŽ·å–å¯ç®¡ç†çš„ç”¨æˆ·åˆ—è¡¨ã€‚
        /// Gets the list of users manageable by the current operator.
        /// </summary>
        /// <param name="includeDeleted">æ˜¯å¦åŒ…å«å·²åˆ é™¤ç”¨æˆ·ã€‚ / Whether to include deleted users.</param>
        /// <returns>ç”¨æˆ·ç®¡ç†åˆ—è¡¨ã€‚ / User management list.</returns>
        [HttpGet("management")]
        [AuthorizeRole(UserRole.Admin)]
        public async Task<IActionResult> GetManageableUsers([FromQuery] bool includeDeleted = false)
        {
            var currentRoleCode = GetCurrentOperatorRoleCode();
            var currentUserId = GetCurrentUserId();
            var isSuperAdmin = string.Equals(currentRoleCode, RoleCodes.SuperAdmin, StringComparison.OrdinalIgnoreCase);

            var usersQuery = _db.Users
                .AsNoTracking()
                .AsQueryable();

            if (!includeDeleted)
            {
                usersQuery = usersQuery.Where(u => !u.IsDeleted);
            }

            var users = await usersQuery
                .OrderBy(u => u.CreatedTime)
                .Select(u => new
                {
                    u.Id,
                    u.UserName,
                    u.Email,
                    u.Status,
                    u.CreatedTime,
                    u.IsDeleted
                })
                .ToListAsync();

            var userRoleLookup = await _db.UserRoles
                .AsNoTracking()
                .Join(
                    _db.Roles.AsNoTracking(),
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => new
                    {
                        userRole.UserId,
                        role.Code
                    })
                .ToListAsync();

            var roleMap = userRoleLookup
                .GroupBy(item => item.UserId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(item => GetRoleLevel(item.Code))
                        .Select(item => item.Code)
                        .FirstOrDefault() ?? RoleCodes.User);

            var result = users
                .Select(user =>
                {
                    var roleCode = roleMap.TryGetValue(user.Id, out var code) ? code : RoleCodes.User;
                    return new UserManagementDto
                    {
                        Id = user.Id,
                        UserName = user.UserName,
                        Email = user.Email,
                        Status = user.Status,
                        StatusText = user.IsDeleted ? "Deleted" : user.Status == (short)UserStatus.Enabled ? "Enabled" : "Disabled",
                        RoleCode = roleCode,
                        RoleDisplayName = GetRoleDisplayName(roleCode),
                        CreatedTime = user.CreatedTime,
                        CanManage = CanManageTarget(currentRoleCode, roleCode),
                        IsDeleted = user.IsDeleted,
                        IsCurrentUser = currentUserId == user.Id
                    };
                })
                .Where(user => isSuperAdmin || string.Equals(user.RoleCode, RoleCodes.User, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(user => user.CreatedTime)
                .ToList();

            Log.Information("User management list loaded by {Operator}, role {RoleCode}, count {Count}", User.Identity?.Name, currentRoleCode, result.Count);
            return Ok(result);
        }

        /// <summary>
        /// åˆ‡æ¢ç”¨æˆ·å¯ç”¨çŠ¶æ€ã€‚
        /// Toggles a user's enabled status.
        /// </summary>
        /// <param name="userId">ç”¨æˆ·IDã€‚ / User ID.</param>
        /// <returns>æ“ä½œç»“æžœã€‚ / Operation result.</returns>
        /// <exception cref="ArgumentException">å½“ userId æ— æ•ˆæ—¶æŠ›å‡ºã€‚ / Thrown when userId is invalid.</exception>
        [HttpPost("{userId:guid}/toggle-status")]
        [AuthorizeRole(UserRole.Admin)]
        public async Task<IActionResult> ToggleUserStatus(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("userId must be a valid UUID", nameof(userId));

            var currentRoleCode = GetCurrentOperatorRoleCode();
            var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (targetUser == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var targetRoleCode = await GetUserRoleCodeAsync(userId);
            if (!CanManageTarget(currentRoleCode, targetRoleCode))
            {
                Log.Warning("Operator {Operator} with role {CurrentRole} attempted to manage user {UserId} with role {TargetRole}", User.Identity?.Name, currentRoleCode, userId, targetRoleCode);
                return Forbid();
            }

            targetUser.Status = targetUser.Status == (short)UserStatus.Enabled
                ? (short)UserStatus.Disabled
                : (short)UserStatus.Enabled;
            targetUser.UpdatedTime = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            Log.Information("User {UserId} status changed to {Status} by {Operator}", userId, targetUser.Status, User.Identity?.Name);
            return Ok(new
            {
                message = "User status updated successfully",
                status = targetUser.Status
            });
        }

        /// <summary>
        /// æ›´æ–°å¯ç®¡ç†ç”¨æˆ·çš„åŸºæœ¬ä¿¡æ¯ã€‚
        /// Updates the basic information of a manageable user.
        /// </summary>
        /// <param name="userId">ç”¨æˆ·IDã€‚ / User ID.</param>
        /// <param name="request">æ›´æ–°è¯·æ±‚ã€‚ / Update request.</param>
        /// <returns>æ“ä½œç»“æžœã€‚ / Operation result.</returns>
        /// <exception cref="ArgumentException">å½“ userId æ— æ•ˆæ—¶æŠ›å‡ºã€‚ / Thrown when userId is invalid.</exception>
        [HttpPut("{userId:guid}")]
        [AuthorizeRole(UserRole.Admin)]
        public async Task<IActionResult> UpdateManagedUser(Guid userId, [FromBody] UpdateManagedUserRequest request)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("userId must be a valid UUID", nameof(userId));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.UserName))
                return BadRequest(new { message = "User name is required" });
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { message = "Email is required" });
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var currentRoleCode = GetCurrentOperatorRoleCode();
            var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (targetUser == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var targetRoleCode = await GetUserRoleCodeAsync(userId);
            if (!CanManageTarget(currentRoleCode, targetRoleCode))
            {
                Log.Warning("Operator {Operator} with role {CurrentRole} attempted to edit user {UserId} with role {TargetRole}", User.Identity?.Name, currentRoleCode, userId, targetRoleCode);
                return Forbid();
            }

            var normalizedUserName = request.UserName.Trim();
            var normalizedEmail = request.Email.Trim();

            var duplicateUserName = await _db.Users
                .AsNoTracking()
                .AnyAsync(u => !u.IsDeleted && u.Id != userId && u.UserName == normalizedUserName);
            if (duplicateUserName)
            {
                return Conflict(new { message = "User name already exists" });
            }

            var duplicateEmail = await _db.Users
                .AsNoTracking()
                .AnyAsync(u => !u.IsDeleted && u.Id != userId && u.Email == normalizedEmail);
            if (duplicateEmail)
            {
                return Conflict(new { message = "Email already exists" });
            }

            targetUser.UserName = normalizedUserName;
            targetUser.Email = normalizedEmail;
            targetUser.Status = request.Status;
            targetUser.UpdatedTime = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            Log.Information("User {UserId} updated by {Operator}", userId, User.Identity?.Name);
            return Ok(new { message = "User updated successfully" });
        }

        /// <summary>
        /// åˆ é™¤å¯ç®¡ç†ç”¨æˆ·ã€‚
        /// Deletes a manageable user.
        /// </summary>
        /// <param name="userId">ç”¨æˆ·IDã€‚ / User ID.</param>
        /// <returns>æ“ä½œç»“æžœã€‚ / Operation result.</returns>
        /// <exception cref="ArgumentException">å½“ userId æ— æ•ˆæ—¶æŠ›å‡ºã€‚ / Thrown when userId is invalid.</exception>
        [HttpDelete("{userId:guid}")]
        [AuthorizeRole(UserRole.Admin)]
        public async Task<IActionResult> DeleteManagedUser(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("userId must be a valid UUID", nameof(userId));

            var currentUserId = GetCurrentUserId();
            if (currentUserId == userId)
            {
                return BadRequest(new { message = "You cannot delete the current user" });
            }

            var currentRoleCode = GetCurrentOperatorRoleCode();
            var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted);
            if (targetUser == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var targetRoleCode = await GetUserRoleCodeAsync(userId);
            if (!CanManageTarget(currentRoleCode, targetRoleCode))
            {
                Log.Warning("Operator {Operator} with role {CurrentRole} attempted to delete user {UserId} with role {TargetRole}", User.Identity?.Name, currentRoleCode, userId, targetRoleCode);
                return Forbid();
            }

            if (string.Equals(targetRoleCode, RoleCodes.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Super admin users cannot be deleted" });
            }

            targetUser.IsDeleted = true;
            targetUser.Status = (short)UserStatus.Disabled;
            targetUser.UpdatedTime = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            Log.Information("User {UserId} deleted by {Operator}", userId, User.Identity?.Name);
            return Ok(new { message = "User deleted successfully" });
        }

        /// <summary>
        /// æ¢å¤å·²åˆ é™¤çš„å¯ç®¡ç†ç”¨æˆ·ã€‚
        /// Restores a deleted manageable user.
        /// </summary>
        /// <param name="userId">ç”¨æˆ·IDã€‚ / User ID.</param>
        /// <returns>æ“ä½œç»“æžœã€‚ / Operation result.</returns>
        /// <exception cref="ArgumentException">å½“ userId æ— æ•ˆæ—¶æŠ›å‡ºã€‚ / Thrown when userId is invalid.</exception>
        [HttpPost("{userId:guid}/restore")]
        [AuthorizeRole(UserRole.Admin)]
        public async Task<IActionResult> RestoreManagedUser(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("userId must be a valid UUID", nameof(userId));

            var currentRoleCode = GetCurrentOperatorRoleCode();
            var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsDeleted);
            if (targetUser == null)
            {
                return NotFound(new { message = "Deleted user not found" });
            }

            var targetRoleCode = await GetUserRoleCodeAsync(userId);
            if (!CanManageTarget(currentRoleCode, targetRoleCode))
            {
                Log.Warning("Operator {Operator} with role {CurrentRole} attempted to restore user {UserId} with role {TargetRole}", User.Identity?.Name, currentRoleCode, userId, targetRoleCode);
                return Forbid();
            }

            targetUser.IsDeleted = false;
            targetUser.Status = (short)UserStatus.Enabled;
            targetUser.UpdatedTime = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            Log.Information("User {UserId} restored by {Operator}", userId, User.Identity?.Name);
            return Ok(new { message = "User restored successfully" });
        }

        /// <summary>
        /// ä¿®æ”¹å½“å‰ç”¨æˆ·å¯†ç ï¼ˆéªŒè¯RSA JWTä»¤ç‰ŒåŽæ“ä½œï¼‰ã€‚
        /// Change password for current authenticated user (after RSA JWT token validation).
        /// </summary>
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (request == null) return BadRequest("Request cannot be null.");
            if (string.IsNullOrWhiteSpace(request.OldPassword)) return BadRequest("Old password is required.");
            if (string.IsNullOrWhiteSpace(request.NewPassword)) return BadRequest("New password is required.");
            if (!ModelState.IsValid) return BadRequest(ModelState);

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
                user = await _userRepository.GetByEmailAsync(userId)
                    ?? await _userRepository.GetByUsernameAsync(userId);
            }

            if (user == null)
            {
                Log.Warning("ChangePassword: user not found (UserId={UserId})", userId);
                return NotFound(new { message = "User not found" });
            }

            if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.OldPassword))
            {
                Log.Warning("ChangePassword: invalid old password for UserId={UserId}", userId);
                return BadRequest(new { message = "Old password is incorrect" });
            }

            var newHash = _passwordHasher.HashPassword(request.NewPassword);
            await _userRepository.UpdatePasswordAsync(user.Id, newHash);

            Log.Information("Password changed successfully for UserId={UserId}", userId);
            return NoContent();
        }

        /// <summary>
        /// ä¸ºæ–°ç”¨æˆ·æ³¨å†Œå¹¶è¿”å›žRSAç­¾åçš„è‡ªåŠ¨ç™»å½•ä»¤ç‰Œå¯¹ã€‚
        /// Registers a new user and returns RSA-signed tokens for immediate login.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
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
                    request.Username ?? request.Email.Split('@')[0],
                    request.ClientId ?? "default");

                Log.Information("User registered successfully (Email={Email}), RSA token generated", request.Email);
                return Ok(tokenResponse);
            }
            catch (InvalidOperationException ex)
            {
                Log.Warning(ex, "Register failed for email: {Email}", request.Email);
                return Conflict(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during user registration (Email={Email})", request.Email);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred during registration, please try again later" });
            }
        }

        /// <summary>
        /// æŽˆäºˆç”¨æˆ·ç®¡ç†å‘˜è§’è‰²ï¼ˆä»…è¶…çº§ç®¡ç†å‘˜ï¼‰ã€‚
        /// Grants admin role to a user (SuperAdmin only).
        /// </summary>
        /// <param name="userId">ç”¨æˆ·IDã€‚ / User ID.</param>
        /// <returns>æ“ä½œç»“æžœã€‚ / Operation result.</returns>
        /// <exception cref="ArgumentException">å½“ userId æ— æ•ˆæ—¶æŠ›å‡ºã€‚ / Thrown when userId is invalid.</exception>
        [HttpPost("{userId:guid}/grant-admin")]
        [AuthorizeRole(UserRole.Super_Admin)]
        public async Task<IActionResult> GrantAdminRole(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("userId must be a valid UUID", nameof(userId));

            var currentUserId = GetCurrentUserId();
            if (currentUserId == userId)
            {
                return BadRequest(new { message = "You cannot change the current user's admin role" });
            }

            var userExists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                return NotFound(new { message = "User not found" });
            }

            var currentRoleCode = await GetUserRoleCodeAsync(userId);
            if (string.Equals(currentRoleCode, RoleCodes.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "SUPER_ADMIN role cannot be changed" });
            }

            var adminRole = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Code == RoleCodes.Admin);
            if (adminRole == null)
            {
                return NotFound(new { message = "ADMIN role not found" });
            }

            var hasRole = await _db.UserRoles.AsNoTracking().AnyAsync(ur => ur.UserId == userId && ur.RoleId == adminRole.Id);
            if (hasRole)
            {
                return Ok(new { message = "User already has ADMIN role" });
            }

            _db.UserRoles.Add(new UserRoleEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RoleId = adminRole.Id,
                CreatedTime = DateTimeOffset.UtcNow
            });
            await _db.SaveChangesAsync();

            Log.Information("ADMIN role granted to user {UserId} by {Operator}", userId, User.Identity?.Name);
            return Ok(new { message = "ADMIN role granted successfully" });
        }

        /// <summary>
        /// ç§»é™¤ç”¨æˆ·ç®¡ç†å‘˜è§’è‰²ï¼ˆä»…è¶…çº§ç®¡ç†å‘˜ï¼‰ã€‚
        /// Revokes admin role from a user (SuperAdmin only).
        /// </summary>
        /// <param name="userId">ç”¨æˆ·IDã€‚ / User ID.</param>
        /// <returns>æ“ä½œç»“æžœã€‚ / Operation result.</returns>
        /// <exception cref="ArgumentException">å½“ userId æ— æ•ˆæ—¶æŠ›å‡ºã€‚ / Thrown when userId is invalid.</exception>
        [HttpPost("{userId:guid}/revoke-admin")]
        [AuthorizeRole(UserRole.Super_Admin)]
        public async Task<IActionResult> RevokeAdminRole(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("userId must be a valid UUID", nameof(userId));

            var currentUserId = GetCurrentUserId();
            if (currentUserId == userId)
            {
                return BadRequest(new { message = "You cannot revoke the current user's admin role" });
            }

            var adminRole = await _db.Roles.AsNoTracking().FirstOrDefaultAsync(r => r.Code == RoleCodes.Admin);
            if (adminRole == null)
            {
                return NotFound(new { message = "ADMIN role not found" });
            }

            var userRole = await _db.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == adminRole.Id);
            if (userRole == null)
            {
                return Ok(new { message = "User does not have ADMIN role" });
            }

            _db.UserRoles.Remove(userRole);
            await _db.SaveChangesAsync();

            Log.Information("ADMIN role revoked from user {UserId} by {Operator}", userId, User.Identity?.Name);
            return Ok(new { message = "ADMIN role revoked successfully" });
        }

        private string GetCurrentOperatorRoleCode()
        {
            if (User.Claims.Any(c => c.Type == ClaimTypes.Role && string.Equals(c.Value, RoleCodes.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
            {
                return RoleCodes.SuperAdmin;
            }

            if (User.Claims.Any(c => c.Type == ClaimTypes.Role && string.Equals(c.Value, RoleCodes.Admin, StringComparison.OrdinalIgnoreCase)))
            {
                return RoleCodes.Admin;
            }

            return RoleCodes.User;
        }

        private async Task<string> GetUserRoleCodeAsync(Guid userId)
        {
            var roleCodes = await _db.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .Join(
                    _db.Roles.AsNoTracking(),
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => role.Code)
                .ToListAsync();

            return roleCodes
                .OrderByDescending(GetRoleLevel)
                .FirstOrDefault() ?? RoleCodes.User;
        }

        private static bool CanManageTarget(string currentRoleCode, string targetRoleCode)
        {
            if (string.Equals(currentRoleCode, RoleCodes.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(currentRoleCode, RoleCodes.Admin, StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(targetRoleCode, RoleCodes.User, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private Guid GetCurrentUserId()
        {
            var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(claimValue, out var userId)
                ? userId
                : Guid.Empty;
        }

        private static int GetRoleLevel(string roleCode)
        {
            if (string.Equals(roleCode, RoleCodes.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (string.Equals(roleCode, RoleCodes.Admin, StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return 1;
        }

        private static string GetRoleDisplayName(string roleCode)
        {
            if (string.Equals(roleCode, RoleCodes.SuperAdmin, StringComparison.OrdinalIgnoreCase))
            {
                return "Super Admin";
            }

            if (string.Equals(roleCode, RoleCodes.Admin, StringComparison.OrdinalIgnoreCase))
            {
                return "Admin";
            }

            return "User";
        }
    }
}