using Microsoft.EntityFrameworkCore;
using Saas.Infra.Data;
using Serilog;
using Stripe;
using System.Text;
using System.Xml.Linq;

namespace Saas.Infra.Services.Payment;

public interface IPaymentApplicationService
{
    Task<CreateOrderResult> CreateOrderAsync(Guid userId, Guid priceId, string scheme, string host);
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Guid orderId, Guid userId, string scheme, string host);
    Task<PaymentStatusResult?> GetPaymentStatusAsync(Guid orderId, Guid userId);
    Task<bool> VerifyAndProcessStripeWebhookAsync(string payload, string signature);
}

public interface ISubscriptionApplicationService
{
    Task<List<SubscriptionEntity>> GetMySubscriptionsAsync(Guid userId, bool activeOnly);
    Task<SubscriptionEntity?> GetSubscriptionAsync(Guid id);
    Task<SubscriptionEntity?> CancelSubscriptionAsync(Guid id);
    Task<List<SubscriptionTransactionResult>> GetSubscriptionTransactionsAsync(Guid id);
}

public interface IAdminTransactionExportService
{
    Task<ExportFileResult> ExportCsvAsync(string? gateway, short? status, DateTime? fromDate, DateTime? toDate);
    Task<ExportFileResult> ExportExcelAsync(string? gateway, short? status, DateTime? fromDate, DateTime? toDate);
}

public class PaymentApplicationService : IPaymentApplicationService
{
    private readonly ApplicationDbContext _db;
    private readonly IEnumerable<IPaymentGateway> _gateways;
    private readonly IPaymentUrlResolver _paymentUrlResolver;
    private readonly IStripeWebhookService _stripeWebhookService;
    private readonly ISubscriptionTokenService _subscriptionTokenService;

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

    public async Task<CreateOrderResult> CreateOrderAsync(Guid userId, Guid priceId, string scheme, string host)
    {
        var price = await _db.Prices
            .Include(p => p.Product)
            .FirstOrDefaultAsync(p => p.Id == priceId);

        if (price == null || !price.IsActive)
            throw new InvalidOperationException("Price is invalid or inactive");
        if (price.Product == null || !price.Product.IsActive)
            throw new InvalidOperationException("Product is invalid or inactive");

        var order = new OrderEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
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

        var checkoutSession = await CreateStripeCheckoutForOrderAsync(order, userId, scheme, host, price);

        return new CreateOrderResult
        {
            OrderId = order.Id,
            Status = order.Status,
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

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Guid orderId, Guid userId, string scheme, string host)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId && !o.IsDeleted);

        if (order == null)
            throw new InvalidOperationException("Order not found");
        if (order.Status != 0)
            throw new InvalidOperationException("Order is not pending");
        if (order.ExpiredTime.HasValue && order.ExpiredTime.Value <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Order has expired");

        var price = await _db.Prices
            .AsNoTracking()
            .Include(p => p.Product)
            .FirstOrDefaultAsync(p => p.Id == order.PriceId);

        if (price == null || !price.IsActive)
            throw new InvalidOperationException("Price is invalid or inactive");
        if (price.Product == null || !price.Product.IsActive)
            throw new InvalidOperationException("Product is invalid or inactive");

        return await CreateStripeCheckoutForOrderAsync(order, userId, scheme, host, price);
    }

    public async Task<PaymentStatusResult?> GetPaymentStatusAsync(Guid orderId, Guid userId)
    {
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
        if (order.Status == 1 && subscription != null && user != null)
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
                SubscriptionStatus = subscription.Status,
                SubscriptionStartUtc = subscription.StartDate,
                SubscriptionEndUtc = subscription.EndDate,
                OrderId = order.Id
            });

            subscriptionAccessToken = tokenResult.AccessToken;
            tokenExpiresIn = tokenResult.ExpiresIn;
        }

        return new PaymentStatusResult
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
    }

    public async Task<bool> VerifyAndProcessStripeWebhookAsync(string payload, string signature)
    {
        var stripeGateway = _gateways.FirstOrDefault(g => g.GatewayName == "Stripe")
            ?? throw new InvalidOperationException("Stripe gateway is not configured");

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
            ?? throw new InvalidOperationException("Stripe gateway is not configured");

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
}

public class SubscriptionApplicationService : ISubscriptionApplicationService
{
    private readonly ApplicationDbContext _db;

    public SubscriptionApplicationService(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<List<SubscriptionEntity>> GetMySubscriptionsAsync(Guid userId, bool activeOnly)
    {
        var query = _db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.Price)
            .Where(s => s.UserId == userId && !s.IsDeleted);

        if (activeOnly)
        {
            query = query.Where(s => s.Status == 1);
        }

        return query.OrderByDescending(s => s.CreatedTime).ToListAsync();
    }

    public Task<SubscriptionEntity?> GetSubscriptionAsync(Guid id)
    {
        return _db.Subscriptions
            .AsNoTracking()
            .Include(s => s.Product)
            .Include(s => s.Price)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<SubscriptionEntity?> CancelSubscriptionAsync(Guid id)
    {
        var subscription = await _db.Subscriptions.FindAsync(id);
        if (subscription == null)
        {
            return null;
        }

        if (subscription.Status == 2)
            throw new InvalidOperationException("Subscription is already cancelled");
        if (subscription.Status == 3)
            throw new InvalidOperationException("Subscription is already expired");

        subscription.Status = 2;
        subscription.AutoRenew = false;
        await _db.SaveChangesAsync();
        return subscription;
    }

    public async Task<List<SubscriptionTransactionResult>> GetSubscriptionTransactionsAsync(Guid id)
    {
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
                Status = t.Status,
                StatusText = t.Status == 0 ? "Pending" : t.Status == 1 ? "Success" : t.Status == 2 ? "Failed" : t.Status == 3 ? "Refunded" : "Unknown",
                ExternalTransactionId = t.ExternalTransactionId,
                CreatedTime = t.CreatedTime,
                Remarks = t.Remarks
            })
            .ToListAsync();
    }
}

public class AdminTransactionExportService : IAdminTransactionExportService
{
    private readonly ApplicationDbContext _db;

    public AdminTransactionExportService(ApplicationDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

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
                EscapeCsv(GetStatusText(item.Status)),
                EscapeCsv(item.ExternalTransactionId ?? string.Empty),
                EscapeCsv(item.SubscriptionId?.ToString() ?? string.Empty),
                EscapeCsv(item.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss zzz")),
                EscapeCsv(item.Remarks ?? string.Empty)));
        }

        return new ExportFileResult
        {
            Content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray(),
            ContentType = "text/csv",
            FileName = $"transactions-{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
        };
    }

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
            throw new ArgumentException("Gateway length cannot exceed 50 characters", nameof(gateway));
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value.Date > toDate.Value.Date)
            throw new ArgumentException("From date cannot be later than to date");
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
            query = query.Where(t => t.CreatedTime >= new DateTimeOffset(fromDate.Value.Date, TimeSpan.Zero));
        if (toDate.HasValue)
            query = query.Where(t => t.CreatedTime < new DateTimeOffset(toDate.Value.Date.AddDays(1), TimeSpan.Zero));

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
                        "Created Time",
                        "Remarks"),
                    items.Select(item => CreateRow(spreadsheetNamespace,
                        item.Id.ToString(),
                        item.UserName,
                        item.UserEmail,
                        (item.Amount / 100.0m).ToString("F2"),
                        item.Currency,
                        item.Gateway,
                        GetStatusText(item.Status),
                        item.ExternalTransactionId ?? string.Empty,
                        item.SubscriptionId?.ToString() ?? string.Empty,
                        item.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss"),
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

    private static string GetStatusText(short status)
    {
        return status switch
        {
            0 => "Pending",
            1 => "Success",
            2 => "Failed",
            3 => "Refunded",
            _ => "Unknown"
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
