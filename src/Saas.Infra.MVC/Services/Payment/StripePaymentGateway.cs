using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Stripe;
using Serilog;

namespace Saas.Infra.MVC.Services.Payment
{
    /// <summary>
    /// Stripe支付网关实现。
    /// Stripe payment gateway implementation.
    /// </summary>
    public class StripePaymentGateway : IPaymentGateway
    {
        private readonly string _secretKey;
        private readonly string _webhookSecret;

        /// <summary>
        /// 网关名称。
        /// Gateway name.
        /// </summary>
        public string GatewayName => "Stripe";

        /// <summary>
        /// 初始化<see cref="StripePaymentGateway"/>的新实例。
        /// Initializes a new instance of the <see cref="StripePaymentGateway"/> class.
        /// </summary>
        /// <param name="secretKey">Stripe Secret Key。 / Stripe Secret Key.</param>
        /// <param name="webhookSecret">Webhook签名密钥。 / Webhook signature secret.</param>
        /// <exception cref="ArgumentNullException">当参数为null或空白时抛出。 / Thrown when parameters are null or whitespace.</exception>
        public StripePaymentGateway(string secretKey, string webhookSecret)
        {
            if (string.IsNullOrWhiteSpace(secretKey))
                throw new ArgumentNullException(nameof(secretKey));
            if (string.IsNullOrWhiteSpace(webhookSecret))
                throw new ArgumentNullException(nameof(webhookSecret));

            _secretKey = secretKey;
            _webhookSecret = webhookSecret;
            
            // 配置 Stripe API Key
            StripeConfiguration.ApiKey = _secretKey;
        }

        /// <summary>
        /// 创建Stripe支付意图。
        /// Creates a Stripe payment intent.
        /// </summary>
        /// <param name="amount">金额（以分为单位）。 / Amount (in cents).</param>
        /// <param name="currency">货币代码（小写，如"usd"）。 / Currency code (lowercase, e.g., "usd").</param>
        /// <param name="metadata">元数据（userId, priceId等）。 / Metadata (userId, priceId, etc.).</param>
        /// <returns>支付意图结果。 / Payment intent result.</returns>
        /// <exception cref="ArgumentException">当参数无效时抛出。 / Thrown when parameters are invalid.</exception>
        public async Task<PaymentIntentResult> CreatePaymentIntentAsync(
            long amount,
            string currency,
            Dictionary<string, string> metadata)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive", nameof(amount));
            if (string.IsNullOrWhiteSpace(currency))
                throw new ArgumentNullException(nameof(currency));

            try
            {
                var options = new PaymentIntentCreateOptions
                {
                    Amount = amount,
                    Currency = currency.ToLower(),
                    Metadata = metadata,
                    AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                    {
                        Enabled = true,
                        AllowRedirects = "never" // MVP版本仅支持卡支付
                    }
                };

                var service = new PaymentIntentService();
                var intent = await service.CreateAsync(options);

                Log.Information("Stripe PaymentIntent created: {PaymentIntentId}, Amount: {Amount} {Currency}",
                    intent.Id, amount, currency);

                return new PaymentIntentResult
                {
                    ClientSecret = intent.ClientSecret ?? string.Empty,
                    PaymentIntentId = intent.Id,
                    Amount = intent.Amount,
                    Currency = intent.Currency,
                    Status = intent.Status
                };
            }
            catch (StripeException ex)
            {
                Log.Error(ex, "Stripe error creating PaymentIntent: {ErrorMessage}", ex.Message);
                throw new InvalidOperationException($"Stripe error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 确认Stripe支付（通常由Webhook触发，这里用于手动确认）。
        /// Confirms a Stripe payment (usually triggered by webhook, used for manual confirmation here).
        /// </summary>
        /// <param name="paymentIntentId">支付意图ID。 / Payment intent ID.</param>
        /// <returns>支付结果。 / Payment result.</returns>
        /// <exception cref="ArgumentNullException">当paymentIntentId为null或空白时抛出。 / Thrown when paymentIntentId is null or whitespace.</exception>
        public async Task<PaymentResult> ConfirmPaymentAsync(string paymentIntentId)
        {
            if (string.IsNullOrWhiteSpace(paymentIntentId))
                throw new ArgumentNullException(nameof(paymentIntentId));

            try
            {
                var service = new PaymentIntentService();
                var intent = await service.GetAsync(paymentIntentId);

                var succeeded = intent.Status == "succeeded";

                Log.Information("Stripe PaymentIntent {PaymentIntentId} status: {Status}",
                    paymentIntentId, intent.Status);

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
                Log.Error(ex, "Stripe error confirming PaymentIntent {PaymentIntentId}: {ErrorMessage}",
                    paymentIntentId, ex.Message);

                return new PaymentResult
                {
                    Succeeded = false,
                    PaymentIntentId = paymentIntentId,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 验证Stripe Webhook签名。
        /// Verifies Stripe webhook signature.
        /// </summary>
        /// <param name="payload">请求体（JSON字符串）。 / Request body (JSON string).</param>
        /// <param name="signature">Stripe-Signature头部的值。 / Value of Stripe-Signature header.</param>
        /// <returns>是否验证成功。 / Whether verification succeeded.</returns>
        /// <exception cref="ArgumentNullException">当参数为null或空白时抛出。 / Thrown when parameters are null or whitespace.</exception>
        public Task<bool> VerifyWebhookSignatureAsync(string payload, string signature)
        {
            if (string.IsNullOrWhiteSpace(payload))
                throw new ArgumentNullException(nameof(payload));
            if (string.IsNullOrWhiteSpace(signature))
                throw new ArgumentNullException(nameof(signature));

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    payload,
                    signature,
                    _webhookSecret,
                    throwOnApiVersionMismatch: false);

                Log.Information("Stripe webhook signature verified: {EventType}", stripeEvent.Type);
                return Task.FromResult(true);
            }
            catch (StripeException ex)
            {
                Log.Warning(ex, "Stripe webhook signature verification failed: {ErrorMessage}", ex.Message);
                return Task.FromResult(false);
            }
        }
    }
}
