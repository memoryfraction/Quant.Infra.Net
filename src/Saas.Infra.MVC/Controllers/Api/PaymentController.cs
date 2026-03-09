using System;
using System.IO;
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
        /// <exception cref="ArgumentNullException">当参数为null时抛出。 / Thrown when parameters are null.</exception>
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
                return BadRequest(new { message = "Request cannot be null" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // 获取当前用户ID
                var userId = await ResolveCurrentUserIdAsync();
                if (userId == Guid.Empty)
                {
                    Log.Warning("User ID not found in claims or database for payment intent creation");
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var result = await _paymentService.CreatePaymentIntentAsync(
                    userId,
                    request.PriceId,
                    request.Gateway);

                var response = new PaymentIntentDto
                {
                    OrderId = result.OrderId,
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
        /// 创建订单（产品端调用）。
        /// Creates an order (called by product side).
        /// </summary>
        /// <param name="request">创建订单请求。 / Create order request.</param>
        /// <returns>订单信息与支付页链接。 / Order info and payment page URL.</returns>
        [HttpPost("create-order")]
        [Authorize]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Request cannot be null" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var userId = await ResolveCurrentUserIdAsync();
                if (userId == Guid.Empty)
                {
                    Log.Warning("User ID not found in claims or database for order creation");
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var price = await _db.Prices
                    .Include(p => p.Product)
                    .FirstOrDefaultAsync(p => p.Id == request.PriceId);

                if (price == null || !price.IsActive)
                {
                    return BadRequest(new { message = "Price is invalid or inactive" });
                }

                if (price.Product == null || !price.Product.IsActive)
                {
                    return BadRequest(new { message = "Product is invalid or inactive" });
                }

                var order = new OrderEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OrderType = string.IsNullOrWhiteSpace(request.OrderType) ? "SUBSCRIPTION" : request.OrderType.Trim().ToUpperInvariant(),
                    ProductId = price.ProductId,
                    PriceId = price.Id,
                    OriginalAmount = price.Amount,
                    ActualAmount = price.Amount,
                    DiscountAmount = 0,
                    Status = 0,
                    ExpiredTime = DateTimeOffset.UtcNow.AddHours(24),
                    CreatedTime = DateTimeOffset.UtcNow,
                    IsDeleted = false
                };

                _db.Orders.Add(order);
                await _db.SaveChangesAsync();

                var paymentUrl = $"{Request.Scheme}://{Request.Host}/checkout?orderId={order.Id}";
                var response = new CreateOrderDto
                {
                    OrderId = order.Id,
                    Status = order.Status,
                    ProductId = order.ProductId,
                    PriceId = order.PriceId,
                    OrderType = order.OrderType,
                    OriginalAmount = order.OriginalAmount,
                    ActualAmount = order.ActualAmount,
                    Currency = price.Currency,
                    ProductName = price.Product.Name,
                    PriceName = price.Name,
                    BillingPeriod = price.BillingPeriod,
                    PaymentUrl = paymentUrl,
                    ExpiredTime = order.ExpiredTime
                };

                Log.Information("Order {OrderId} created for user {UserId}, price {PriceId}", order.Id, userId, price.Id);
                return Ok(response);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error creating order");
                return StatusCode(500, new { message = "Failed to create order" });
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
                var userId = await ResolveCurrentUserIdAsync();
                if (userId == Guid.Empty)
                {
                    Log.Warning("User ID not found in claims or database for payment confirmation");
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var subscriptionId = await _paymentService.ConfirmPaymentAndCreateSubscriptionAsync(
                    request.PaymentIntentId,
                    userId,
                    request.OrderId,
                    request.Gateway);

                var response = new PaymentConfirmationDto
                {
                    Success = true,
                    OrderId = request.OrderId,
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

        private async Task<Guid> ResolveCurrentUserIdAsync()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            var email = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("sub") ?? User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
            {
                return Guid.Empty;
            }

            return await _db.Users
                .AsNoTracking()
                .Where(u => u.Email == email && !u.IsDeleted)
                .Select(u => u.Id)
                .FirstOrDefaultAsync();
        }
    }
}
