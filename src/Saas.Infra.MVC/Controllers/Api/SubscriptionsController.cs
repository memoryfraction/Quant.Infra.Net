using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saas.Infra.Data;
using Saas.Infra.MVC.Models.Responses;
using Saas.Infra.Services.Payment;
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
        private readonly IUserContextService _userContextService;
        private readonly ISubscriptionApplicationService _subscriptionApplicationService;

        /// <summary>
        /// 初始化<see cref="SubscriptionsController"/>的新实例。
        /// Initializes a new instance of the <see cref="SubscriptionsController"/> class.
        /// </summary>
        /// <param name="userContextService">用户上下文服务。 / User context service.</param>
        /// <param name="subscriptionApplicationService">订阅应用服务。 / Subscription application service.</param>
        /// <exception cref="ArgumentNullException">当参数为null时抛出。 / Thrown when a parameter is null.</exception>
        public SubscriptionsController(IUserContextService userContextService, ISubscriptionApplicationService subscriptionApplicationService)
        {
            _userContextService = userContextService ?? throw new ArgumentNullException(nameof(userContextService));
            _subscriptionApplicationService = subscriptionApplicationService ?? throw new ArgumentNullException(nameof(subscriptionApplicationService));
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
                var userId = await _userContextService.ResolveUserIdAsync(User);
                if (userId == Guid.Empty)
                    return Unauthorized(new { message = "User not authenticated" });

                var subscriptions = await _subscriptionApplicationService.GetMySubscriptionsAsync(userId, activeOnly);
                var result = subscriptions.Select(MapSubscription).ToList();
                Log.Information("Retrieved {Count} subscriptions for user {UserId}", result.Count, userId);
                return Ok(result);
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
                var userId = await _userContextService.ResolveUserIdAsync(User);
                var isAdmin = User.IsInRole("ADMIN") || User.IsInRole("SUPER_ADMIN");
                var subscription = await _subscriptionApplicationService.GetSubscriptionAsync(id);
                if (subscription == null)
                    return NotFound(new { message = "Subscription not found" });
                if (!isAdmin && subscription.UserId != userId)
                    return Forbid();

                return Ok(MapSubscription(subscription));
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
                var userId = await _userContextService.ResolveUserIdAsync(User);
                var isAdmin = User.IsInRole("ADMIN") || User.IsInRole("SUPER_ADMIN");
                var subscription = await _subscriptionApplicationService.GetSubscriptionAsync(id);
                if (subscription == null)
                    return NotFound(new { message = "Subscription not found" });
                if (!isAdmin && subscription.UserId != userId)
                    return Forbid();

                await _subscriptionApplicationService.CancelSubscriptionAsync(id);
                Log.Information("Subscription {Id} cancelled by user {UserId}", id, userId);
                return Ok(new { message = "Subscription cancelled successfully" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
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
                var userId = await _userContextService.ResolveUserIdAsync(User);
                var isAdmin = User.IsInRole("ADMIN") || User.IsInRole("SUPER_ADMIN");
                var subscription = await _subscriptionApplicationService.GetSubscriptionAsync(id);
                if (subscription == null)
                    return NotFound(new { message = "Subscription not found" });
                if (!isAdmin && subscription.UserId != userId)
                    return Forbid();

                var transactions = await _subscriptionApplicationService.GetSubscriptionTransactionsAsync(id);
                return Ok(transactions);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving transactions for subscription {Id}", id);
                return StatusCode(500, new { message = "Failed to retrieve transactions" });
            }
        }

        private static SubscriptionDto MapSubscription(SubscriptionEntity subscription) => new()
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
    }
}
