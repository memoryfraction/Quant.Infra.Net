using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Confirms payment and creates subscription.
        /// </summary>
        /// <param name="paymentIntentId">支付意图ID。 / Payment intent ID.</param>
        /// <param name="userId">用户ID。 / User ID.</param>
        /// <param name="orderId">订单ID。 / Order ID.</param>
        /// <param name="gateway">支付网关名称。 / Payment gateway name.</param>
        /// <returns>订阅ID。 / Subscription ID.</returns>
        Task<Guid> ConfirmPaymentAndCreateSubscriptionAsync(
            string paymentIntentId,
            Guid userId,
            Guid orderId,
            string gateway);
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
        /// <exception cref="ArgumentNullException">当参数为null时抛出。 / Thrown when parameters are null.</exception>
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
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when parameters are invalid.</exception>
        /// <exception cref="InvalidOperationException">当价格或网关未找到时抛出。 / Thrown when price or gateway not found.</exception>
        public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
            Guid userId,
            Guid priceId,
            string gateway)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("Invalid user ID", nameof(userId));
            if (priceId == Guid.Empty)
                throw new ArgumentException("Invalid price ID", nameof(priceId));
            if (string.IsNullOrWhiteSpace(gateway))
                throw new ArgumentNullException(nameof(gateway));

            // 获取价格信息
            var price = await _db.Prices
                .Include(p => p.Product)
                .FirstOrDefaultAsync(p => p.Id == priceId);

            if (price == null)
            {
                Log.Warning("Price {PriceId} not found", priceId);
                throw new InvalidOperationException("Price not found");
            }

            if (!price.IsActive)
            {
                Log.Warning("Price {PriceId} is not active", priceId);
                throw new InvalidOperationException("Price is not active");
            }

            if (price.Product == null || !price.Product.IsActive)
            {
                Log.Warning("Product for price {PriceId} is not active", priceId);
                throw new InvalidOperationException("Product is not active");
            }

            // 获取支付网关
            var paymentGateway = _gateways.FirstOrDefault(g =>
                g.GatewayName.Equals(gateway, StringComparison.OrdinalIgnoreCase));

            if (paymentGateway == null)
            {
                Log.Warning("Payment gateway {Gateway} not found", gateway);
                throw new InvalidOperationException($"Payment gateway '{gateway}' not supported");
            }

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

            // 创建元数据
            var metadata = new Dictionary<string, string>
            {
                { "orderId", order.Id.ToString() },
                { "userId", userId.ToString() },
                { "priceId", priceId.ToString() },
                { "productId", price.ProductId.ToString() },
                { "productCode", price.Product?.Code ?? "unknown" }
            };

            // 调用网关创建支付意图
            var result = await paymentGateway.CreatePaymentIntentAsync(
                price.Amount,
                price.Currency,
                metadata);

            result.OrderId = order.Id;

            Log.Information("Payment intent created for order {OrderId}, user {UserId}, price {PriceId}, gateway {Gateway}",
                order.Id, userId, priceId, gateway);

            return result;
        }

        /// <summary>
        /// 确认支付并创建订阅。
        /// Confirms payment and creates subscription.
        /// </summary>
        /// <param name="paymentIntentId">支付意图ID。 / Payment intent ID.</param>
        /// <param name="userId">用户ID。 / User ID.</param>
        /// <param name="orderId">订单ID。 / Order ID.</param>
        /// <param name="gateway">支付网关名称。 / Payment gateway name.</param>
        /// <returns>订阅ID。 / Subscription ID.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when parameters are invalid.</exception>
        /// <exception cref="InvalidOperationException">当支付失败或数据错误时抛出。 / Thrown when payment fails or data is invalid.</exception>
        public async Task<Guid> ConfirmPaymentAndCreateSubscriptionAsync(
            string paymentIntentId,
            Guid userId,
            Guid orderId,
            string gateway)
        {
            if (string.IsNullOrWhiteSpace(paymentIntentId))
                throw new ArgumentNullException(nameof(paymentIntentId));
            if (userId == Guid.Empty)
                throw new ArgumentException("Invalid user ID", nameof(userId));
            if (orderId == Guid.Empty)
                throw new ArgumentException("Invalid order ID", nameof(orderId));
            if (string.IsNullOrWhiteSpace(gateway))
                throw new ArgumentNullException(nameof(gateway));

            var order = await _db.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);

            if (order == null)
                throw new InvalidOperationException("Order not found");

            if (order.UserId != userId)
                throw new InvalidOperationException("Order does not belong to the current user");

            if (order.Status != 0)
                throw new InvalidOperationException("Order is not pending");

            if (order.ExpiredTime.HasValue && order.ExpiredTime.Value <= DateTimeOffset.UtcNow)
                throw new InvalidOperationException("Order has expired");

            // 获取价格信息
            var price = await _db.Prices
                .Include(p => p.Product)
                .FirstOrDefaultAsync(p => p.Id == order.PriceId);

            if (price == null)
                throw new InvalidOperationException("Price not found");

            // 获取支付网关并确认支付
            var paymentGateway = _gateways.FirstOrDefault(g =>
                g.GatewayName.Equals(gateway, StringComparison.OrdinalIgnoreCase));

            if (paymentGateway == null)
                throw new InvalidOperationException($"Payment gateway '{gateway}' not supported");

            var paymentResult = await paymentGateway.ConfirmPaymentAsync(paymentIntentId);

            if (!paymentResult.Succeeded)
            {
                Log.Warning("Payment {PaymentIntentId} failed: {Error}", paymentIntentId, paymentResult.ErrorMessage);
                throw new InvalidOperationException($"Payment failed: {paymentResult.ErrorMessage}");
            }

            // 使用事务创建订阅和交易记录
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // 1. 创建订阅
                var subscription = new SubscriptionEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ProductId = price.ProductId,
                    PriceId = order.PriceId,
                    OrderId = order.Id,
                    Status = 1, // Active
                    StartDate = DateTimeOffset.UtcNow,
                    EndDate = CalculateEndDate(price.BillingPeriod),
                    AutoRenew = true,
                    CreatedTime = DateTimeOffset.UtcNow,
                    IsDeleted = false
                };

                _db.Subscriptions.Add(subscription);

                // 2. 创建交易记录
                var transactionEntity = new TransactionEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OrderId = order.Id,
                    SubscriptionId = subscription.Id,
                    Amount = paymentResult.Amount,
                    Currency = paymentResult.Currency,
                    Gateway = gateway,
                    ExternalTransactionId = paymentResult.ExternalTransactionId,
                    Status = 1, // Success
                    CreatedTime = DateTimeOffset.UtcNow,
                    Remarks = $"Payment via {gateway}"
                };

                _db.Transactions.Add(transactionEntity);

                order.SubscriptionId = subscription.Id;
                order.ActualAmount = paymentResult.Amount;
                order.Status = 1;
                order.PaidTime = DateTimeOffset.UtcNow;

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                Log.Information("Subscription {SubscriptionId} created for order {OrderId}, user {UserId}, payment {PaymentIntentId}",
                    subscription.Id, order.Id, userId, paymentIntentId);

                return subscription.Id;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Log.Error(ex, "Error creating subscription for payment {PaymentIntentId}", paymentIntentId);
                throw;
            }
        }

        /// <summary>
        /// 根据计费周期计算订阅结束日期。
        /// Calculates subscription end date based on billing period.
        /// </summary>
        /// <param name="billingPeriod">计费周期（week/month/year）。 / Billing period (week/month/year).</param>
        /// <returns>结束日期。 / End date.</returns>
        private static DateTimeOffset CalculateEndDate(string billingPeriod)
        {
            return billingPeriod.ToLower() switch
            {
                "week" => DateTimeOffset.UtcNow.AddDays(7),
                "month" => DateTimeOffset.UtcNow.AddMonths(1),
                "year" => DateTimeOffset.UtcNow.AddYears(1),
                _ => DateTimeOffset.UtcNow.AddMonths(1) // 默认1个月
            };
        }
    }
}
