using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Saas.Infra.Core;
using Saas.Infra.Data;
using Serilog;
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

    public PaymentUrlResolver(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public string ResolveBaseUrl(string scheme, string host)
    {
        if (string.IsNullOrWhiteSpace(scheme))
            throw new ArgumentException("Scheme cannot be null or empty", nameof(scheme));
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host cannot be null or empty", nameof(host));

        var runtimeEnv = UtilityService.GetCurrentEnvironment();
        if (runtimeEnv == RuntimeEnvironment.AzureContainerApps)
        {
            var hostName = Environment.GetEnvironmentVariable("CONTAINER_APP_HOSTNAME");
            if (!string.IsNullOrWhiteSpace(hostName))
            {
                var resolvedFromAca = $"https://{hostName.Trim().TrimEnd('/')}";
                Log.Debug("Payment redirect base URL resolved from CONTAINER_APP_HOSTNAME: {BaseUrl}", resolvedFromAca);
                return resolvedFromAca;
            }
        }

        var manualUrl = _configuration["Payment:PublicBaseUrl"];
        if (!string.IsNullOrWhiteSpace(manualUrl))
        {
            var resolvedFromConfig = manualUrl.TrimEnd('/');
            Log.Debug("Payment redirect base URL resolved from Payment:PublicBaseUrl: {BaseUrl}", resolvedFromConfig);
            return resolvedFromConfig;
        }

        var resolvedFromRequest = $"{scheme.Trim().ToLower()}://{host.Trim()}";
        Log.Debug("Payment redirect base URL resolved from Request context: {BaseUrl}", resolvedFromRequest);
        return resolvedFromRequest;
    }
}

public class SubscriptionTokenService : ISubscriptionTokenService
{
    private const string DefaultIssuer = "Saas.Infra.Server";
    private readonly IConfiguration _configuration;
    private readonly RsaSecurityKey _jwtSigningKey;

    public SubscriptionTokenService(IConfiguration configuration, RsaSecurityKey jwtSigningKey)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _jwtSigningKey = jwtSigningKey ?? throw new ArgumentNullException(nameof(jwtSigningKey));
    }

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

        Log.Debug("Subscription token generated for user {UserId}, subscription {SubscriptionId}", request.UserId, request.SubscriptionId);

        return new SubscriptionTokenResult
        {
            AccessToken = accessToken,
            ExpiresIn = expiresIn
        };
    }

    private static string ComputeContextHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
}

public class UserContextService : IUserContextService
{
    private readonly ApplicationDbContext _db;

    public UserContextService(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Guid> ResolveUserIdAsync(ClaimsPrincipal principal)
    {
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

    public PaymentService(IEnumerable<IPaymentGateway> gateways, ApplicationDbContext db)
    {
        _gateways = gateways ?? throw new ArgumentNullException(nameof(gateways));
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(Guid userId, Guid priceId, string gateway)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Invalid user ID", nameof(userId));
        if (priceId == Guid.Empty)
            throw new ArgumentException("Invalid price ID", nameof(priceId));
        if (string.IsNullOrWhiteSpace(gateway))
            throw new ArgumentNullException(nameof(gateway));

        var price = await _db.Prices
            .Include(p => p.Product)
            .FirstOrDefaultAsync(p => p.Id == priceId);

        if (price == null)
            throw new InvalidOperationException("Price not found");
        if (!price.IsActive)
            throw new InvalidOperationException("Price is not active");
        if (price.Product == null || !price.Product.IsActive)
            throw new InvalidOperationException("Product is not active");

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
            Status = 0,
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
        Log.Information("Payment intent created for order {OrderId}, user {UserId}, price {PriceId}, gateway {Gateway}", order.Id, userId, priceId, gateway);
        return result;
    }

    public async Task<Guid> ConfirmPaymentAndCreateSubscriptionAsync(string paymentIntentId, Guid userId, Guid orderId, string gateway)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            throw new ArgumentNullException(nameof(paymentIntentId));
        if (userId == Guid.Empty)
            throw new ArgumentException("Invalid user ID", nameof(userId));
        if (orderId == Guid.Empty)
            throw new ArgumentException("Invalid order ID", nameof(orderId));
        if (string.IsNullOrWhiteSpace(gateway))
            throw new ArgumentNullException(nameof(gateway));

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
        if (order == null)
            throw new InvalidOperationException("Order not found");
        if (order.UserId != userId)
            throw new InvalidOperationException("Order does not belong to the current user");
        if (order.Status != 0)
            throw new InvalidOperationException("Order is not pending");
        if (order.ExpiredTime.HasValue && order.ExpiredTime.Value <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Order has expired");

        var price = await _db.Prices.Include(p => p.Product).FirstOrDefaultAsync(p => p.Id == order.PriceId);
        if (price == null)
            throw new InvalidOperationException("Price not found");

        var paymentResult = await ResolveGateway(gateway).ConfirmPaymentAsync(paymentIntentId);
        if (!paymentResult.Succeeded)
        {
            Log.Warning("Payment {PaymentIntentId} failed: {Error}", paymentIntentId, paymentResult.ErrorMessage);
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
                Status = 1,
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
                Status = 1,
                CreatedTime = DateTimeOffset.UtcNow,
                Remarks = $"Payment via {gateway}"
            });

            order.SubscriptionId = subscription.Id;
            order.ActualAmount = paymentResult.Amount;
            order.Status = 1;
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
            ?? throw new InvalidOperationException($"Payment gateway '{gateway}' not supported");
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

    public StripeWebhookService(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

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
                Log.Information("Stripe event {EventType} is ignored in current handler", stripeEvent.Type);
                break;
        }
    }

    private async Task HandleCheckoutCompletedAsync(Session session)
    {
        var orderId = TryParseMetadataGuid(session.Metadata, "orderId");
        if (orderId == Guid.Empty)
            return;

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
        if (order == null || order.Status == 1)
            return;

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            order.Status = 1;
            order.PaidTime = DateTimeOffset.UtcNow;

            var price = await _db.Prices.AsNoTracking().FirstOrDefaultAsync(p => p.Id == order.PriceId)
                ?? throw new InvalidOperationException($"Price {order.PriceId} not found for order {order.Id}");

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
                    Status = 1,
                    StartDate = DateTimeOffset.UtcNow,
                    EndDate = CalculateEndDate(price.BillingPeriod),
                    AutoRenew = true,
                    CreatedTime = DateTimeOffset.UtcNow,
                    IsDeleted = false
                };
                _db.Subscriptions.Add(subscription);
            }

            order.SubscriptionId = subscription.Id;
            var existingTransaction = await _db.Transactions.FirstOrDefaultAsync(t => t.OrderId == order.Id && t.Status == 1);
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
                    Status = 1,
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
        if (order == null || order.Status == 1)
            return;

        if (order.Status != 2)
        {
            order.Status = 2;
            await _db.SaveChangesAsync();
        }
    }

    private async Task HandlePaymentFailedAsync(PaymentIntent intent)
    {
        var orderId = TryParseMetadataGuid(intent.Metadata, "orderId");
        if (orderId == Guid.Empty)
            return;

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
        if (order == null || order.Status == 1)
            return;

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

    public StripePaymentGateway(string secretKey, string webhookSecret)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
            throw new ArgumentNullException(nameof(secretKey));

        _webhookSecret = webhookSecret ?? string.Empty;
        StripeConfiguration.ApiKey = secretKey;
    }

    public string GatewayName => "Stripe";

    public async Task<PaymentIntentResult> CreatePaymentIntentAsync(long amount, string currency, Dictionary<string, string> metadata)
    {
        var intent = await new PaymentIntentService().CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = amount,
            Currency = currency.ToLower(),
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

    public async Task<PaymentResult> ConfirmPaymentAsync(string paymentIntentId)
    {
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

    public Task<bool> VerifyWebhookSignatureAsync(string payload, string signature)
    {
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

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Guid priceId, long amount, string currency, string productName, string billingPeriod, Dictionary<string, string> metadata, string successUrl, string cancelUrl)
    {
        var session = await new SessionService().CreateAsync(new SessionCreateOptions
        {
            PaymentMethodTypes = new List<string> { "card" },
            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = currency.ToLower(),
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

    public async Task<string?> GetCheckoutSessionPaymentIntentIdAsync(string sessionId)
    {
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
