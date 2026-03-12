using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Saas.Infra.Data;
using Serilog;
using Stripe;
using Stripe.Checkout;

namespace Saas.Infra.MVC.Services.Payment
{
    /// <summary>
    /// Stripe Webhook事件处理服务实现。
    /// Stripe webhook event processing service implementation.
    /// </summary>
    public class StripeWebhookService : IStripeWebhookService
    {
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// 初始化<see cref="StripeWebhookService"/>的新实例。
        /// Initializes a new instance of the <see cref="StripeWebhookService"/> class.
        /// </summary>
        /// <param name="db">数据库上下文。 / Database context.</param>
        /// <exception cref="ArgumentNullException">当参数为null时抛出。 / Thrown when parameters are null.</exception>
        public StripeWebhookService(ApplicationDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>
        /// 根据Stripe事件类型路由到对应的处理方法。
        /// Routes to the corresponding handler based on the Stripe event type.
        /// </summary>
        /// <param name="stripeEvent">Stripe事件对象。 / Stripe event object.</param>
        /// <exception cref="ArgumentNullException">当stripeEvent为null时抛出。 / Thrown when stripeEvent is null.</exception>
        public async Task ProcessEventAsync(Event stripeEvent)
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

        #region Webhook Handlers

        /// <summary>
        /// 处理checkout.session.completed事件：标记订单为已支付，创建订阅和交易记录。
        /// Handles checkout.session.completed: marks order as paid, creates subscription and transaction.
        /// </summary>
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

        /// <summary>
        /// 处理checkout.session.expired事件：标记订单为已取消。
        /// Handles checkout.session.expired: marks order as cancelled.
        /// </summary>
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

        /// <summary>
        /// 处理payment_intent.payment_failed事件：标记订单为已取消并记录失败交易。
        /// Handles payment_intent.payment_failed: marks order as cancelled and records failed transaction.
        /// </summary>
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

        #endregion

        #region Utilities

        /// <summary>
        /// 从元数据中解析GUID。
        /// Parses a GUID from metadata dictionary.
        /// </summary>
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

        /// <summary>
        /// 根据计费周期计算订阅结束日期。
        /// Calculates subscription end date based on billing period.
        /// </summary>
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

        #endregion
    }
}
