using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Infra.Data;
using Saas.Infra.MVC.Models.Requests;
using Saas.Infra.MVC.Models.Responses;
using Saas.Infra.MVC.Services.Payment;
using Serilog;

namespace Saas.Infra.MVC.Controllers.Api
{
    /// <summary>
    /// 支付API控制器（用户支付）。
    /// Payment API controller (User payment).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IConfiguration _configuration;
        private readonly IEnumerable<IPaymentGateway> _gateways;
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// 初始化<see cref="PaymentController"/>的新实例。
        /// Initializes a new instance of the <see cref="PaymentController"/> class.
        /// </summary>
        /// <param name="paymentService">支付服务。 / Payment service.</param>
        /// <param name="configuration">配置。 / Configuration.</param>
        /// <param name="gateways">支付网关集合。 / Payment gateways collection.</param>
        /// <param name="db">数据库上下文。 / Database context.</param>
        /// <exception cref="ArgumentNullException">当参数为null时抛出。 / Thrown when arguments are null.</exception>
        public PaymentController(
            IPaymentService paymentService,
            IConfiguration configuration,
            IEnumerable<IPaymentGateway> gateways,
            ApplicationDbContext db)
        {
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _gateways = gateways ?? throw new ArgumentNullException(nameof(gateways));
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// 创建支付意图（用户）。
        /// Creates a payment intent (User).
        /// </summary>
        /// <param name="request">创建支付意图请求。 / Create payment intent request.</param>
        /// <returns>支付意图详情。 / Payment intent details.</returns>
        [HttpPost("create-intent")]
        [Authorize]
        public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request cannot be null" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
                if (userId == Guid.Empty)
                {
                    Log.Warning("User ID not found in claims for payment intent creation");
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var result = await _paymentService.CreatePaymentIntentAsync(userId, request.PriceId, request.Gateway).ConfigureAwait(false);
                var response = new PaymentIntentDto
                {
                    ClientSecret = result.ClientSecret,
                    PaymentIntentId = result.PaymentIntentId,
                    Amount = result.Amount,
                    Currency = result.Currency,
                    PublishableKey = _configuration["Stripe:PublishableKey"]
                };

                Log.Information("Payment intent created for user {UserId}, price {PriceId}", userId, request.PriceId);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                Log.Warning(ex, "Error creating payment intent: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error creating payment intent");
                return StatusCode(500, new { message = "Failed to create payment intent" });
            }
        }

        /// <summary>
        /// 创建托管结账会话（用户）。
        /// Creates a hosted checkout session (User).
        /// </summary>
        /// <param name="request">创建托管结账会话请求。 / Create hosted checkout session request.</param>
        /// <returns>托管结账会话信息。 / Hosted checkout session information.</returns>
        [HttpPost("checkout-session")]
        [Authorize]
        public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request cannot be null" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
                if (userId == Guid.Empty)
                {
                    Log.Warning("User ID not found in claims for checkout session creation");
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var session = await _paymentService.CreateCheckoutSessionAsync(
                    userId,
                    request.PriceId,
                    request.Gateway,
                    request.SuccessUrl,
                    request.CancelUrl).ConfigureAwait(false);

                var response = new CheckoutSessionDto
                {
                    SessionId = session.SessionId,
                    Url = session.Url
                };

                Log.Information("Checkout session created for user {UserId}, price {PriceId}, session {SessionId}", userId, request.PriceId, session.SessionId);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                Log.Warning(ex, "Error creating checkout session: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error creating checkout session");
                return StatusCode(500, new { message = "Failed to create checkout session" });
            }
        }

        /// <summary>
        /// 确认支付并创建订阅（用户）。
        /// Confirms payment and creates a subscription (User).
        /// </summary>
        /// <param name="request">确认支付请求。 / Confirm payment request.</param>
        /// <returns>支付确认结果。 / Payment confirmation result.</returns>
        [HttpPost("confirm")]
        [Authorize]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request cannot be null" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
                if (userId == Guid.Empty)
                {
                    Log.Warning("User ID not found in claims for payment confirmation");
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var subscriptionId = await _paymentService.ConfirmPaymentAndCreateSubscriptionAsync(
                    request.PaymentIntentId,
                    userId,
                    request.PriceId,
                    request.Gateway).ConfigureAwait(false);

                var response = new PaymentConfirmationDto
                {
                    Success = true,
                    SubscriptionId = subscriptionId,
                    Message = "Payment confirmed and subscription created successfully"
                };

                Log.Information("Payment confirmed for user {UserId}, subscription {SubscriptionId}", userId, subscriptionId);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                Log.Warning(ex, "Error confirming payment: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error confirming payment");
                return StatusCode(500, new { message = "Failed to confirm payment" });
            }
        }

        /// <summary>
        /// 根据托管结账会话确认支付并创建订阅（用户）。
        /// Confirms payment and creates a subscription from a hosted checkout session (User).
        /// </summary>
        /// <param name="request">确认托管结账会话请求。 / Confirm hosted checkout session request.</param>
        /// <returns>支付确认结果。 / Payment confirmation result.</returns>
        [HttpPost("confirm-checkout-session")]
        [Authorize]
        public async Task<IActionResult> ConfirmCheckoutSession([FromBody] ConfirmCheckoutSessionRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Request cannot be null" });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var userId = await GetCurrentUserIdAsync().ConfigureAwait(false);
                if (userId == Guid.Empty)
                {
                    Log.Warning("User ID not found in claims for checkout session confirmation");
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var subscriptionId = await _paymentService.ConfirmCheckoutSessionAsync(
                    request.SessionId,
                    userId,
                    request.PriceId,
                    request.Gateway).ConfigureAwait(false);

                var response = new PaymentConfirmationDto
                {
                    Success = true,
                    SubscriptionId = subscriptionId,
                    Message = "Payment confirmed and subscription created successfully"
                };

                Log.Information("Checkout session confirmed for user {UserId}, subscription {SubscriptionId}, session {SessionId}", userId, subscriptionId, request.SessionId);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                Log.Warning(ex, "Error confirming checkout session: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error confirming checkout session");
                return StatusCode(500, new { message = "Failed to confirm checkout session" });
            }
        }

        /// <summary>
        /// 处理Stripe Webhook事件。
        /// Handles Stripe webhook events.
        /// </summary>
        /// <returns>处理结果。 / Processing result.</returns>
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> HandleStripeWebhook()
        {
            try
            {
                var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync().ConfigureAwait(false);
                var signature = Request.Headers["Stripe-Signature"].ToString();

                if (string.IsNullOrEmpty(signature))
                {
                    Log.Warning("Stripe webhook received without signature");
                    return BadRequest(new { message = "Missing signature" });
                }

                var stripeGateway = _gateways.FirstOrDefault(g => g.GatewayName == "Stripe");
                if (stripeGateway == null)
                {
                    Log.Error("Stripe gateway not found for webhook processing");
                    return StatusCode(500, new { message = "Gateway not configured" });
                }

                var isValid = await stripeGateway.VerifyWebhookSignatureAsync(json, signature).ConfigureAwait(false);
                if (!isValid)
                {
                    Log.Warning("Stripe webhook signature verification failed");
                    return Unauthorized(new { message = "Invalid signature" });
                }

                Log.Information("Stripe webhook processed successfully");
                return Ok();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing Stripe webhook");
                return StatusCode(500, new { message = "Webhook processing failed" });
            }
        }

        private async Task<Guid> GetCurrentUserIdAsync()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            var userName = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName))
            {
                return Guid.Empty;
            }

            return await _db.Users
                .AsNoTracking()
                .Where(u => u.Email == userName || u.UserName == userName)
                .Select(u => u.Id)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }
    }
}
