using Saas.Infra.Core;
using Stripe;
using System.Security.Claims;

namespace Saas.Infra.Services.Payment;

/// <summary>
/// 支付网关抽象。
/// Payment gateway abstraction。
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// 网关名称。
    /// Gateway name.
    /// </summary>
    string GatewayName { get; }

    /// <summary>
    /// 创建支付意图。
    /// Creates a payment intent.
    /// </summary>
    /// <param name="amount">金额（最小货币单位）。 / Amount in the smallest currency unit.</param>
    /// <param name="currency">货币代码。 / Currency code.</param>
    /// <param name="metadata">元数据。 / Metadata.</param>
    /// <returns>支付意图结果。 / Payment intent result.</returns>
    Task<PaymentIntentResult> CreatePaymentIntentAsync(long amount, string currency, Dictionary<string, string> metadata);

    /// <summary>
    /// 确认支付结果。
    /// Confirms the payment result.
    /// </summary>
    /// <param name="paymentIntentId">支付意图标识。 / Payment intent identifier.</param>
    /// <returns>支付结果。 / Payment result.</returns>
    Task<PaymentResult> ConfirmPaymentAsync(string paymentIntentId);

    /// <summary>
    /// 验证 Webhook 签名。
    /// Verifies a webhook signature.
    /// </summary>
    /// <param name="payload">Webhook 负载。 / Webhook payload.</param>
    /// <param name="signature">Webhook 签名。 / Webhook signature.</param>
    /// <returns>是否有效。 / Whether the signature is valid.</returns>
    Task<bool> VerifyWebhookSignatureAsync(string payload, string signature);

    /// <summary>
    /// 创建结账会话。
    /// Creates a checkout session.
    /// </summary>
    /// <param name="priceId">价格标识。 / Price identifier.</param>
    /// <param name="amount">金额。 / Amount.</param>
    /// <param name="currency">货币代码。 / Currency code.</param>
    /// <param name="productName">产品名称。 / Product name.</param>
    /// <param name="billingPeriod">计费周期。 / Billing period.</param>
    /// <param name="metadata">元数据。 / Metadata.</param>
    /// <param name="successUrl">成功回调地址。 / Success URL.</param>
    /// <param name="cancelUrl">取消回调地址。 / Cancel URL.</param>
    /// <returns>结账会话结果。 / Checkout session result.</returns>
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Guid priceId, long amount, string currency, string productName, string billingPeriod, Dictionary<string, string> metadata, string successUrl, string cancelUrl);

    /// <summary>
    /// 获取结账会话对应的支付意图标识。
    /// Gets the payment intent identifier for a checkout session.
    /// </summary>
    /// <param name="sessionId">会话标识。 / Session identifier.</param>
    /// <returns>支付意图标识。 / Payment intent identifier.</returns>
    Task<string?> GetCheckoutSessionPaymentIntentIdAsync(string sessionId);
}

/// <summary>
/// 支付回调地址解析器抽象。
/// Payment callback URL resolver abstraction。
/// </summary>
public interface IPaymentUrlResolver
{
    /// <summary>
    /// 解析公开可访问的基础地址。
    /// Resolves the publicly accessible base URL.
    /// </summary>
    /// <param name="scheme">协议。 / Request scheme.</param>
    /// <param name="host">主机。 / Request host.</param>
    /// <returns>基础地址。 / Base URL.</returns>
    string ResolveBaseUrl(string scheme, string host);
}

/// <summary>
/// 订阅令牌服务抽象。
/// Subscription token service abstraction。
/// </summary>
public interface ISubscriptionTokenService
{
    /// <summary>
    /// 生成订阅访问令牌。
    /// Generates a subscription access token.
    /// </summary>
    /// <param name="request">请求参数。 / Request payload.</param>
    /// <returns>令牌结果。 / Token result.</returns>
    SubscriptionTokenResult GenerateToken(SubscriptionTokenRequest request);
}

/// <summary>
/// 支付核心服务抽象。
/// Payment core service abstraction。
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// 创建支付意图。
    /// Creates a payment intent.
    /// </summary>
    /// <param name="userId">用户标识。 / User identifier.</param>
    /// <param name="priceId">价格标识。 / Price identifier.</param>
    /// <param name="gateway">网关名称。 / Gateway name.</param>
    /// <returns>支付意图结果。 / Payment intent result.</returns>
    Task<PaymentIntentResult> CreatePaymentIntentAsync(Guid userId, Guid priceId, string gateway);

    /// <summary>
    /// 确认支付并创建订阅。
    /// Confirms a payment and creates a subscription.
    /// </summary>
    /// <param name="paymentIntentId">支付意图标识。 / Payment intent identifier.</param>
    /// <param name="userId">用户标识。 / User identifier.</param>
    /// <param name="orderId">订单标识。 / Order identifier.</param>
    /// <param name="gateway">网关名称。 / Gateway name.</param>
    /// <returns>订阅标识。 / Subscription identifier.</returns>
    Task<Guid> ConfirmPaymentAndCreateSubscriptionAsync(string paymentIntentId, Guid userId, Guid orderId, string gateway);
}

/// <summary>
/// Stripe Webhook 处理服务抽象。
/// Stripe webhook processing service abstraction。
/// </summary>
public interface IStripeWebhookService
{
    /// <summary>
    /// 处理 Stripe 事件。
    /// Processes a Stripe event.
    /// </summary>
    /// <param name="stripeEvent">Stripe 事件。 / Stripe event.</param>
    /// <returns>异步任务。 / Asynchronous task.</returns>
    Task ProcessEventAsync(Event stripeEvent);
}

/// <summary>
/// 用户上下文服务抽象。
/// User context service abstraction。
/// </summary>
public interface IUserContextService
{
    /// <summary>
    /// 从当前主体解析用户标识。
    /// Resolves the user identifier from the current principal.
    /// </summary>
    /// <param name="principal">声明主体。 / Claims principal.</param>
    /// <returns>用户标识。 / User identifier.</returns>
    Task<Guid> ResolveUserIdAsync(ClaimsPrincipal principal);
}

/// <summary>
/// 结账会话结果。
/// Checkout session result。
/// </summary>
public class CheckoutSessionResult
{
    /// <summary>
    /// 会话标识。
    /// Session identifier。
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// 跳转地址。
    /// Redirect URL。
    /// </summary>
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// 支付意图结果。
/// Payment intent result。
/// </summary>
public class PaymentIntentResult
{
    /// <summary>
    /// 订单标识。
    /// Order identifier。
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// 客户端密钥。
    /// Client secret。
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// 支付意图标识。
    /// Payment intent identifier。
    /// </summary>
    public string PaymentIntentId { get; set; } = string.Empty;

    /// <summary>
    /// 金额。
    /// Amount。
    /// </summary>
    public long Amount { get; set; }

    /// <summary>
    /// 货币代码。
    /// Currency code。
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// 网关状态文本。
    /// Gateway status text。
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// 支付结果。
/// Payment result。
/// </summary>
public class PaymentResult
{
    /// <summary>
    /// 是否成功。
    /// Whether the payment succeeded。
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// 支付意图标识。
    /// Payment intent identifier。
    /// </summary>
    public string PaymentIntentId { get; set; } = string.Empty;

    /// <summary>
    /// 外部交易标识。
    /// External transaction identifier。
    /// </summary>
    public string? ExternalTransactionId { get; set; }

    /// <summary>
    /// 金额。
    /// Amount。
    /// </summary>
    public long Amount { get; set; }

    /// <summary>
    /// 货币代码。
    /// Currency code。
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// 错误消息。
    /// Error message。
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 订阅令牌请求。
/// Subscription token request。
/// </summary>
public class SubscriptionTokenRequest
{
    /// <summary>
    /// 用户标识。
    /// User identifier。
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 用户邮箱。
    /// User email。
    /// </summary>
    public string UserEmail { get; set; } = string.Empty;

    /// <summary>
    /// 产品标识。
    /// Product identifier。
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// 产品名称。
    /// Product name。
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// 订阅标识。
    /// Subscription identifier。
    /// </summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>
    /// 订阅状态。
    /// Subscription status。
    /// </summary>
    public SubscriptionStatus SubscriptionStatus { get; set; }

    /// <summary>
    /// 订阅开始时间（UTC）。
    /// Subscription start time in UTC。
    /// </summary>
    public DateTimeOffset SubscriptionStartUtc { get; set; }

    /// <summary>
    /// 订阅结束时间（UTC）。
    /// Subscription end time in UTC。
    /// </summary>
    public DateTimeOffset? SubscriptionEndUtc { get; set; }

    /// <summary>
    /// 订单标识。
    /// Order identifier。
    /// </summary>
    public Guid OrderId { get; set; }
}

/// <summary>
/// 订阅令牌结果。
/// Subscription token result。
/// </summary>
public class SubscriptionTokenResult
{
    /// <summary>
    /// 访问令牌。
    /// Access token。
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 过期秒数。
    /// Expiration time in seconds。
    /// </summary>
    public int ExpiresIn { get; set; }
}

/// <summary>
/// 创建订单结果。
/// Create order result。
/// </summary>
public class CreateOrderResult
{
    /// <summary>
    /// 订单标识。
    /// Order identifier。
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// 订单状态。
    /// Order status。
    /// </summary>
    public OrderStatus Status { get; set; }

    /// <summary>
    /// 产品标识。
    /// Product identifier。
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// 价格标识。
    /// Price identifier。
    /// </summary>
    public Guid PriceId { get; set; }

    /// <summary>
    /// 原始金额。
    /// Original amount。
    /// </summary>
    public long OriginalAmount { get; set; }

    /// <summary>
    /// 实际金额。
    /// Actual amount。
    /// </summary>
    public long ActualAmount { get; set; }

    /// <summary>
    /// 货币代码。
    /// Currency code。
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// 产品名称。
    /// Product name。
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// 价格名称。
    /// Price name。
    /// </summary>
    public string PriceName { get; set; } = string.Empty;

    /// <summary>
    /// 计费周期。
    /// Billing period。
    /// </summary>
    public string BillingPeriod { get; set; } = string.Empty;

    /// <summary>
    /// 支付地址。
    /// Payment URL。
    /// </summary>
    public string PaymentUrl { get; set; } = string.Empty;

    /// <summary>
    /// 过期时间（UTC）。
    /// Expiration time in UTC。
    /// </summary>
    public DateTimeOffset? ExpiredTime { get; set; }
}

/// <summary>
/// 支付状态结果。
/// Payment status result。
/// </summary>
public class PaymentStatusResult
{
    /// <summary>
    /// 订单标识。
    /// Order identifier。
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// 订单状态。
    /// Order status。
    /// </summary>
    public OrderStatus OrderStatus { get; set; }

    /// <summary>
    /// 订单状态文本。
    /// Order status text。
    /// </summary>
    public string OrderStatusText { get; set; } = string.Empty;

    /// <summary>
    /// 是否已支付。
    /// Whether the order is paid。
    /// </summary>
    public bool Paid { get; set; }

    /// <summary>
    /// 订阅标识。
    /// Subscription identifier。
    /// </summary>
    public Guid? SubscriptionId { get; set; }

    /// <summary>
    /// 交易标识。
    /// Transaction identifier。
    /// </summary>
    public Guid? TransactionId { get; set; }

    /// <summary>
    /// 交易状态。
    /// Transaction status。
    /// </summary>
    public TransactionStatus? TransactionStatus { get; set; }

    /// <summary>
    /// 外部交易标识。
    /// External transaction identifier。
    /// </summary>
    public string? ExternalTransactionId { get; set; }

    /// <summary>
    /// 支付时间（UTC）。
    /// Paid time in UTC。
    /// </summary>
    public DateTimeOffset? PaidTime { get; set; }

    /// <summary>
    /// 过期时间（UTC）。
    /// Expiration time in UTC。
    /// </summary>
    public DateTimeOffset? ExpiredTime { get; set; }

    /// <summary>
    /// 订阅访问令牌。
    /// Subscription access token。
    /// </summary>
    public string? SubscriptionAccessToken { get; set; }

    /// <summary>
    /// 令牌过期秒数。
    /// Token expiration in seconds。
    /// </summary>
    public int? SubscriptionTokenExpiresIn { get; set; }
}

/// <summary>
/// 订阅交易结果。
/// Subscription transaction result。
/// </summary>
public class SubscriptionTransactionResult
{
    /// <summary>
    /// 交易标识。
    /// Transaction identifier。
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 金额。
    /// Amount。
    /// </summary>
    public long Amount { get; set; }

    /// <summary>
    /// 货币代码。
    /// Currency code。
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// 网关名称。
    /// Gateway name。
    /// </summary>
    public string Gateway { get; set; } = string.Empty;

    /// <summary>
    /// 交易状态。
    /// Transaction status。
    /// </summary>
    public TransactionStatus Status { get; set; }

    /// <summary>
    /// 状态文本。
    /// Status text。
    /// </summary>
    public string StatusText { get; set; } = string.Empty;

    /// <summary>
    /// 外部交易标识。
    /// External transaction identifier。
    /// </summary>
    public string? ExternalTransactionId { get; set; }

    /// <summary>
    /// 创建时间（UTC）。
    /// Created time in UTC。
    /// </summary>
    public DateTimeOffset CreatedTime { get; set; }

    /// <summary>
    /// 备注。
    /// Remarks。
    /// </summary>
    public string? Remarks { get; set; }
}

/// <summary>
/// 导出文件结果。
/// Export file result。
/// </summary>
public class ExportFileResult
{
    /// <summary>
    /// 文件内容。
    /// File content。
    /// </summary>
    public byte[] Content { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// 内容类型。
    /// Content type。
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// 文件名。
    /// File name。
    /// </summary>
    public string FileName { get; set; } = string.Empty;
}
