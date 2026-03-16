using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saas.Infra.MVC.Models.Requests;
using Saas.Infra.MVC.Models.Responses;
using Saas.Infra.Services.Payment;
using Serilog;

namespace Saas.Infra.MVC.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IConfiguration _configuration;
        private readonly IUserContextService _userContextService;
        private readonly IPaymentApplicationService _paymentApplicationService;

        public PaymentController(
            IPaymentService paymentService,
            IConfiguration configuration,
            IUserContextService userContextService,
            IPaymentApplicationService paymentApplicationService)
        {
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _userContextService = userContextService ?? throw new ArgumentNullException(nameof(userContextService));
            _paymentApplicationService = paymentApplicationService ?? throw new ArgumentNullException(nameof(paymentApplicationService));
        }

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
                var userId = await _userContextService.ResolveUserIdAsync(User);
                if (userId == Guid.Empty)
                    return Unauthorized(new { message = "User not authenticated" });

                var result = await _paymentService.CreatePaymentIntentAsync(userId, request.PriceId, request.Gateway);
                return Ok(new PaymentIntentDto
                {
                    OrderId = result.OrderId,
                    ClientSecret = result.ClientSecret,
                    PaymentIntentId = result.PaymentIntentId,
                    Amount = result.Amount,
                    Currency = result.Currency,
                    PublishableKey = _configuration["Stripe:PublishableKey"]
                });
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
                var userId = await _userContextService.ResolveUserIdAsync(User);
                if (userId == Guid.Empty)
                    return Unauthorized(new { message = "User not authenticated" });

                var result = await _paymentApplicationService.CreateOrderAsync(userId, request.PriceId, Request.Scheme, Request.Host.ToString());
                return Ok(new CreateOrderDto
                {
                    OrderId = result.OrderId,
                    Status = result.Status,
                    ProductId = result.ProductId,
                    PriceId = result.PriceId,
                    OriginalAmount = result.OriginalAmount,
                    ActualAmount = result.ActualAmount,
                    Currency = result.Currency,
                    ProductName = result.ProductName,
                    PriceName = result.PriceName,
                    BillingPeriod = result.BillingPeriod,
                    PaymentUrl = result.PaymentUrl,
                    ExpiredTime = result.ExpiredTime
                });
            }
            catch (InvalidOperationException ex)
            {
                Log.Warning(ex, "Business error creating order: {Message}", ex.Message);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error creating order");
                return StatusCode(500, new { message = "Failed to create order" });
            }
        }

        [HttpPost("{orderId:guid}/checkout-session")]
        [Authorize]
        public async Task<IActionResult> CreateCheckoutSession(Guid orderId)
        {
            if (orderId == Guid.Empty)
                return BadRequest(new { message = "Order ID is required" });

            try
            {
                var userId = await _userContextService.ResolveUserIdAsync(User);
                if (userId == Guid.Empty)
                    return Unauthorized(new { message = "User not authenticated" });

                var result = await _paymentApplicationService.CreateCheckoutSessionAsync(orderId, userId, Request.Scheme, Request.Host.ToString());
                return Ok(new
                {
                    orderId,
                    paymentUrl = result.Url,
                    sessionId = result.SessionId
                });
            }
            catch (InvalidOperationException ex)
            {
                Log.Warning(ex, "Error creating checkout session for order {OrderId}", orderId);
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error creating checkout session for order {OrderId}", orderId);
                return StatusCode(500, new { message = "Failed to create checkout session" });
            }
        }

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
                var userId = await _userContextService.ResolveUserIdAsync(User);
                if (userId == Guid.Empty)
                    return Unauthorized(new { message = "User not authenticated" });

                var subscriptionId = await _paymentService.ConfirmPaymentAndCreateSubscriptionAsync(
                    request.PaymentIntentId,
                    userId,
                    request.OrderId,
                    request.Gateway);

                return Ok(new PaymentConfirmationDto
                {
                    Success = true,
                    OrderId = request.OrderId,
                    SubscriptionId = subscriptionId,
                    Message = "Payment confirmed and subscription created successfully"
                });
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

        [HttpGet("status/{orderId:guid}")]
        [Authorize]
        public async Task<IActionResult> GetPaymentStatus(Guid orderId)
        {
            if (orderId == Guid.Empty)
                return BadRequest(new { message = "Order ID is required" });

            try
            {
                var userId = await _userContextService.ResolveUserIdAsync(User);
                if (userId == Guid.Empty)
                    return Unauthorized(new { message = "User not authenticated" });

                var result = await _paymentApplicationService.GetPaymentStatusAsync(orderId, userId);
                if (result == null)
                    return NotFound(new { message = "Order not found" });

                return Ok(new PaymentStatusDto
                {
                    OrderId = result.OrderId,
                    OrderStatus = result.OrderStatus,
                    OrderStatusText = result.OrderStatusText,
                    Paid = result.Paid,
                    SubscriptionId = result.SubscriptionId,
                    TransactionId = result.TransactionId,
                    TransactionStatus = result.TransactionStatus,
                    ExternalTransactionId = result.ExternalTransactionId,
                    PaidTime = result.PaidTime,
                    ExpiredTime = result.ExpiredTime,
                    SubscriptionAccessToken = result.SubscriptionAccessToken,
                    SubscriptionTokenExpiresIn = result.SubscriptionTokenExpiresIn
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error querying payment status for order {OrderId}", orderId);
                return StatusCode(500, new { message = "Failed to query payment status" });
            }
        }

        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> HandleStripeWebhook()
        {
            try
            {
                var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
                var signature = Request.Headers["Stripe-Signature"].ToString();
                if (string.IsNullOrEmpty(signature))
                    return BadRequest(new { message = "Missing signature" });

                var isValid = await _paymentApplicationService.VerifyAndProcessStripeWebhookAsync(json, signature);
                if (!isValid)
                    return Unauthorized(new { message = "Invalid signature" });

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
