using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Saas.Infra.Core;
using Saas.Infra.Data;
using Serilog.Events;
using Stripe;
using Stripe.Checkout;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Saas.Infra.Services.Payment;

public class PaymentUrlResolver : IPaymentUrlResolver
{
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 初始化支付地址解析器。
    /// Initializes the payment URL resolver.
    /// </summary>
    /// <param name="configuration">应用程序配置。 / Application configuration.</param>
    public PaymentUrlResolver(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// 解析支付流程可访问的基础地址。
    /// Resolves the accessible base URL used by the payment flow.
    /// </summary>
    /// <param name="scheme">请求协议。 / Request scheme.</param>
    /// <param name="host">请求主机。 / Request host.</param>
    /// <returns>公开基础地址。 / Public base URL.</returns>
    public string ResolveBaseUrl(string scheme, string host)
    {
        const string methodName = nameof(ResolveBaseUrl);
        string finalResult = string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(scheme))
                throw new ArgumentException("Scheme cannot be null or empty/whitespace", nameof(scheme));
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("Host cannot be null or empty/whitespace", nameof(host));

            var runtimeEnv = UtilityService.GetCurrentEnvironment();
            if (runtimeEnv == RuntimeEnvironment.AzureContainerApps)
            {
                var hostName = Environment.GetEnvironmentVariable("CONTAINER_APP_HOSTNAME");
                if (!string.IsNullOrWhiteSpace(hostName))
                {
                    var cleanedHostName = hostName.Trim().TrimEnd('/');
                    var acaBaseUrl = cleanedHostName.StartsWith("http")
                        ? cleanedHostName
                        : $"https://{cleanedHostName}";

                    if (Uri.TryCreate(acaBaseUrl, UriKind.Absolute, out var validAcaUri))
                    {
                        finalResult = validAcaUri.GetLeftPart(UriPartial.Authority);
                        UtilityService.LogAndWriteLine(LogEventLevel.Debug, "[{MethodName}] Resolved from CONTAINER_APP_HOSTNAME: {BaseUrl}, FinalResult: {FinalResult}", methodName, finalResult, finalResult);
                        return finalResult;
                    }

                    UtilityService.LogAndWriteLine(LogEventLevel.Warning, "[{MethodName}] Invalid CONTAINER_APP_HOSTNAME format: {HostName}, fallback to request context", methodName, hostName);
                }
                else
                {
                    UtilityService.LogAndWriteLine(LogEventLevel.Debug, "[{MethodName}] CONTAINER_APP_HOSTNAME is empty in ACA environment, fallback to request context", methodName);
                }
            }

            var cleanedScheme = scheme.Trim().ToLowerInvariant();
            var cleanedHost = host.Trim();
            var requestBaseUrl = $"{cleanedScheme}://{cleanedHost}";

            if (Uri.TryCreate(requestBaseUrl, UriKind.Absolute, out var validRequestUri))
            {
                finalResult = validRequestUri.GetLeftPart(UriPartial.Authority);
                UtilityService.LogAndWriteLine(LogEventLevel.Debug, "[{MethodName}] Resolved from Request context (scheme:{Scheme}, host:{Host}): {BaseUrl}, FinalResult: {FinalResult}", methodName, scheme, host, finalResult, finalResult);
                return finalResult;
            }

            finalResult = "https://default-payment-url.azurecontainerapps.io";
            UtilityService.LogAndWriteLine(LogEventLevel.Warning, "[{MethodName}] Failed to resolve from ACA/Request (scheme:{Scheme}, host:{Host}), using fallback: {FinalResult}", methodName, scheme, host, finalResult);
            return finalResult;
        }
        catch (Exception ex)
        {
            UtilityService.LogAndWriteLine(ex, LogEventLevel.Error, "[{MethodName}] Failed to resolve base URL (scheme:{Scheme}, host:{Host}), Error: {Message}", methodName, scheme, host, ex.Message);
            throw;
        }
        finally
        {
            UtilityService.LogAndWriteLine(LogEventLevel.Information, "[{MethodName}] Execution completed, FinalReturnedUrl: {FinalResult}", methodName, finalResult);
        }
    }

}

public class SubscriptionTokenService : ISubscriptionTokenService
{
    private const string DefaultIssuer = "Saas.Infra.Server";
    private readonly IConfiguration _configuration;
    private readonly RsaSecurityKey _jwtSigningKey;

    /// <summary>
    /// 初始化订阅令牌服务。
    /// Initializes the subscription token service.
    /// </summary>
    /// <param name="configuration">应用程序配置。 / Application configuration.</param>
    /// <param name="jwtSigningKey">JWT 签名密钥。 / JWT signing key.</param>
    public SubscriptionTokenService(IConfiguration configuration, RsaSecurityKey jwtSigningKey)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _jwtSigningKey = jwtSigningKey ?? throw new ArgumentNullException(nameof(jwtSigningKey));
    }

    /// <summary>
    /// 生成订阅访问令牌。
    /// Generates a subscription access token.
    /// </summary>
    /// <param name="request">令牌请求参数。 / Token request payload.</param>
    /// <returns>订阅令牌结果。 / Subscription token result.</returns>
    public SubscriptionTokenResult GenerateToken(SubscriptionTokenRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddHours(24);
        var ctxHash = ComputeContextHash($"{request.UserId}|{request.SubscriptionId}|{request.OrderId}");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, request.UserId.ToString()),
            new(ClaimTypes.Name, request.UserEmail),
            new("token_type", "subscription"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new("productId", request.ProductId.ToString()),
            new("productName", request.ProductName),
            new("subscriptionId", request.SubscriptionId.ToString()),
            new("subscriptionStatus", request.SubscriptionStatus.ToString()),
            new("subscriptionStartUtc", request.SubscriptionStartUtc.UtcDateTime.ToString("O")),
            new("subscriptionEndUtc", (request.SubscriptionEndUtc ?? now).UtcDateTime.ToString("O")),
            new("orderId", request.OrderId.ToString()),
            new("ctx_hash", ctxHash)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? DefaultIssuer,
            audience: _configuration["Jwt:SubscriptionAudience"] ?? "Saas.Infra.Subscription",
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(_jwtSigningKey, SecurityAlgorithms.RsaSha256));

        if (!token.Header.ContainsKey("kid") && !string.IsNullOrWhiteSpace(_jwtSigningKey.KeyId))
        {
            token.Header["kid"] = _jwtSigningKey.KeyId;
        }

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var expiresIn = (int)(expires - now).TotalSeconds;

        UtilityService.LogAndWriteLine(LogEventLevel.Debug, "Subscription token generated for user {UserId}, subscription {SubscriptionId}", request.UserId, request.SubscriptionId);

        return new SubscriptionTokenResult
        {
            AccessToken = accessToken,
            ExpiresIn = expiresIn
        };    }

    private static string ComputeContextHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
}

public class UserContextService : IUserContextService
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// 初始化用户上下文服务。
    /// Initializes the user context service.
    /// </summary>
    /// <param name="db">数据库上下文。 / Database context.</param>
    public UserContextService(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// 从当前主体解析用户标识。
    /// Resolves the user identifier from the current principal.
    /// </summary>
    /// <param name="principal">声明主体。 / Claims principal.</param>
    /// <returns>用户标识。 / User identifier.</returns>
    public async Task<Guid> ResolveUserIdAsync(ClaimsPrincipal principal)
    {
        if (principal == null)
            throw new ArgumentNullException(nameof(principal));

        var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.NameId)
            ?? principal.FindFirstValue("nameid");

        if (!string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        var email = principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.Identity?.Name;

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

public class PaymentService : IPaymentService
{
    private readonly IEnumerable<IPaymentGateway> _gateways;
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// 初始化支付服务。
    /// Initializes the payment service.
    /// </summary>
    /// <param name="gateways">支付网关集合。 / Payment gateways.</param>
    /// <param name="db">数据库上下文。 / Database context.</param>
    public PaymentService(IEnumerable<IPaymentGateway> gateways, ApplicationDbContext db)
    {
        _gateways = gateways ?? throw new ArgumentNullException(nameof(gateways));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// 创建支付意图并生成待支付订单。
    /// Creates a payment intent and a pending order.
    /// </summary>
    /// <param name="userId">用户标识。 / User identifier.</param>
    /// <param name="priceId">价格标识。 / Price identifier.</param>
    /// <param name="gateway">支付网关。 / Payment gateway.</param>
    /// <returns>支付意图结果。 / Payment intent result.</returns>
    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(Guid userId, Guid priceId, string gateway)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Invalid user ID.", nameof(userId));
        if (priceId == Guid.Empty)
            throw new ArgumentException("Invalid price ID.", nameof(priceId));
        if (string.IsNullOrWhiteSpace(gateway))
            throw new ArgumentException("Gateway cannot be null or whitespace.", nameof(gateway));

        var price = await _db.Prices
            .Include(p => p.Product)
            .FirstOrDefaultAsync(p => p.Id == priceId);

        if (price == null)
            throw new InvalidOperationException("Price not found.");
        if (!price.IsActive)
            throw new InvalidOperationException("Price is not active.");
        if (price.Product == null || !price.Product.IsActive)
            throw new InvalidOperationException("Product is not active.");

        var paymentGateway = ResolveGateway(gateway);
        var order = new OrderEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductId = price.ProductId,
            PriceId = priceId,
            OriginalAmount = price.Amount,
            ActualAmount = price.Amount,
            DiscountAmount = 0,
            Status = (short)OrderStatus.Pending,
            ExpiredTime = DateTimeOffset.UtcNow.AddHours(24),
            CreatedTime = DateTimeOffset.UtcNow,
            IsDeleted = false
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var result = await paymentGateway.CreatePaymentIntentAsync(price.Amount, price.Currency, new Dictionary<string, string>
        {
            ["orderId"] = order.Id.ToString(),
            ["userId"] = userId.ToString(),
            ["priceId"] = priceId.ToString(),
            ["productId"] = price.ProductId.ToString(),
            ["productCode"] = price.Product?.Code ?? "unknown"
        });

        result.OrderId = order.Id;
        UtilityService.LogAndWriteLine(LogEventLevel.Information, "Payment intent created for order {OrderId}, user {UserId}, price {PriceId}, gateway {Gateway}", order.Id, userId, priceId, gateway);
        return result;
    }

    /// <summary>
    /// 确认支付并创建订阅。
    /// Confirms a payment and creates a subscription.
    /// </summary>
    /// <param name="paymentIntentId">支付意图标识。 / Payment intent identifier.</param>
    /// <param name="userId">用户标识。 / User identifier.</param>
    /// <param name="orderId">订单标识。 / Order identifier.</param>
    /// <param name="gateway">支付网关。 / Payment gateway.</param>
    /// <returns>订阅标识。 / Subscription identifier.</returns>
    public async Task<Guid> ConfirmPaymentAndCreateSubscriptionAsync(string paymentIntentId, Guid userId, Guid orderId, string gateway)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            throw new ArgumentException("Payment intent ID cannot be null or whitespace.", nameof(paymentIntentId));
        if (userId == Guid.Empty)
            throw new ArgumentException("Invalid user ID.", nameof(userId));
        if (orderId == Guid.Empty)
            throw new ArgumentException("Invalid order ID.", nameof(orderId));
        if (string.IsNullOrWhiteSpace(gateway))
            throw new ArgumentException("Gateway cannot be null or whitespace.", nameof(gateway));

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
        if (order == null)
            throw new InvalidOperationException("Order not found.");
        if (order.UserId != userId)
            throw new InvalidOperationException("Order does not belong to the current user.");
        if ((OrderStatus)order.Status != OrderStatus.Pending)
            throw new InvalidOperationException("Order is not pending.");
        if (order.ExpiredTime.HasValue && order.ExpiredTime.Value <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Order has expired.");

        var price = await _db.Prices.Include(p => p.Product).FirstOrDefaultAsync(p => p.Id == order.PriceId);
        if (price == null)
            throw new InvalidOperationException("Price not found.");

        var paymentResult = await ResolveGateway(gateway).ConfirmPaymentAsync(paymentIntentId);
        if (!paymentResult.Succeeded)
        {
            UtilityService.LogAndWriteLine(LogEventLevel.Warning, "Payment {PaymentIntentId} failed: {Error}", paymentIntentId, paymentResult.ErrorMessage ?? "unknown");
            throw new InvalidOperationException($"Payment failed: {paymentResult.ErrorMessage}");
        }

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var subscription = new SubscriptionEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ProductId = price.ProductId,
                PriceId = order.PriceId,
                OrderId = order.Id,
                Status = (short)SubscriptionStatus.Active,
                StartDate = DateTimeOffset.UtcNow,
                EndDate = CalculateEndDate(price.BillingPeriod),
                AutoRenew = true,
                CreatedTime = DateTimeOffset.UtcNow,
                IsDeleted = false
            };

            _db.Subscriptions.Add(subscription);
            _db.Transactions.Add(new TransactionEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrderId = order.Id,
                SubscriptionId = subscription.Id,
                Amount = paymentResult.Amount,
                Currency = paymentResult.Currency,
                Gateway = gateway,
                ExternalTransactionId = paymentResult.ExternalTransactionId,
                Status = (short)TransactionStatus.Success,
                CreatedTime = DateTimeOffset.UtcNow,
                Remarks = $"Payment via {gateway}"
            });

            order.SubscriptionId = subscription.Id;
            order.ActualAmount = paymentResult.Amount;
            order.Status = (short)OrderStatus.Paid;
            order.PaidTime = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return subscription.Id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private IPaymentGateway ResolveGateway(string gateway)
    {
        return _gateways.FirstOrDefault(g => g.GatewayName.Equals(gateway, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Payment gateway '{gateway}' is not supported.");
    }

    private static DateTimeOffset CalculateEndDate(string billingPeriod)
    {
        return billingPeriod.ToLower() switch
        {
            "week" => DateTimeOffset.UtcNow.AddDays(7),
            "month" => DateTimeOffset.UtcNow.AddMonths(1),
            "year" => DateTimeOffset.UtcNow.AddYears(1),
            _ => DateTimeOffset.UtcNow.AddMonths(1)
        };
    }
}

public class StripeWebhookService : IStripeWebhookService
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// 初始化 Stripe Webhook 服务。
    /// Initializes the Stripe webhook service.
    /// </summary>
    /// <param name="db">数据库上下文。 / Database context.</param>
    public StripeWebhookService(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    /// 处理 Stripe 事件。
    /// Processes a Stripe event.
    /// </summary>
    /// <param name="stripeEvent">Stripe 事件。 / Stripe event.</param>
    /// <returns>异步任务。 / Asynchronous task.</returns>
    public async Task ProcessEventAsync(Event stripeEvent)
    {
        if (stripeEvent == null)
            throw new ArgumentNullException(nameof(stripeEvent));

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed" when stripeEvent.Data.Object is Session completedSession:
                await HandleCheckoutCompletedAsync(completedSession);
                break;
            case "checkout.session.expired" when stripeEvent.Data.Object is Session expiredSession:
                await HandleCheckoutExpiredAsync(expiredSession);
                break;
            case "payment_intent.payment_failed" when stripeEvent.Data.Object is PaymentIntent failedIntent:
                await HandlePaymentFailedAsync(failedIntent);
                break;
            default:
                UtilityService.LogAndWriteLine(LogEventLevel.Information, "Stripe event {EventType} is ignored in current handler", stripeEvent.Type);
                break;
        }
    }

    private async Task HandleCheckoutCompletedAsync(Session session)
    {
        var orderId = TryParseMetadataGuid(session.Metadata, "orderId");
        if (orderId == Guid.Empty)
            return;

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
        if (order == null || (OrderStatus)order.Status == OrderStatus.Paid)
            return;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            order.Status = (short)OrderStatus.Paid;
            order.PaidTime = DateTimeOffset.UtcNow;

            var price = await _db.Prices.AsNoTracking().FirstOrDefaultAsync(p => p.Id == order.PriceId)
                ?? throw new InvalidOperationException($"Price {order.PriceId} not found for order {order.Id}.");

            var subscription = await _db.Subscriptions.FirstOrDefaultAsync(s => s.OrderId == order.Id && !s.IsDeleted);
            if (subscription == null)
            {
                subscription = new SubscriptionEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = order.UserId,
                    ProductId = order.ProductId,
                    PriceId = order.PriceId,
                    OrderId = order.Id,
                    Status = (short)SubscriptionStatus.Active,
                    StartDate = DateTimeOffset.UtcNow,
                    EndDate = CalculateEndDate(price.BillingPeriod),
                    AutoRenew = true,
                    CreatedTime = DateTimeOffset.UtcNow,
                    IsDeleted = false
                };
                _db.Subscriptions.Add(subscription);
            }

            order.SubscriptionId = subscription.Id;
            var existingTransaction = await _db.Transactions.FirstOrDefaultAsync(t => t.OrderId == order.Id && t.Status == (short)TransactionStatus.Success);
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
                    ExternalTransactionId = string.IsNullOrWhiteSpace(session.PaymentIntentId) ? session.Id : session.PaymentIntentId,
                    Status = (short)TransactionStatus.Success,
                    CreatedTime = DateTimeOffset.UtcNow,
                    Remarks = "Stripe checkout.session.completed"
                });
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
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
            return;

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
        if (order == null || (OrderStatus)order.Status == OrderStatus.Paid)
            return;

        if ((OrderStatus)order.Status != OrderStatus.Cancelled)
        {
            order.Status = (short)OrderStatus.Cancelled;
            await _db.SaveChangesAsync();
        }
    }

    private async Task HandlePaymentFailedAsync(PaymentIntent intent)
    {
        var orderId = TryParseMetadataGuid(intent.Metadata, "orderId");
        if (orderId == Guid.Empty)
            return;

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
        if (order == null || (OrderStatus)order.Status == OrderStatus.Paid)
            return;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            order.Status = (short)OrderStatus.Cancelled;
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
                Status = (short)TransactionStatus.Failed,
                CreatedTime = DateTimeOffset.UtcNow,
                Remarks = $"Stripe payment failed: {intent.LastPaymentError?.Message ?? "unknown reason"}"
            });

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
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
            return Guid.Empty;

        return Guid.TryParse(raw, out var value) ? value : Guid.Empty;
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
}

public class StripePaymentGateway : IPaymentGateway
{
    private readonly string _webhookSecret;

    /// <summary>
    /// 初始化 Stripe 支付网关。
    /// Initializes the Stripe payment gateway.
    /// </summary>
    /// <param name="secretKey">Stripe 密钥。 / Stripe secret key.</param>
    /// <param name="webhookSecret">Webhook 密钥。 / Webhook secret.</param>
    public StripePaymentGateway(string secretKey, string webhookSecret)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentNullException(nameof(secretKey));

        _webhookSecret = webhookSecret ?? string.Empty;
        StripeConfiguration.ApiKey = secretKey;
    }

    public string GatewayName => "Stripe";

    /// <summary>
    /// 创建 Stripe 支付意图。
    /// Creates a Stripe payment intent.
    /// </summary>
    /// <param name="amount">金额。 / Amount.</param>
    /// <param name="currency">货币代码。 / Currency code.</param>
    /// <param name="metadata">元数据。 / Metadata.</param>
    /// <returns>支付意图结果。 / Payment intent result.</returns>
    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(long amount, string currency, Dictionary<string, string> metadata)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be null or whitespace.", nameof(currency));
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        var intent = await new PaymentIntentService().CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = amount,
            Currency = currency.ToLowerInvariant(),
            Metadata = metadata,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                AllowRedirects = "never"
            }
        });

        return new PaymentIntentResult
        {
            ClientSecret = intent.ClientSecret ?? string.Empty,
            PaymentIntentId = intent.Id,
            Amount = intent.Amount,
            Currency = intent.Currency,
            Status = intent.Status
        };
    }

    /// <summary>
    /// 确认 Stripe 支付结果。
    /// Confirms the Stripe payment result.
    /// </summary>
    /// <param name="paymentIntentId">支付意图标识。 / Payment intent identifier.</param>
    /// <returns>支付结果。 / Payment result.</returns>
    public async Task<PaymentResult> ConfirmPaymentAsync(string paymentIntentId)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            throw new ArgumentException("Payment intent ID cannot be null or whitespace.", nameof(paymentIntentId));

        try
        {
            var intent = await new PaymentIntentService().GetAsync(paymentIntentId);
            var succeeded = intent.Status == "succeeded";
            return new PaymentResult
            {
                Succeeded = succeeded,
                PaymentIntentId = intent.Id,
                ExternalTransactionId = intent.LatestChargeId,
                Amount = intent.Amount,
                Currency = intent.Currency,
                ErrorMessage = succeeded ? null : $"Payment status: {intent.Status}"
            };
        }
        catch (StripeException ex)
        {
            return new PaymentResult
            {
                Succeeded = false,
                PaymentIntentId = paymentIntentId,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// 验证 Stripe Webhook 签名。
    /// Verifies the Stripe webhook signature.
    /// </summary>
    /// <param name="payload">Webhook 负载。 / Webhook payload.</param>
    /// <param name="signature">Webhook 签名。 / Webhook signature.</param>
    /// <returns>是否有效。 / Whether the signature is valid.</returns>
    public Task<bool> VerifyWebhookSignatureAsync(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload cannot be null or whitespace.", nameof(payload));
        if (string.IsNullOrWhiteSpace(signature))
            throw new ArgumentException("Signature cannot be null or whitespace.", nameof(signature));
        if (string.IsNullOrWhiteSpace(_webhookSecret))
            throw new InvalidOperationException("Stripe webhook secret is not configured.");

        try
        {
            EventUtility.ConstructEvent(payload, signature, _webhookSecret, throwOnApiVersionMismatch: false);
            return Task.FromResult(true);
        }
        catch (StripeException)
        {
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// 创建 Stripe 结账会话。
    /// Creates a Stripe checkout session.
    /// </summary>
    /// <param name="priceId">价格标识。 / Price identifier.</param>
    /// <param name="amount">金额。 / Amount.</param>
    /// <param name="currency">货币代码。 / Currency code.</param>
    /// <param name="productName">产品名称。 / Product name.</param>
    /// <param name="billingPeriod">计费周期。 / Billing period.</param>
    /// <param name="metadata">元数据。 / Metadata.</param>
    /// <param name="successUrl">成功地址。 / Success URL.</param>
    /// <param name="cancelUrl">取消地址。 / Cancel URL.</param>
    /// <returns>结账会话结果。 / Checkout session result.</returns>
    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Guid priceId, long amount, string currency, string productName, string billingPeriod, Dictionary<string, string> metadata, string successUrl, string cancelUrl)
    {
        if (priceId == Guid.Empty)
            throw new ArgumentException("Price ID cannot be empty.", nameof(priceId));
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be null or whitespace.", nameof(currency));
        if (string.IsNullOrWhiteSpace(billingPeriod))
            throw new ArgumentException("Billing period cannot be null or whitespace.", nameof(billingPeriod));
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));
        if (string.IsNullOrWhiteSpace(successUrl))
            throw new ArgumentException("Success URL cannot be null or whitespace.", nameof(successUrl));
        if (string.IsNullOrWhiteSpace(cancelUrl))
            throw new ArgumentException("Cancel URL cannot be null or whitespace.", nameof(cancelUrl));

        var session = await new SessionService().CreateAsync(new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency.ToLowerInvariant(),
                        UnitAmount = amount,
                        Recurring = new SessionLineItemPriceDataRecurringOptions
                        {
                            Interval = NormalizeBillingInterval(billingPeriod)
                        },
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = string.IsNullOrWhiteSpace(productName) ? "Subscription" : productName
                        }
                    },
                    Quantity = 1
                }
            },
            Mode = "subscription",
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            Metadata = metadata
        });

        return new CheckoutSessionResult
        {
            SessionId = session.Id,
            Url = session.Url
        };
    }

    /// <summary>
    /// 获取结账会话关联的支付意图标识。
    /// Gets the payment intent identifier associated with a checkout session.
    /// </summary>
    /// <param name="sessionId">会话标识。 / Session identifier.</param>
    /// <returns>支付意图标识。 / Payment intent identifier.</returns>
    public async Task<string?> GetCheckoutSessionPaymentIntentIdAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID cannot be null or whitespace.", nameof(sessionId));

        try
        {
            var session = await new SessionService().GetAsync(sessionId);
            return session.PaymentIntentId;
        }
        catch (StripeException)
        {
            return null;
        }
    }

    private static string NormalizeBillingInterval(string billingPeriod)
    {
        return billingPeriod.Trim().ToLowerInvariant() switch
        {
            "week" => "week",
            "month" => "month",
            "year" => "year",
            _ => throw new ArgumentException($"Unsupported billing period: {billingPeriod}", nameof(billingPeriod))
        };
    }
}
