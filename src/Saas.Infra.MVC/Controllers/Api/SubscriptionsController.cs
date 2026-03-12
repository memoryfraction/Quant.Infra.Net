using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Infra.Data;
using Saas.Infra.MVC.Models.Responses;
using Serilog;

namespace Saas.Infra.MVC.Controllers.Api
{
    /// <summary>
    /// 订阅管理API控制器（用户）。
    /// Subscription management API controller (User).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SubscriptionsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// 初始化<see cref="SubscriptionsController"/>的新实例。
        /// Initializes a new instance of the <see cref="SubscriptionsController"/> class.
        /// </summary>
        /// <param name="db">数据库上下文。 / Database context.</param>
        /// <exception cref="ArgumentNullException">当db为null时抛出。 / Thrown when db is null.</exception>
        public SubscriptionsController(ApplicationDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// 获取当前用户的所有订阅。
        /// Gets all subscriptions for the current user.
        /// </summary>
        /// <param name="activeOnly">是否仅查看激活的订阅。 / Whether to show only active subscriptions.</param>
        /// <returns>订阅列表。 / List of subscriptions.</returns>
        [HttpGet("my")]
        public async Task<IActionResult> GetMySubscriptions([FromQuery] bool activeOnly = false)
        {
            try
            {
                // 获取当前用户ID
                var userId = await GetCurrentUserIdAsync();
                if (userId == Guid.Empty)
                {
                    Log.Warning("User ID not found in claims for subscription retrieval");
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var query = _db.Subscriptions
                    .AsNoTracking()
                    .Include(s => s.Product)
                    .Include(s => s.Price)
                    .Where(s => s.UserId == userId && !s.IsDeleted);

                // 如果仅查看激活的订阅
                if (activeOnly)
                {
                    query = query.Where(s => s.Status == 1);
                }

                var subscriptions = await query
                    .OrderByDescending(s => s.CreatedTime)
                    .Select(s => new SubscriptionDto
                    {
                        Id = s.Id,
                        UserId = s.UserId,
                        ProductId = s.ProductId,
                        ProductCode = s.Product != null ? s.Product.Code : null,
                        ProductName = s.Product != null ? s.Product.Name : null,
                        PriceId = s.PriceId,
                        PriceName = s.Price != null ? s.Price.Name : null,
                        BillingPeriod = s.Price != null ? s.Price.BillingPeriod : null,
                        Status = s.Status,
                        StartDate = s.StartDate,
                        EndDate = s.EndDate,
                        AutoRenew = s.AutoRenew,
                        Amount = s.Price != null ? s.Price.Amount : 0,
                        Currency = s.Price != null ? s.Price.Currency : "USD",
                        CreatedTime = s.CreatedTime,
                        IsDeleted = s.IsDeleted
                    })
                    .ToListAsync();

                Log.Information("Retrieved {Count} subscriptions for user {UserId}", subscriptions.Count, userId);
                return Ok(subscriptions);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving user subscriptions");
                return StatusCode(500, new { message = "Failed to retrieve subscriptions" });
            }
        }

        /// <summary>
        /// 根据ID获取订阅详情（仅限本人或管理员）。
        /// Gets subscription details by ID (only for owner or admin).
        /// </summary>
        /// <param name="id">订阅ID。 / Subscription ID.</param>
        /// <returns>订阅详情。 / Subscription details.</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSubscription(Guid id)
        {
            if (id == Guid.Empty)
                return BadRequest(new { message = "Invalid subscription ID" });

            try
            {
                var userId = await GetCurrentUserIdAsync();
                var isAdmin = User.IsInRole("ADMIN") || User.IsInRole("SUPER_ADMIN");

                var subscription = await _db.Subscriptions
                    .AsNoTracking()
                    .Include(s => s.Product)
                    .Include(s => s.Price)
                    .Where(s => s.Id == id)
                    .FirstOrDefaultAsync();

                if (subscription == null)
                {
                    Log.Warning("Subscription {Id} not found", id);
                    return NotFound(new { message = "Subscription not found" });
                }

                // 权限检查：只有本人或管理员可以查看
                if (!isAdmin && subscription.UserId != userId)
                {
                    Log.Warning("User {UserId} attempted to access subscription {SubscriptionId} belonging to {OwnerId}",
                        userId, id, subscription.UserId);
                    return Forbid();
                }

                var dto = new SubscriptionDto
                {
                    Id = subscription.Id,
                    UserId = subscription.UserId,
                    ProductId = subscription.ProductId,
                    ProductCode = subscription.Product?.Code,
                    ProductName = subscription.Product?.Name,
                    PriceId = subscription.PriceId,
                    PriceName = subscription.Price?.Name,
                    BillingPeriod = subscription.Price?.BillingPeriod,
                    Status = subscription.Status,
                    StartDate = subscription.StartDate,
                    EndDate = subscription.EndDate,
                    AutoRenew = subscription.AutoRenew,
                    Amount = subscription.Price?.Amount ?? 0,
                    Currency = subscription.Price?.Currency ?? "USD",
                    CreatedTime = subscription.CreatedTime,
                    IsDeleted = subscription.IsDeleted
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving subscription {Id}", id);
                return StatusCode(500, new { message = "Failed to retrieve subscription" });
            }
        }

        /// <summary>
        /// 取消订阅（设置状态为已取消，关闭自动续费）。
        /// Cancels a subscription (sets status to cancelled, disables auto-renew).
        /// </summary>
        /// <param name="id">订阅ID。 / Subscription ID.</param>
        /// <returns>取消结果。 / Cancellation result.</returns>
        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelSubscription(Guid id)
        {
            if (id == Guid.Empty)
                return BadRequest(new { message = "Invalid subscription ID" });

            try
            {
                var userId = await GetCurrentUserIdAsync();
                var isAdmin = User.IsInRole("ADMIN") || User.IsInRole("SUPER_ADMIN");

                var subscription = await _db.Subscriptions.FindAsync(id);

                if (subscription == null)
                {
                    Log.Warning("Subscription {Id} not found for cancellation", id);
                    return NotFound(new { message = "Subscription not found" });
                }

                // 权限检查：只有本人或管理员可以取消
                if (!isAdmin && subscription.UserId != userId)
                {
                    Log.Warning("User {UserId} attempted to cancel subscription {SubscriptionId} belonging to {OwnerId}",
                        userId, id, subscription.UserId);
                    return Forbid();
                }

                // 检查订阅状态
                if (subscription.Status == 2)
                {
                    Log.Warning("Subscription {Id} already cancelled", id);
                    return BadRequest(new { message = "Subscription is already cancelled" });
                }

                if (subscription.Status == 3)
                {
                    Log.Warning("Subscription {Id} already expired", id);
                    return BadRequest(new { message = "Subscription is already expired" });
                }

                // 更新订阅状态
                subscription.Status = 2; // Cancelled
                subscription.AutoRenew = false;
                await _db.SaveChangesAsync();

                Log.Information("Subscription {Id} cancelled by user {UserId}", id, userId);
                return Ok(new { message = "Subscription cancelled successfully" });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error cancelling subscription {Id}", id);
                return StatusCode(500, new { message = "Failed to cancel subscription" });
            }
        }

        /// <summary>
        /// 获取订阅的交易记录（仅限本人或管理员）。
        /// Gets transaction history for a subscription (only for owner or admin).
        /// </summary>
        /// <param name="id">订阅ID。 / Subscription ID.</param>
        /// <returns>交易记录列表。 / List of transactions.</returns>
        [HttpGet("{id}/transactions")]
        public async Task<IActionResult> GetSubscriptionTransactions(Guid id)
        {
            if (id == Guid.Empty)
                return BadRequest(new { message = "Invalid subscription ID" });

            try
            {
                var userId = await GetCurrentUserIdAsync();
                var isAdmin = User.IsInRole("ADMIN") || User.IsInRole("SUPER_ADMIN");

                // 验证订阅存在且有权限访问
                var subscription = await _db.Subscriptions
                    .AsNoTracking()
                    .Where(s => s.Id == id)
                    .FirstOrDefaultAsync();

                if (subscription == null)
                {
                    Log.Warning("Subscription {Id} not found", id);
                    return NotFound(new { message = "Subscription not found" });
                }

                if (!isAdmin && subscription.UserId != userId)
                {
                    Log.Warning("User {UserId} attempted to access transactions for subscription {SubscriptionId}",
                        userId, id);
                    return Forbid();
                }

                var transactions = await _db.Transactions
                    .AsNoTracking()
                    .Where(t => t.SubscriptionId == id)
                    .OrderByDescending(t => t.CreatedTime)
                    .Select(t => new
                    {
                        t.Id,
                        t.Amount,
                        t.Currency,
                        t.Gateway,
                        t.Status,
                        StatusText = t.Status == 0 ? "Pending" :
                                    t.Status == 1 ? "Success" :
                                    t.Status == 2 ? "Failed" :
                                    t.Status == 3 ? "Refunded" : "Unknown",
                        t.ExternalTransactionId,
                        t.CreatedTime,
                        t.Remarks
                    })
                    .ToListAsync();

                Log.Information("Retrieved {Count} transactions for subscription {SubscriptionId}",
                    transactions.Count, id);

                return Ok(transactions);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving transactions for subscription {Id}", id);
                return StatusCode(500, new { message = "Failed to retrieve transactions" });
            }
        }

        /// <summary>
        /// 从JWT Claims中获取当前用户ID。
        /// Gets current user ID from JWT claims.
        /// </summary>
        /// <returns>用户ID，如果未找到则返回Guid.Empty。 / User ID, or Guid.Empty if not found.</returns>
        private async Task<Guid> GetCurrentUserIdAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst(JwtRegisteredClaimNames.NameId)?.Value
                ?? User.FindFirst("nameid")?.Value;

            if (!string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            var emailOrSub = User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (!string.IsNullOrWhiteSpace(emailOrSub))
            {
                var resolvedUserId = await _db.Users
                    .AsNoTracking()
                    .Where(u => u.Email == emailOrSub)
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync();

                if (resolvedUserId != Guid.Empty)
                {
                    return resolvedUserId;
                }
            }

            return Guid.Empty;
        }
    }
}
