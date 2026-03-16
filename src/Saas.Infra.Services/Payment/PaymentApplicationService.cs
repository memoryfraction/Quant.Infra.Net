using Microsoft.EntityFrameworkCore;
using Saas.Infra.Core;
using Saas.Infra.Data;
using Stripe;
using System.Text;
using System.Xml.Linq;

namespace Saas.Infra.Services.Payment;

/// <summary>
/// 支付应用服务抽象。
/// Payment application service abstraction。
/// </summary>
public interface IPaymentApplicationService
{
    /// <summary>
    /// 创建订单并生成支付地址。
    /// Creates an order and generates the payment URL.
    /// </summary>
    /// <param name="userId">用户标识。 / User identifier.</param>
    /// <param name="priceId">价格标识。 / Price identifier.</param>
    /// <param name="scheme">请求协议。 / Request scheme.</param>
    /// <param name="host">请求主机。 / Request host.</param>
    /// <returns>创建订单结果。 / Create order result.</returns>
    Task<CreateOrderResult> CreateOrderAsync(Guid userId, Guid priceId, string scheme, string host);

    /// <summary>
    /// 为现有订单创建结账会话。
    /// Creates a checkout session for an existing order.
    /// </summary>
    /// <param name="orderId">订单标识。 / Order identifier.</param>
    /// <param name="userId">用户标识。 / User identifier.</param>
    /// <param name="scheme">请求协议。 / Request scheme.</param>
    /// <param name="host">请求主机。 / Request host.</param>
    /// <returns>结账会话结果。 / Checkout session result.</returns>
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Guid orderId, Guid userId, string scheme, string host);

    /// <summary>
    /// 获取支付状态。
    /// Gets the payment status.
    /// </summary>
    /// <param name="orderId">订单标识。 / Order identifier.</param>
    /// <param name="userId">用户标识。 / User identifier.</param>
    /// <returns>支付状态结果。 / Payment status result.</returns>
    Task<PaymentStatusResult?> GetPaymentStatusAsync(Guid orderId, Guid userId);

    /// <summary>
    /// 验证并处理 Stripe Webhook。
    /// Verifies and processes a Stripe webhook.
    /// </summary>
    /// <param name="payload">Webhook 负载。 / Webhook payload.</param>
    /// <param name="signature">Webhook 签名。 / Webhook signature.</param>
    /// <returns>处理是否成功。 / Whether processing succeeded.</returns>
    Task<bool> VerifyAndProcessStripeWebhookAsync(string payload, string signature);
}

/// <summary>
/// 订阅应用服务抽象。
/// Subscription application service abstraction。
/// </summary>
public interface ISubscriptionApplicationService
{
    /// <summary>
    /// 获取当前用户订阅列表。
    /// Gets the subscriptions for the current user.
    /// </summary>
    /// <param name="userId">用户标识。 / User identifier.</param>
    /// <param name="activeOnly">是否仅返回激活订阅。 / Whether to return active subscriptions only.</param>
    /// <returns>订阅列表。 / Subscription list.</returns>
    Task<List<SubscriptionEntity>> GetMySubscriptionsAsync(Guid userId, bool activeOnly);

    /// <summary>
    /// 获取订阅详情。
    /// Gets subscription details.
    /// </summary>
    /// <param name="id">订阅标识。 / Subscription identifier.</param>
    /// <returns>订阅实体。 / Subscription entity.</returns>
    Task<SubscriptionEntity?> GetSubscriptionAsync(Guid id);

    /// <summary>
    /// 取消订阅。
    /// Cancels a subscription.
    /// </summary>
    /// <param name="id">订阅标识。 / Subscription identifier.</param>
    /// <returns>订阅实体。 / Subscription entity.</returns>
    Task<SubscriptionEntity?> CancelSubscriptionAsync(Guid id);

    /// <summary>
    /// 获取订阅交易记录。
    /// Gets subscription transactions.
    /// </summary>
    /// <param name="id">订阅标识。 / Subscription identifier.</param>
    /// <returns>交易列表。 / Transaction list.</returns>
    Task<List<SubscriptionTransactionResult>> GetSubscriptionTransactionsAsync(Guid id);
}

/// <summary>
/// 管理端交易导出服务抽象。
/// Admin transaction export service abstraction。
/// </summary>
public interface IAdminTransactionExportService
{
    /// <summary>
    /// 导出 CSV。
    /// Exports CSV.
    /// </summary>
    /// <param name="gateway">支付网关。 / Payment gateway.</param>
    /// <param name="status">交易状态。 / Transaction status.</param>
    /// <param name="fromDate">开始日期（UTC）。 / Start date in UTC.</param>
    /// <param name="toDate">结束日期（UTC）。 / End date in UTC.</param>
    /// <returns>导出文件。 / Export file.</returns>
    Task<ExportFileResult> ExportCsvAsync(string? gateway, short? status, DateTime? fromDate, DateTime? toDate);

    /// <summary>
    /// 导出 Excel XML。
    /// Exports Excel XML.
    /// </summary>
    /// <param name="gateway">支付网关。 / Payment gateway.</param>
    /// <param name="status">交易状态。 / Transaction status.</param>
    /// <param name="fromDate">开始日期（UTC）。 / Start date in UTC.</param>
    /// <param name="toDate">结束日期（UTC）。 / End date in UTC.</param>
    /// <returns>导出文件。 / Export file.</returns>
    Task<ExportFileResult> ExportExcelAsync(string? gateway, short? status, DateTime? fromDate, DateTime? toDate);
}

/// <summary>
/// 支付应用服务。
/// Payment application service.
/// </summary>
public class PaymentApplicationService : IPaymentApplicationService
{
    private readonly ApplicationDbContext _db;
    private readonly IEnumerable<IPaymentGateway> _gateways;
    private readonly IPaymentUrlResolver _paymentUrlResolver;
    private readonly IStripeWebhookService _stripeWebhookService;
    private readonly ISubscriptionTokenService _subscriptionTokenService;

    /// <summary>
    /// 初始化支付应用服务。
    /// Initializes the payment application service.
    /// </summary>
    /// <param name="db">数据库上下文。 / Database context.</param>
    /// <param name="gateways">支付网关集合。 / Payment gateways.</param>
    /// <param name="paymentUrlResolver">支付地址解析器。 / Payment URL resolver.</param>
    /// <param name="stripeWebhookService">Stripe Webhook 服务。 / Stripe webhook service.</param>
    /// <param name="subscriptionTokenService">订阅令牌服务。 / Subscription token service.</param>
    public PaymentApplicationService(
        ApplicationDbContext db,
        IEnumerable<IPaymentGateway> gateways,
        IPaymentUrlResolver paymentUrlResolver,
        IStripeWebhookService stripeWebhookService,
        ISubscriptionTokenService subscriptionTokenService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _gateways = gateways ?? throw new ArgumentNullException(nameof(gateways));
        _paymentUrlResolver = paymentUrlResolver ?? throw new ArgumentNullException(nameof(paymentUrlResolver));
        _stripeWebhookService = stripeWebhookService ?? throw new ArgumentNullException(nameof(stripeWebhookService));
        _subscriptionTokenService = subscriptionTokenService ?? throw new ArgumentNullException(nameof(subscriptionTokenService));
    }

    /// <inheritdoc />
    public async Task<CreateOrderResult> CreateOrderAsync(Guid userId, Guid priceId, string scheme, string host)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        if (priceId == Guid.Empty)
            throw new ArgumentException("Price ID cannot be empty.", nameof(priceId));
        if (string.IsNullOrWhiteSpace(scheme))
            throw new ArgumentException("Scheme cannot be null or whitespace.", nameof(scheme));
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host cannot be null or whitespace.", nameof(host));

        var price = await _db.Prices
            .Include(p => p.Product)
            .FirstOrDefaultAsync(p => p.Id == priceId);

        if (price == null || !price.IsActive)
            throw new InvalidOperationException("Price is invalid or inactive.");
        if (price.Product == null || !price.Product.IsActive)
            throw new InvalidOperationException("Product is invalid or inactive.");

        var order = new OrderEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProductId = price.ProductId,
            PriceId = price.Id,
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

        var checkoutSession = await CreateStripeCheckoutForOrderAsync(order, userId, scheme, host, price);

        return new CreateOrderResult
        {
            OrderId = order.Id,
            Status = (OrderStatus)order.Status,
            ProductId = order.ProductId,
            PriceId = order.PriceId,
            OriginalAmount = order.OriginalAmount,
            ActualAmount = order.ActualAmount,
            Currency = price.Currency,
            ProductName = price.Product.Name,
            PriceName = price.Name,
            BillingPeriod = price.BillingPeriod,
            PaymentUrl = checkoutSession.Url,
            ExpiredTime = order.ExpiredTime
        };
    }

    /// <inheritdoc />
    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Guid orderId, Guid userId, string scheme, string host)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order ID cannot be empty.", nameof(orderId));
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));
        if (string.IsNullOrWhiteSpace(scheme))
            throw new ArgumentException("Scheme cannot be null or whitespace.", nameof(scheme));
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host cannot be null or whitespace.", nameof(host));

        var order = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId && !o.IsDeleted);

        if (order == null)
            throw new InvalidOperationException("Order not found.");
        if ((OrderStatus)order.Status != OrderStatus.Pending)
            throw new InvalidOperationException("Order is not pending.");
        if (order.ExpiredTime.HasValue && order.ExpiredTime.Value <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Order has expired.");

        var price = await _db.Prices
            .AsNoTracking()
            .Include(p => p.Product)
            .FirstOrDefaultAsync(p => p.Id == order.PriceId);

        if (price == null || !price.IsActive)
            throw new InvalidOperationException("Price is invalid or inactive.");
        if (price.Product == null || !price.Product.IsActive)
            throw new InvalidOperationException("Product is invalid or inactive.");

        return await CreateStripeCheckoutForOrderAsync(order, userId, scheme, host, price);
    }

    /// <inheritdoc />
    public async Task<PaymentStatusResult?> GetPaymentStatusAsync(Guid orderId, Guid userId)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("Order ID cannot be empty.", nameof(orderId));
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        var order = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId && !o.IsDeleted);

        if (order == null)
            return null;

        var latestTransaction = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.OrderId == order.Id)
            .OrderByDescending(t => t.CreatedTime)
            .FirstOrDefaultAsync();

        var subscription = order.SubscriptionId.HasValue
            ? await _db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == order.SubscriptionId.Value && !s.IsDeleted)
            : null;
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == order.UserId && !u.IsDeleted);

        string? subscriptionAccessToken = null;
        int? tokenExpiresIn = null;
        if ((OrderStatus)order.Status == OrderStatus.Paid && subscription != null && user != null)
        {
            var productName = await _db.Products
                .AsNoTracking()
                .Where(p => p.Id == order.ProductId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync() ?? "UnknownProduct";

            var tokenResult = _subscriptionTokenService.GenerateToken(new SubscriptionTokenRequest
            {
                UserId = user.Id,
                UserEmail = user.Email,
                ProductId = order.ProductId,
                ProductName = productName,
                SubscriptionId = subscription.Id,
                SubscriptionStatus = (SubscriptionStatus)subscription.Status,
                SubscriptionStartUtc = subscription.StartDate,
                SubscriptionEndUtc = subscription.EndDate,
                OrderId = order.Id
            });

            subscriptionAccessToken = tokenResult.AccessToken;
            tokenExpiresIn = tokenResult.ExpiresIn;
        }

        var orderStatus = (OrderStatus)order.Status;
        return new PaymentStatusResult
        {
            OrderId = order.Id,
            OrderStatus = orderStatus,
            OrderStatusText = GetOrderStatusText(orderStatus),
            Paid = orderStatus == OrderStatus.Paid,
            SubscriptionId = order.SubscriptionId,
            TransactionId = latestTransaction?.Id,
            TransactionStatus = latestTransaction != null ? (TransactionStatus)latestTransaction.Status : null,
            ExternalTransactionId = latestTransaction?.ExternalTransactionId,
            PaidTime = order.PaidTime,
            ExpiredTime = order.ExpiredTime,
            SubscriptionAccessToken = subscriptionAccessToken,
            SubscriptionTokenExpiresIn = tokenExpiresIn
        };
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAndProcessStripeWebhookAsync(string payload, string signature)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Payload cannot be null or whitespace.", nameof(payload));
        if (string.IsNullOrWhiteSpace(signature))
            throw new ArgumentException("Signature cannot be null or whitespace.", nameof(signature));

        var stripeGateway = _gateways.FirstOrDefault(g => g.GatewayName == "Stripe")
            ?? throw new InvalidOperationException("Stripe gateway is not configured.");

        var isValid = await stripeGateway.VerifyWebhookSignatureAsync(payload, signature);
        if (!isValid)
        {
            return false;
        }

        var stripeEvent = EventUtility.ParseEvent(payload);
        await _stripeWebhookService.ProcessEventAsync(stripeEvent);
        return true;
    }

    private async Task<CheckoutSessionResult> CreateStripeCheckoutForOrderAsync(OrderEntity order, Guid userId, string scheme, string host, PriceEntity price)
    {
        var stripeGateway = _gateways.FirstOrDefault(g => g.GatewayName == "Stripe")
            ?? throw new InvalidOperationException("Stripe gateway is not configured.");

        var publicBaseUrl = _paymentUrlResolver.ResolveBaseUrl(scheme, host);
        var successUrl = $"{publicBaseUrl}/checkout?payment=success&orderId={order.Id}&session_id={{CHECKOUT_SESSION_ID}}";
        var cancelUrl = $"{publicBaseUrl}/checkout?payment=cancel&orderId={order.Id}";
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
            price.Product?.Name ?? "Subscription",
            price.BillingPeriod,
            metadata,
            successUrl,
            cancelUrl);

        if (string.IsNullOrWhiteSpace(checkoutSession.Url))
            throw new InvalidOperationException("Failed to create Stripe checkout session.");

        return checkoutSession;
    }

    private static string GetOrderStatusText(OrderStatus status)
    {
        return status switch
        {
            OrderStatus.Pending => "Pending",
            OrderStatus.Paid => "Paid",
            OrderStatus.Cancelled => "Cancelled",
            OrderStatus.Refunded => "Refunded",
            _ => $"Unknown({(short)status})"
        };
    }
}

/// <summary>
/// 订阅应用服务。
/// Subscription application service。
/// </summary>
public class SubscriptionApplicationService : ISubscriptionApplicationService
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// 初始化订阅应用服务。
    /// Initializes the subscription application service。
    /// </summary>
    /// <param name="db">数据库上下文。 / Database context.</param>
    public SubscriptionApplicationService(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <inheritdoc />
    public Task<List<SubscriptionEntity>> GetMySubscriptionsAsync(Guid userId, bool activeOnly)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        var query = _db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.Price)
            .Where(s => s.UserId == userId && !s.IsDeleted);

        if (activeOnly)
        {
            query = query.Where(s => s.Status == (short)SubscriptionStatus.Active);
        }

        return query.OrderByDescending(s => s.CreatedTime).ToListAsync();
    }

    /// <inheritdoc />
    public Task<SubscriptionEntity?> GetSubscriptionAsync(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Subscription ID cannot be empty.", nameof(id));

        return _db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.Price)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    /// <inheritdoc />
    public async Task<SubscriptionEntity?> CancelSubscriptionAsync(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Subscription ID cannot be empty.", nameof(id));

        var subscription = await _db.Subscriptions.FindAsync(id);
        if (subscription == null)
        {
            return null;
        }

        if ((SubscriptionStatus)subscription.Status == SubscriptionStatus.Cancelled)
            throw new InvalidOperationException("Subscription is already cancelled.");
        if ((SubscriptionStatus)subscription.Status == SubscriptionStatus.Expired)
            throw new InvalidOperationException("Subscription is already expired.");

        subscription.Status = (short)SubscriptionStatus.Cancelled;
        subscription.AutoRenew = false;
        await _db.SaveChangesAsync();
        return subscription;
    }

    /// <inheritdoc />
    public async Task<List<SubscriptionTransactionResult>> GetSubscriptionTransactionsAsync(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Subscription ID cannot be empty.", nameof(id));

        return await _db.Transactions
            .AsNoTracking()
            .Where(t => t.SubscriptionId == id)
            .OrderByDescending(t => t.CreatedTime)
            .Select(t => new SubscriptionTransactionResult
            {
                Id = t.Id,
                Amount = t.Amount,
                Currency = t.Currency,
                Gateway = t.Gateway,
                Status = (TransactionStatus)t.Status,
                StatusText = GetTransactionStatusText((TransactionStatus)t.Status),
                ExternalTransactionId = t.ExternalTransactionId,
                CreatedTime = t.CreatedTime,
                Remarks = t.Remarks
            })
            .ToListAsync();
    }

    private static string GetTransactionStatusText(TransactionStatus status)
    {
        return status switch
        {
            TransactionStatus.Pending => "Pending",
            TransactionStatus.Success => "Success",
            TransactionStatus.Failed => "Failed",
            TransactionStatus.Refunded => "Refunded",
            _ => $"Unknown({(short)status})"
        };
    }
}

/// <summary>
/// 管理端交易导出服务。
/// Admin transaction export service。
/// </summary>
public class AdminTransactionExportService : IAdminTransactionExportService
{
    private readonly ApplicationDbContext _db;

    /// <summary>
    /// 初始化管理端交易导出服务。
    /// Initializes the admin transaction export service。
    /// </summary>
    /// <param name="db">数据库上下文。 / Database context.</param>
    public AdminTransactionExportService(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <inheritdoc />
    public async Task<ExportFileResult> ExportCsvAsync(string? gateway, short? status, DateTime? fromDate, DateTime? toDate)
    {
        ValidateFilter(gateway, fromDate, toDate);
        var items = await QueryTransactionsAsync(gateway, status, fromDate, toDate);

        var builder = new StringBuilder();
        builder.AppendLine("TransactionId,UserName,UserEmail,Amount,Currency,Gateway,Status,ExternalTransactionId,SubscriptionId,CreatedTime,Remarks");
        foreach (var item in items)
        {
            builder.AppendLine(string.Join(",",
                EscapeCsv(item.Id.ToString()),
                EscapeCsv(item.UserName),
                EscapeCsv(item.UserEmail),
                EscapeCsv((item.Amount / 100.0m).ToString("F2")),
                EscapeCsv(item.Currency),
                EscapeCsv(item.Gateway),
                EscapeCsv(GetTransactionStatusText((TransactionStatus)item.Status)),
                EscapeCsv(item.ExternalTransactionId ?? string.Empty),
                EscapeCsv(item.SubscriptionId?.ToString() ?? string.Empty),
                EscapeCsv(item.CreatedTime.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'")),
                EscapeCsv(item.Remarks ?? string.Empty)));
        }

        return new ExportFileResult
        {
            Content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray(),
            ContentType = "text/csv",
            FileName = $"transactions-{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
        };
    }

    /// <inheritdoc />
    public async Task<ExportFileResult> ExportExcelAsync(string? gateway, short? status, DateTime? fromDate, DateTime? toDate)
    {
        ValidateFilter(gateway, fromDate, toDate);
        var items = await QueryTransactionsAsync(gateway, status, fromDate, toDate);
        var xml = BuildExcelXml(items);

        return new ExportFileResult
        {
            Content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(xml)).ToArray(),
            ContentType = "application/vnd.ms-excel",
            FileName = $"transactions-{DateTime.UtcNow:yyyyMMddHHmmss}.xml"
        };
    }

    private static void ValidateFilter(string? gateway, DateTime? fromDate, DateTime? toDate)
    {
        if (!string.IsNullOrWhiteSpace(gateway) && gateway.Length > 50)
            throw new ArgumentException("Gateway length cannot exceed 50 characters.", nameof(gateway));
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            throw new ArgumentException("From date cannot be later than to date.");
    }

    private Task<List<ExportTransactionItem>> QueryTransactionsAsync(string? gateway, short? status, DateTime? fromDate, DateTime? toDate)
    {
        var query = _db.Transactions.AsNoTracking().Include(t => t.User).AsQueryable();
        if (!string.IsNullOrWhiteSpace(gateway))
        {
            var normalizedGateway = gateway.Trim().ToLowerInvariant();
            query = query.Where(t => t.Gateway.ToLower() == normalizedGateway);
        }
        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);
        if (fromDate.HasValue)
            query = query.Where(t => t.CreatedTime >= new DateTimeOffset(DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Utc)));
        if (toDate.HasValue)
            query = query.Where(t => t.CreatedTime < new DateTimeOffset(DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Utc)));

        return query
            .OrderByDescending(t => t.CreatedTime)
            .Take(5000)
            .Select(t => new ExportTransactionItem
            {
                Id = t.Id,
                UserName = t.User != null ? (t.User.UserName ?? string.Empty) : string.Empty,
                UserEmail = t.User != null ? (t.User.Email ?? string.Empty) : string.Empty,
                Amount = t.Amount,
                Currency = t.Currency,
                Gateway = t.Gateway,
                Status = t.Status,
                ExternalTransactionId = t.ExternalTransactionId,
                SubscriptionId = t.SubscriptionId,
                CreatedTime = t.CreatedTime,
                Remarks = t.Remarks
            })
            .ToListAsync();
    }

    private static string BuildExcelXml(IEnumerable<ExportTransactionItem> items)
    {
        var spreadsheetNamespace = XNamespace.Get("urn:schemas-microsoft-com:office:spreadsheet");
        var workbook = new XElement(spreadsheetNamespace + "Workbook",
            new XAttribute(XNamespace.Xmlns + "ss", spreadsheetNamespace.NamespaceName),
            new XElement(spreadsheetNamespace + "Worksheet",
                new XAttribute(spreadsheetNamespace + "Name", "Transactions"),
                new XElement(spreadsheetNamespace + "Table",
                    CreateRow(spreadsheetNamespace,
                        "Transaction ID",
                        "User Name",
                        "User Email",
                        "Amount",
                        "Currency",
                        "Gateway",
                        "Status",
                        "External Transaction ID",
                        "Subscription ID",
                        "Created Time UTC",
                        "Remarks"),
                    items.Select(item => CreateRow(spreadsheetNamespace,
                        item.Id.ToString(),
                        item.UserName,
                        item.UserEmail,
                        (item.Amount / 100.0m).ToString("F2"),
                        item.Currency,
                        item.Gateway,
                        GetTransactionStatusText((TransactionStatus)item.Status),
                        item.ExternalTransactionId ?? string.Empty,
                        item.SubscriptionId?.ToString() ?? string.Empty,
                        item.CreatedTime.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        item.Remarks ?? string.Empty)))));

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), workbook).ToString();
    }

    private static XElement CreateRow(XNamespace spreadsheetNamespace, params string[] values)
    {
        return new XElement(spreadsheetNamespace + "Row",
            values.Select(value =>
                new XElement(spreadsheetNamespace + "Cell",
                    new XElement(spreadsheetNamespace + "Data",
                        new XAttribute(spreadsheetNamespace + "Type", "String"),
                        value ?? string.Empty))));
    }

    private static string EscapeCsv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static string GetTransactionStatusText(TransactionStatus status)
    {
        return status switch
        {
            TransactionStatus.Pending => "Pending",
            TransactionStatus.Success => "Success",
            TransactionStatus.Failed => "Failed",
            TransactionStatus.Refunded => "Refunded",
            _ => $"Unknown({(short)status})"
        };
    }

    private sealed class ExportTransactionItem
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public short Status { get; set; }
        public string? ExternalTransactionId { get; set; }
        public Guid? SubscriptionId { get; set; }
        public DateTimeOffset CreatedTime { get; set; }
        public string? Remarks { get; set; }
    }
}
