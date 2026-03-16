using Stripe;
using System.Security.Claims;

namespace Saas.Infra.Services.Payment;

public interface IPaymentGateway
{
    string GatewayName { get; }
    Task<PaymentIntentResult> CreatePaymentIntentAsync(long amount, string currency, Dictionary<string, string> metadata);
    Task<PaymentResult> ConfirmPaymentAsync(string paymentIntentId);
    Task<bool> VerifyWebhookSignatureAsync(string payload, string signature);
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Guid priceId, long amount, string currency, string productName, string billingPeriod, Dictionary<string, string> metadata, string successUrl, string cancelUrl);
    Task<string?> GetCheckoutSessionPaymentIntentIdAsync(string sessionId);
}

public interface IPaymentUrlResolver
{
    string ResolveBaseUrl(string scheme, string host);
}

public interface ISubscriptionTokenService
{
    SubscriptionTokenResult GenerateToken(SubscriptionTokenRequest request);
}

public interface IPaymentService
{
    Task<PaymentIntentResult> CreatePaymentIntentAsync(Guid userId, Guid priceId, string gateway);
    Task<Guid> ConfirmPaymentAndCreateSubscriptionAsync(string paymentIntentId, Guid userId, Guid orderId, string gateway);
}

public interface IStripeWebhookService
{
    Task ProcessEventAsync(Event stripeEvent);
}

public interface IUserContextService
{
    Task<Guid> ResolveUserIdAsync(ClaimsPrincipal principal);
}

public class CheckoutSessionResult
{
    public string SessionId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class PaymentIntentResult
{
    public Guid OrderId { get; set; }
    public string ClientSecret { get; set; } = string.Empty;
    public string PaymentIntentId { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class PaymentResult
{
    public bool Succeeded { get; set; }
    public string PaymentIntentId { get; set; } = string.Empty;
    public string? ExternalTransactionId { get; set; }
    public long Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public class SubscriptionTokenRequest
{
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid SubscriptionId { get; set; }
    public short SubscriptionStatus { get; set; }
    public DateTimeOffset SubscriptionStartUtc { get; set; }
    public DateTimeOffset? SubscriptionEndUtc { get; set; }
    public Guid OrderId { get; set; }
}

public class SubscriptionTokenResult
{
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

public class CreateOrderResult
{
    public Guid OrderId { get; set; }
    public short Status { get; set; }
    public Guid ProductId { get; set; }
    public Guid PriceId { get; set; }
    public long OriginalAmount { get; set; }
    public long ActualAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string ProductName { get; set; } = string.Empty;
    public string PriceName { get; set; } = string.Empty;
    public string BillingPeriod { get; set; } = string.Empty;
    public string PaymentUrl { get; set; } = string.Empty;
    public DateTimeOffset? ExpiredTime { get; set; }
}

public class PaymentStatusResult
{
    public Guid OrderId { get; set; }
    public short OrderStatus { get; set; }
    public string OrderStatusText { get; set; } = string.Empty;
    public bool Paid { get; set; }
    public Guid? SubscriptionId { get; set; }
    public Guid? TransactionId { get; set; }
    public short? TransactionStatus { get; set; }
    public string? ExternalTransactionId { get; set; }
    public DateTimeOffset? PaidTime { get; set; }
    public DateTimeOffset? ExpiredTime { get; set; }
    public string? SubscriptionAccessToken { get; set; }
    public int? SubscriptionTokenExpiresIn { get; set; }
}

public class SubscriptionTransactionResult
{
    public Guid Id { get; set; }
    public long Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public short Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string? ExternalTransactionId { get; set; }
    public DateTimeOffset CreatedTime { get; set; }
    public string? Remarks { get; set; }
}

public class ExportFileResult
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}
