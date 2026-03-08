using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Saas.Infra.Data;
using Serilog;

namespace Saas.Infra.MVC.Services.Payment
{
    /// <summary>
    /// 支付服务接口。
    /// Payment service interface.
    /// </summary>
    public interface IPaymentService
    {
        /// <summary>
        /// 创建支付意图。
        /// Creates a payment intent.
        /// </summary>
        /// <param name="userId">用户ID。 / User ID.</param>
        /// <param name="priceId">价格ID。 / Price ID.</param>
        /// <param name="gateway">支付网关名称。 / Payment gateway name.</param>
        /// <returns>支付意图结果。 / Payment intent result.</returns>
        Task<PaymentIntentResult> CreatePaymentIntentAsync(Guid userId, Guid priceId, string gateway);

        /// <summary>
        /// 确认支付并创建订阅。
        /// Confirms payment and creates a subscription.
        /// </summary>
        /// <param name="paymentIntentId">支付意图ID。 / Payment intent ID.</param>
        /// <param name="userId">用户ID。 / User ID.</param>
        /// <param name="priceId">价格ID。 / Price ID.</param>
        /// <param name="gateway">支付网关名称。 / Payment gateway name.</param>
        /// <returns>订阅ID。 / Subscription ID.</returns>
        Task<Guid> ConfirmPaymentAndCreateSubscriptionAsync(string paymentIntentId, Guid userId, Guid priceId, string gateway);

        /// <summary>
        /// 创建托管结账会话。
        /// Creates a hosted checkout session.
        /// </summary>
        /// <param name="userId">用户ID。 / User ID.</param>
        /// <param name="priceId">价格ID。 / Price ID.</param>
        /// <param name="gateway">支付网关名称。 / Payment gateway name.</param>
        /// <param name="successUrl">支付成功回调地址。 / Payment success callback URL.</param>
        /// <param name="cancelUrl">支付取消回调地址。 / Payment cancel callback URL.</param>
        /// <returns>托管结账会话结果。 / Hosted checkout session result.</returns>
        Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Guid userId, Guid priceId, string gateway, string successUrl, string cancelUrl);

        /// <summary>
        /// 根据结账会话确认支付并创建订阅。
        /// Confirms payment and creates a subscription from a checkout session.
        /// </summary>
        /// <param name="sessionId">结账会话ID。 / Checkout session ID.</param>
        /// <param name="userId">用户ID。 / User ID.</param>
        /// <param name="priceId">价格ID。 / Price ID.</param>
        /// <param name="gateway">支付网关名称。 / Payment gateway name.</param>
        /// <returns>订阅ID。 / Subscription ID.</returns>
        Task<Guid> ConfirmCheckoutSessionAsync(string sessionId, Guid userId, Guid priceId, string gateway);
    }

    /// <summary>
    /// 支付服务实现。
    /// Payment service implementation.
    /// </summary>
    public class PaymentService : IPaymentService
    {
        private readonly IEnumerable<IPaymentGateway> _gateways;
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// 初始化<see cref="PaymentService"/>的新实例。
        /// Initializes a new instance of the <see cref="PaymentService"/> class.
        /// </summary>
        /// <param name="gateways">所有可用的支付网关。 / All available payment gateways.</param>
        /// <param name="db">数据库上下文。 / Database context.</param>
        /// <exception cref="ArgumentNullException">当参数为null时抛出。 / Thrown when arguments are null.</exception>
        public PaymentService(IEnumerable<IPaymentGateway> gateways, ApplicationDbContext db)
        {
            _gateways = gateways ?? throw new ArgumentNullException(nameof(gateways));
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// 创建支付意图。
        /// Creates a payment intent.
        /// </summary>
        /// <param name="userId">用户ID。 / User ID.</param>
        /// <param name="priceId">价格ID。 / Price ID.</param>
        /// <param name="gateway">支付网关名称。 / Payment gateway name.</param>
        /// <returns>支付意图结果。 / Payment intent result.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when arguments are invalid.</exception>
        /// <exception cref="InvalidOperationException">当价格或网关无效时抛出。 / Thrown when the price or gateway is invalid.</exception>
        public async Task<PaymentIntentResult> CreatePaymentIntentAsync(Guid userId, Guid priceId, string gateway)
        {
            ValidateUserId(userId);
            ValidatePriceId(priceId);
            ValidateGateway(gateway);

            var price = await GetActivePriceAsync(priceId).ConfigureAwait(false);
            var paymentGateway = GetPaymentGateway(gateway);

            var metadata = new Dictionary<string, string>
            {
                { "userId", userId.ToString() },
                { "priceId", priceId.ToString() },
                { "productId", price.ProductId.ToString() },
                { "productCode", price.Product?.Code ?? "unknown" }
            };

            var result = await paymentGateway.CreatePaymentIntentAsync(price.Amount, price.Currency, metadata).ConfigureAwait(false);

            Log.Information("Payment intent created for user {UserId}, price {PriceId}, gateway {Gateway}", userId, priceId, gateway);
            return result;
        }

        /// <summary>
        /// 确认支付并创建订阅。
        /// Confirms payment and creates a subscription.
        /// </summary>
        /// <param name="paymentIntentId">支付意图ID。 / Payment intent ID.</param>
        /// <param name="userId">用户ID。 / User ID.</param>
        /// <param name="priceId">价格ID。 / Price ID.</param>
        /// <param name="gateway">支付网关名称。 / Payment gateway name.</param>
        /// <returns>订阅ID。 / Subscription ID.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when arguments are invalid.</exception>
        /// <exception cref="InvalidOperationException">当支付失败或数据无效时抛出。 / Thrown when payment fails or data is invalid.</exception>
        public async Task<Guid> ConfirmPaymentAndCreateSubscriptionAsync(string paymentIntentId, Guid userId, Guid priceId, string gateway)
        {
            if (string.IsNullOrWhiteSpace(paymentIntentId))
            {
                throw new ArgumentNullException(nameof(paymentIntentId));
            }

            ValidateUserId(userId);
            ValidatePriceId(priceId);
            ValidateGateway(gateway);

            var existingSubscriptionId = await FindExistingSubscriptionIdAsync(paymentIntentId, gateway).ConfigureAwait(false);
            if (existingSubscriptionId != Guid.Empty)
            {
                Log.Information("Existing subscription {SubscriptionId} reused for payment intent {PaymentIntentId}", existingSubscriptionId, paymentIntentId);
                return existingSubscriptionId;
            }

            var price = await GetActivePriceAsync(priceId).ConfigureAwait(false);
            var paymentGateway = GetPaymentGateway(gateway);
            var paymentResult = await paymentGateway.ConfirmPaymentAsync(paymentIntentId).ConfigureAwait(false);

            if (!paymentResult.Succeeded)
            {
                Log.Warning("Payment {PaymentIntentId} failed: {Error}", paymentIntentId, paymentResult.ErrorMessage);
                throw new InvalidOperationException($"Payment failed: {paymentResult.ErrorMessage}");
            }

            await using var transaction = await _db.Database.BeginTransactionAsync().ConfigureAwait(false);
            try
            {
                var subscription = new SubscriptionEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ProductId = price.ProductId,
                    PriceId = priceId,
                    Status = 1,
                    StartDate = DateTimeOffset.UtcNow,
                    EndDate = CalculateEndDate(price.BillingPeriod),
                    AutoRenew = true,
                    OriginalAmount = price.Amount,
                    ActualAmount = price.Amount,
                    CreatedTime = DateTimeOffset.UtcNow,
                    IsDeleted = false
                };

                _db.Subscriptions.Add(subscription);

                var transactionEntity = new TransactionEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SubscriptionId = subscription.Id,
                    Amount = paymentResult.Amount,
                    Currency = paymentResult.Currency,
                    Gateway = gateway,
                    ExternalTransactionId = paymentResult.ExternalTransactionId,
                    Status = 1,
                    Metadata = JsonSerializer.Serialize(new
                    {
                        paymentIntentId,
                        priceId,
                        userId,
                        externalTransactionId = paymentResult.ExternalTransactionId
                    }),
                    CreatedTime = DateTimeOffset.UtcNow,
                    Remarks = $"PaymentIntent:{paymentIntentId}; Gateway:{gateway}"
                };

                _db.Transactions.Add(transactionEntity);

                await _db.SaveChangesAsync().ConfigureAwait(false);
                await transaction.CommitAsync().ConfigureAwait(false);

                Log.Information("Subscription {SubscriptionId} created for user {UserId}, payment {PaymentIntentId}", subscription.Id, userId, paymentIntentId);
                return subscription.Id;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
                Log.Error(ex, "Error creating subscription for payment {PaymentIntentId}", paymentIntentId);
                throw;
            }
        }

        /// <summary>
        /// 创建托管结账会话。
        /// Creates a hosted checkout session.
        /// </summary>
        /// <param name="userId">用户ID。 / User ID.</param>
        /// <param name="priceId">价格ID。 / Price ID.</param>
        /// <param name="gateway">支付网关名称。 / Payment gateway name.</param>
        /// <param name="successUrl">支付成功回调地址。 / Payment success callback URL.</param>
        /// <param name="cancelUrl">支付取消回调地址。 / Payment cancel callback URL.</param>
        /// <returns>托管结账会话结果。 / Hosted checkout session result.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when arguments are invalid.</exception>
        /// <exception cref="InvalidOperationException">当价格或网关无效时抛出。 / Thrown when the price or gateway is invalid.</exception>
        public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(Guid userId, Guid priceId, string gateway, string successUrl, string cancelUrl)
        {
            ValidateUserId(userId);
            ValidatePriceId(priceId);
            ValidateGateway(gateway);

            if (string.IsNullOrWhiteSpace(successUrl))
            {
                throw new ArgumentNullException(nameof(successUrl));
            }

            if (string.IsNullOrWhiteSpace(cancelUrl))
            {
                throw new ArgumentNullException(nameof(cancelUrl));
            }

            var price = await GetActivePriceAsync(priceId).ConfigureAwait(false);
            var paymentGateway = GetPaymentGateway(gateway);

            return await paymentGateway.CreateCheckoutSessionAsync(
                priceId,
                price.Amount,
                price.Currency,
                price.Product?.Name ?? "Product",
                successUrl,
                cancelUrl).ConfigureAwait(false);
        }

        /// <summary>
        /// 根据结账会话确认支付并创建订阅。
        /// Confirms payment and creates a subscription from a checkout session.
        /// </summary>
        /// <param name="sessionId">结账会话ID。 / Checkout session ID.</param>
        /// <param name="userId">用户ID。 / User ID.</param>
        /// <param name="priceId">价格ID。 / Price ID.</param>
        /// <param name="gateway">支付网关名称。 / Payment gateway name.</param>
        /// <returns>订阅ID。 / Subscription ID.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when arguments are invalid.</exception>
        /// <exception cref="InvalidOperationException">当会话或网关无效时抛出。 / Thrown when the session or gateway is invalid.</exception>
        public async Task<Guid> ConfirmCheckoutSessionAsync(string sessionId, Guid userId, Guid priceId, string gateway)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
            {
                throw new ArgumentNullException(nameof(sessionId));
            }

            ValidateUserId(userId);
            ValidatePriceId(priceId);
            ValidateGateway(gateway);

            var paymentGateway = GetPaymentGateway(gateway);
            var paymentIntentId = await paymentGateway.GetCheckoutSessionPaymentIntentIdAsync(sessionId).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(paymentIntentId))
            {
                throw new InvalidOperationException("Payment intent was not found for the checkout session");
            }

            return await ConfirmPaymentAndCreateSubscriptionAsync(paymentIntentId, userId, priceId, gateway).ConfigureAwait(false);
        }

        private static void ValidateUserId(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("Invalid user ID", nameof(userId));
            }
        }

        private static void ValidatePriceId(Guid priceId)
        {
            if (priceId == Guid.Empty)
            {
                throw new ArgumentException("Invalid price ID", nameof(priceId));
            }
        }

        private static void ValidateGateway(string gateway)
        {
            if (string.IsNullOrWhiteSpace(gateway))
            {
                throw new ArgumentNullException(nameof(gateway));
            }
        }

        private async Task<Guid> FindExistingSubscriptionIdAsync(string paymentIntentId, string gateway)
        {
            return await _db.Transactions
                .AsNoTracking()
                .Where(t => t.SubscriptionId.HasValue
                            && t.Gateway == gateway
                            && t.Remarks != null
                            && t.Remarks.Contains(paymentIntentId))
                .Select(t => t.SubscriptionId ?? Guid.Empty)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
        }

        private IPaymentGateway GetPaymentGateway(string gateway)
        {
            var paymentGateway = _gateways.FirstOrDefault(g => g.GatewayName.Equals(gateway, StringComparison.OrdinalIgnoreCase));
            if (paymentGateway == null)
            {
                Log.Warning("Payment gateway {Gateway} not found", gateway);
                throw new InvalidOperationException($"Payment gateway '{gateway}' not supported");
            }

            return paymentGateway;
        }

        private async Task<PriceEntity> GetActivePriceAsync(Guid priceId)
        {
            var price = await _db.Prices
                .Include(p => p.Product)
                .FirstOrDefaultAsync(p => p.Id == priceId)
                .ConfigureAwait(false);

            if (price == null)
            {
                Log.Warning("Price {PriceId} not found", priceId);
                throw new InvalidOperationException("Price not found");
            }

            if (!price.IsActive || price.Product == null || !price.Product.IsActive)
            {
                Log.Warning("Price {PriceId} is not active", priceId);
                throw new InvalidOperationException("Price is not active");
            }

            return price;
        }

        private static DateTimeOffset CalculateEndDate(string billingPeriod)
        {
            return billingPeriod.ToLowerInvariant() switch
            {
                "week" => DateTimeOffset.UtcNow.AddDays(7),
                "month" => DateTimeOffset.UtcNow.AddMonths(1),
                "year" => DateTimeOffset.UtcNow.AddYears(1),
                _ => DateTimeOffset.UtcNow.AddMonths(1)
            };
        }
    }
}
