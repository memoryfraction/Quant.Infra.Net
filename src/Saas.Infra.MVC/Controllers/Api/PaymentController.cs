using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        /// <summary>
        /// 初始化<see cref="PaymentController"/>的新实例。
        /// Initializes a new instance of the <see cref="PaymentController"/> class.
        /// </summary>
        /// <param name="paymentService">支付服务。 / Payment service.</param>
        /// <param name="configuration">配置。 / Configuration.</param>
        /// <param name="gateways">支付网关集合。 / Payment gateways collection.</param>
        /// <exception cref="ArgumentNullException">当参数为null时抛出。 / Thrown when parameters are null.</exception>
        public PaymentController(
            IPaymentService paymentService,
            IConfiguration configuration,
            IEnumerable<IPaymentGateway> gateways)
        {
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _gateways = gateways ?? throw new ArgumentNullException(nameof(gateways));
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
                return BadRequest(new { message = "Request cannot be null" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // 获取当前用户ID
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    // 如果NameIdentifier不存在，尝试从Name获取
                    var username = User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                    {
                        Log.Warning("User ID not found in claims for payment intent creation");
                        return Unauthorized(new { message = "User not authenticated" });
                    }
                    
                    // 临时方案：使用固定的测试用户ID（生产环境需要从数据库查询）
                    // TODO: 从数据库根据username查询userId
                    userId = Guid.Empty;
                }

                var result = await _paymentService.CreatePaymentIntentAsync(
                    userId,
                    request.PriceId,
                    request.Gateway);

                var response = new PaymentIntentDto
                {
                    ClientSecret = result.ClientSecret,
                    PaymentIntentId = result.PaymentIntentId,
                    Amount = result.Amount,
                    Currency = result.Currency,
                    PublishableKey = _configuration["Stripe:PublishableKey"]
                };

                Log.Information("Payment intent created for user {UserId}, price {PriceId}",
                    userId, request.PriceId);

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
        /// 确认支付并创建订阅（用户）。
        /// Confirms payment and creates subscription (User).
        /// </summary>
        /// <param name="request">确认支付请求。 / Confirm payment request.</param>
        /// <returns>支付确认结果。 / Payment confirmation result.</returns>
        [HttpPost("confirm")]
        [Authorize]
        public async Task<IActionResult> ConfirmPayment([FromBody] ConfirmPaymentRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Request cannot be null" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // 获取当前用户ID
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    var username = User.Identity?.Name;
                    if (string.IsNullOrEmpty(username))
                    {
                        Log.Warning("User ID not found in claims for payment confirmation");
                        return Unauthorized(new { message = "User not authenticated" });
                    }
                    
                    userId = Guid.Empty;
                }

                var subscriptionId = await _paymentService.ConfirmPaymentAndCreateSubscriptionAsync(
                    request.PaymentIntentId,
                    userId,
                    request.PriceId,
                    request.Gateway);

                var response = new PaymentConfirmationDto
                {
                    Success = true,
                    SubscriptionId = subscriptionId,
                    Message = "Payment confirmed and subscription created successfully"
                };

                Log.Information("Payment confirmed for user {UserId}, subscription {SubscriptionId}",
                    userId, subscriptionId);

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
                var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
                var signature = Request.Headers["Stripe-Signature"].ToString();

                if (string.IsNullOrEmpty(signature))
                {
                    Log.Warning("Stripe webhook received without signature");
                    return BadRequest(new { message = "Missing signature" });
                }

                // 查找Stripe网关
                var stripeGateway = _gateways.FirstOrDefault(g => g.GatewayName == "Stripe");
                if (stripeGateway == null)
                {
                    Log.Error("Stripe gateway not found for webhook processing");
                    return StatusCode(500, new { message = "Gateway not configured" });
                }

                // 验证签名
                var isValid = await stripeGateway.VerifyWebhookSignatureAsync(json, signature);
                if (!isValid)
                {
                    Log.Warning("Stripe webhook signature verification failed");
                    return Unauthorized(new { message = "Invalid signature" });
                }

                // TODO: 根据事件类型处理（payment_intent.succeeded等）
                Log.Information("Stripe webhook processed successfully");

                return Ok();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error processing Stripe webhook");
                return StatusCode(500, new { message = "Webhook processing failed" });
            }
        }
    }
}
