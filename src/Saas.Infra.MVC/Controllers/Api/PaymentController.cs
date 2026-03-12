using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Saas.Infra.Data;
using Saas.Infra.MVC.Models.Requests;
using Saas.Infra.MVC.Models.Responses;
using Saas.Infra.MVC.Services.Payment;
using Serilog;
using Stripe;
using Stripe.Checkout;

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
        private readonly RsaSecurityKey _jwtSigningKey;

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
            ApplicationDbContext db,
            RsaSecurityKey jwtSigningKey)
        {
            _paymentService = paymentService ?? throw new ArgumentNullException(nameof(paymentService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _gateways = gateways ?? throw new ArgumentNullException(nameof(gateways));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _jwtSigningKey = jwtSigningKey ?? throw new ArgumentNullException(nameof(jwtSigningKey));
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

                var checkoutSession = await CreateStripeCheckoutForOrderAsync(order, userId);

                var paymentUrl = checkoutSession.Url;
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

                Log.Information(
                    "Order {OrderId} created and Stripe checkout session {SessionId} prepared for user {UserId}, price {PriceId}",
                    order.Id, checkoutSession.SessionId, userId, price.Id);
                return Ok(response);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error creating order");
                return StatusCode(500, new { message = "Failed to create order" });
            }
        }

        /// <summary>
        /// 基于已有订单创建Stripe Checkout Session。
        /// Creates a Stripe Checkout Session based on an existing order.
        /// </summary>
        /// <param name="orderId">订单ID。 / Order ID.</param>
        /// <returns>包含Stripe托管支付页链接。 / Hosted Stripe checkout URL.</returns>
        [HttpPost("{orderId:guid}/checkout-session")]
        [Authorize]
        public async Task<IActionResult> CreateCheckoutSession(Guid orderId)
        {
            if (orderId == Guid.Empty)
                return BadRequest(new { message = "Order ID is required" });

            try
            {
                var userId = await ResolveCurrentUserIdAsync();
                if (userId == Guid.Empty)
                {
                    Log.Warning("User ID not found in claims or database for checkout session creation");
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var order = await ResolveOrderForCheckoutAsync(orderId, userId);
                if (order == null)
                {
                    return BadRequest(new { message = "Order is invalid for payment" });
                }

                var checkoutSession = await CreateStripeCheckoutForOrderAsync(order, userId);
                return Ok(new
                {
                    orderId = order.Id,
                    paymentUrl = checkoutSession.Url,
                    sessionId = checkoutSession.SessionId
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
        /// 查询订单支付状态（用户）。
        /// Gets payment status by order ID (User).
        /// </summary>
        /// <param name="orderId">订单ID。 / Order ID.</param>
        /// <returns>支付状态。 / Payment status.</returns>
        [HttpGet("status/{orderId:guid}")]
        [Authorize]
        public async Task<IActionResult> GetPaymentStatus(Guid orderId)
        {
            if (orderId == Guid.Empty)
                return BadRequest(new { message = "Order ID is required" });

            try
            {
                var userId = await ResolveCurrentUserIdAsync();
                if (userId == Guid.Empty)
                {
                    Log.Warning("User ID not found in claims or database for payment status query");
                    return Unauthorized(new { message = "User not authenticated" });
                }

                var order = await _db.Orders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId && !o.IsDeleted);

                if (order == null)
                    return NotFound(new { message = "Order not found" });

                var latestTransaction = await _db.Transactions
                    .AsNoTracking()
                    .Where(t => t.OrderId == order.Id)
                    .OrderByDescending(t => t.CreatedTime)
                    .FirstOrDefaultAsync();

                var subscription = order.SubscriptionId.HasValue
                    ? await _db.Subscriptions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == order.SubscriptionId.Value && !s.IsDeleted)
                    : null;

                var user = await _db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == order.UserId && !u.IsDeleted);

                string? subscriptionAccessToken = null;
                int? tokenExpiresIn = null;
                if (order.Status == 1 && subscription != null && user != null)
                {
                    var tokenResult = GenerateSubscriptionToken(
                        user.Id,
                        user.Email,
                        order.ProductId,
                        await ResolveProductNameAsync(order.ProductId),
                        subscription.StartDate,
                        subscription.EndDate,
                        order.Id);

                    subscriptionAccessToken = tokenResult.AccessToken;
                    tokenExpiresIn = tokenResult.ExpiresIn;
                }

                var response = new PaymentStatusDto
                {
                    OrderId = order.Id,
                    OrderStatus = order.Status,
                    OrderStatusText = GetOrderStatusText(order.Status),
                    Paid = order.Status == 1,
                    SubscriptionId = order.SubscriptionId,
                    TransactionId = latestTransaction?.Id,
                    TransactionStatus = latestTransaction?.Status,
                    ExternalTransactionId = latestTransaction?.ExternalTransactionId,
                    PaidTime = order.PaidTime,
                    ExpiredTime = order.ExpiredTime,
                    SubscriptionAccessToken = subscriptionAccessToken,
                    SubscriptionTokenExpiresIn = tokenExpiresIn
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error querying payment status for order {OrderId}", orderId);
                return StatusCode(500, new { message = "Failed to query payment status" });
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

                var stripeEvent = EventUtility.ParseEvent(json);
                await ProcessStripeEventAsync(stripeEvent);

                Log.Information("Stripe webhook processed successfully, event {EventType}", stripeEvent.Type);

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

        private async Task<OrderEntity?> ResolveOrderForCheckoutAsync(Guid orderId, Guid userId)
        {
            var order = await _db.Orders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId && !o.IsDeleted);

            if (order == null)
            {
                Log.Warning("Order {OrderId} not found for user {UserId}", orderId, userId);
                return null;
            }

            if (order.Status != 0)
            {
                Log.Warning("Order {OrderId} is not pending, status {Status}", orderId, order.Status);
                throw new InvalidOperationException("Order is not pending");
            }

            if (order.ExpiredTime.HasValue && order.ExpiredTime.Value <= DateTimeOffset.UtcNow)
            {
                Log.Warning("Order {OrderId} has expired at {ExpiredTime}", orderId, order.ExpiredTime);
                throw new InvalidOperationException("Order has expired");
            }

            return order;
        }

        private async Task<CheckoutSessionResult> CreateStripeCheckoutForOrderAsync(OrderEntity order, Guid userId)
        {
            var price = await _db.Prices
                .AsNoTracking()
                .Include(p => p.Product)
                .FirstOrDefaultAsync(p => p.Id == order.PriceId);

            if (price == null || !price.IsActive)
                throw new InvalidOperationException("Price is invalid or inactive");

            if (price.Product == null || !price.Product.IsActive)
                throw new InvalidOperationException("Product is invalid or inactive");

            var stripeGateway = _gateways.FirstOrDefault(g => g.GatewayName == "Stripe");
            if (stripeGateway == null)
                throw new InvalidOperationException("Stripe gateway is not configured");

            var successUrl = $"{Request.Scheme}://{Request.Host}/checkout?payment=success&orderId={order.Id}&session_id={{CHECKOUT_SESSION_ID}}";
            var cancelUrl = $"{Request.Scheme}://{Request.Host}/checkout?payment=cancel&orderId={order.Id}";
            var metadata = new Dictionary<string, string>
            {
                ["orderId"] = order.Id.ToString(),
                ["userId"] = userId.ToString(),
                ["priceId"] = price.Id.ToString(),
                ["productId"] = price.ProductId.ToString()
            };

            var checkoutSession = await stripeGateway.CreateCheckoutSessionAsync(
                price.Id,
                order.ActualAmount,
                price.Currency,
                price.Product.Name,
                price.BillingPeriod,
                metadata,
                successUrl,
                cancelUrl);

            if (string.IsNullOrWhiteSpace(checkoutSession.Url))
                throw new InvalidOperationException("Failed to create Stripe checkout session");

            return checkoutSession;
        }

        private static string GetOrderStatusText(short status)
        {
            return status switch
            {
                0 => "Pending",
                1 => "Paid",
                2 => "Cancelled",
                3 => "Refunded",
                _ => $"Unknown({status})"
            };
        }

        private async Task ProcessStripeEventAsync(Event stripeEvent)
        {
            if (stripeEvent == null)
                throw new ArgumentNullException(nameof(stripeEvent));

            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                    if (stripeEvent.Data.Object is Session completedSession)
                    {
                        await HandleCheckoutCompletedAsync(completedSession);
                    }
                    break;

                case "checkout.session.expired":
                    if (stripeEvent.Data.Object is Session expiredSession)
                    {
                        await HandleCheckoutExpiredAsync(expiredSession);
                    }
                    break;

                case "payment_intent.payment_failed":
                    if (stripeEvent.Data.Object is PaymentIntent failedIntent)
                    {
                        await HandlePaymentFailedAsync(failedIntent);
                    }
                    break;

                default:
                    Log.Information("Stripe event {EventType} is ignored in current handler", stripeEvent.Type);
                    break;
            }
        }

        private async Task HandleCheckoutCompletedAsync(Session session)
        {
            var orderId = TryParseMetadataGuid(session.Metadata, "orderId");
            if (orderId == Guid.Empty)
            {
                Log.Warning("checkout.session.completed missing orderId metadata, session {SessionId}", session.Id);
                return;
            }

            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
            if (order == null)
            {
                Log.Warning("Order {OrderId} not found for checkout.session.completed", orderId);
                return;
            }

            if (order.Status == 1)
            {
                Log.Information("Order {OrderId} already paid, skip duplicate completion event", order.Id);
                return;
            }

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                order.Status = 1;
                order.PaidTime = DateTimeOffset.UtcNow;

                var price = await _db.Prices
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == order.PriceId);
                if (price == null)
                {
                    throw new InvalidOperationException($"Price {order.PriceId} not found for order {order.Id}");
                }

                var subscription = await _db.Subscriptions
                    .FirstOrDefaultAsync(s => s.OrderId == order.Id && !s.IsDeleted);
                if (subscription == null)
                {
                    subscription = new SubscriptionEntity
                    {
                        Id = Guid.NewGuid(),
                        UserId = order.UserId,
                        ProductId = order.ProductId,
                        PriceId = order.PriceId,
                        OrderId = order.Id,
                        Status = 1,
                        StartDate = DateTimeOffset.UtcNow,
                        EndDate = CalculateEndDate(price.BillingPeriod),
                        AutoRenew = true,
                        CreatedTime = DateTimeOffset.UtcNow,
                        IsDeleted = false
                    };

                    _db.Subscriptions.Add(subscription);
                }

                if (!order.SubscriptionId.HasValue || order.SubscriptionId.Value != subscription.Id)
                {
                    order.SubscriptionId = subscription.Id;
                }

                var externalTransactionId = !string.IsNullOrWhiteSpace(session.PaymentIntentId)
                    ? session.PaymentIntentId
                    : session.Id;

                var existingTransaction = await _db.Transactions
                    .FirstOrDefaultAsync(t => t.OrderId == order.Id && t.Status == 1);
                if (existingTransaction == null)
                {
                    _db.Transactions.Add(new TransactionEntity
                    {
                        Id = Guid.NewGuid(),
                        UserId = order.UserId,
                        OrderId = order.Id,
                        SubscriptionId = subscription.Id,
                        Amount = session.AmountTotal ?? order.ActualAmount,
                        Currency = string.IsNullOrWhiteSpace(session.Currency) ? "usd" : session.Currency,
                        Gateway = "Stripe",
                        ExternalTransactionId = externalTransactionId,
                        Status = 1,
                        CreatedTime = DateTimeOffset.UtcNow,
                        Remarks = "Stripe checkout.session.completed"
                    });
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                Log.Information("Order {OrderId} marked paid by checkout.session.completed", order.Id);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        private async Task HandleCheckoutExpiredAsync(Session session)
        {
            var orderId = TryParseMetadataGuid(session.Metadata, "orderId");
            if (orderId == Guid.Empty)
            {
                Log.Warning("checkout.session.expired missing orderId metadata, session {SessionId}", session.Id);
                return;
            }

            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
            if (order == null)
            {
                Log.Warning("Order {OrderId} not found for checkout.session.expired", orderId);
                return;
            }

            if (order.Status == 1)
            {
                Log.Information("Order {OrderId} already paid, ignore checkout.session.expired", order.Id);
                return;
            }

            if (order.Status != 2)
            {
                order.Status = 2;
                await _db.SaveChangesAsync();
            }

            Log.Information("Order {OrderId} marked cancelled by checkout.session.expired", order.Id);
        }

        private async Task HandlePaymentFailedAsync(PaymentIntent intent)
        {
            var orderId = TryParseMetadataGuid(intent.Metadata, "orderId");
            if (orderId == Guid.Empty)
            {
                Log.Warning("payment_intent.payment_failed missing orderId metadata, paymentIntent {PaymentIntentId}", intent.Id);
                return;
            }

            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
            if (order == null)
            {
                Log.Warning("Order {OrderId} not found for payment_intent.payment_failed", orderId);
                return;
            }

            if (order.Status == 1)
            {
                Log.Information("Order {OrderId} already paid, ignore payment_intent.payment_failed", order.Id);
                return;
            }

            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                order.Status = 2;

                _db.Transactions.Add(new TransactionEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = order.UserId,
                    OrderId = order.Id,
                    SubscriptionId = order.SubscriptionId,
                    Amount = intent.Amount,
                    Currency = string.IsNullOrWhiteSpace(intent.Currency) ? "usd" : intent.Currency,
                    Gateway = "Stripe",
                    ExternalTransactionId = intent.Id,
                    Status = 2,
                    CreatedTime = DateTimeOffset.UtcNow,
                    Remarks = $"Stripe payment failed: {intent.LastPaymentError?.Message ?? "unknown reason"}"
                });

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                Log.Information("Order {OrderId} marked cancelled by payment_intent.payment_failed", order.Id);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        private static Guid TryParseMetadataGuid(IDictionary<string, string>? metadata, string key)
        {
            if (metadata == null || !metadata.TryGetValue(key, out var raw))
            {
                return Guid.Empty;
            }

            return Guid.TryParse(raw, out var value)
                ? value
                : Guid.Empty;
        }

        private static DateTimeOffset CalculateEndDate(string billingPeriod)
        {
            return billingPeriod.Trim().ToLowerInvariant() switch
            {
                "week" => DateTimeOffset.UtcNow.AddDays(7),
                "month" => DateTimeOffset.UtcNow.AddMonths(1),
                "year" => DateTimeOffset.UtcNow.AddYears(1),
                _ => DateTimeOffset.UtcNow.AddMonths(1)
            };
        }

        private async Task<string> ResolveProductNameAsync(Guid productId)
        {
            var productName = await _db.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync();

            return string.IsNullOrWhiteSpace(productName)
                ? "UnknownProduct"
                : productName;
        }

        private (string AccessToken, int ExpiresIn) GenerateSubscriptionToken(
            Guid userId,
            string userEmail,
            Guid productId,
            string productName,
            DateTimeOffset subscriptionStartUtc,
            DateTimeOffset? subscriptionEndUtc,
            Guid orderId)
        {
            var now = DateTimeOffset.UtcNow;
            var expires = now.AddHours(24);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim("productId", productId.ToString()),
                new Claim("productName", productName),
                new Claim("subscriptionStartUtc", subscriptionStartUtc.UtcDateTime.ToString("O")),
                new Claim("subscriptionEndUtc", (subscriptionEndUtc ?? now).UtcDateTime.ToString("O")),
                new Claim("orderId", orderId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, userEmail),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var signingCredentials = new SigningCredentials(_jwtSigningKey, SecurityAlgorithms.RsaSha256);
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"] ?? JwtConstants.Issuer,
                audience: _configuration["Jwt:Audience"] ?? "Saas.Infra.Clients",
                claims: claims,
                notBefore: now.UtcDateTime,
                expires: expires.UtcDateTime,
                signingCredentials: signingCredentials);

            if (!token.Header.ContainsKey("kid") && !string.IsNullOrWhiteSpace(_jwtSigningKey.KeyId))
            {
                token.Header["kid"] = _jwtSigningKey.KeyId;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var accessToken = tokenHandler.WriteToken(token);
            var expiresIn = (int)(expires - now).TotalSeconds;

            return (accessToken, expiresIn);
        }
    }
}
