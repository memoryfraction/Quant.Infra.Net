using System.Collections.Generic;
using System.Threading.Tasks;

namespace Saas.Infra.MVC.Services.Payment
{
    /// <summary>
    /// 支付网关接口，定义统一的支付操作。
    /// Payment gateway interface defining unified payment operations.
    /// </summary>
    public interface IPaymentGateway
    {
        /// <summary>
        /// 网关名称（Stripe / OxaPay / USDT）。
        /// Gateway name (Stripe / OxaPay / USDT).
        /// </summary>
        string GatewayName { get; }

        /// <summary>
        /// 创建支付意图。
        /// Creates a payment intent.
        /// </summary>
        /// <param name="amount">金额（以分为单位）。 / Amount (in cents).</param>
        /// <param name="currency">货币代码。 / Currency code.</param>
        /// <param name="metadata">元数据。 / Metadata.</param>
        /// <returns>支付意图结果。 / Payment intent result.</returns>
        Task<PaymentIntentResult> CreatePaymentIntentAsync(
            long amount,
            string currency,
            Dictionary<string, string> metadata);

        /// <summary>
        /// 确认支付。
        /// Confirms a payment.
        /// </summary>
        /// <param name="paymentIntentId">支付意图ID。 / Payment intent ID.</param>
        /// <returns>支付结果。 / Payment result.</returns>
        Task<PaymentResult> ConfirmPaymentAsync(string paymentIntentId);

        /// <summary>
        /// 验证Webhook签名。
        /// Verifies webhook signature.
        /// </summary>
        /// <param name="payload">请求负载。 / Request payload.</param>
        /// <param name="signature">签名。 / Signature.</param>
        /// <returns>是否验证成功。 / Whether verification succeeded.</returns>
        Task<bool> VerifyWebhookSignatureAsync(string payload, string signature);
    }

    /// <summary>
    /// 支付意图结果。
    /// Payment intent result.
    /// </summary>
    public class PaymentIntentResult
    {
        /// <summary>
        /// 客户端密钥（用于前端）。
        /// Client secret (for frontend).
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// 支付意图ID。
        /// Payment intent ID.
        /// </summary>
        public string PaymentIntentId { get; set; } = string.Empty;

        /// <summary>
        /// 金额（以分为单位）。
        /// Amount (in cents).
        /// </summary>
        public long Amount { get; set; }

        /// <summary>
        /// 货币代码。
        /// Currency code.
        /// </summary>
        public string Currency { get; set; } = string.Empty;

        /// <summary>
        /// 状态。
        /// Status.
        /// </summary>
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// 支付结果。
    /// Payment result.
    /// </summary>
    public class PaymentResult
    {
        /// <summary>
        /// 是否成功。
        /// Whether succeeded.
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// 支付意图ID。
        /// Payment intent ID.
        /// </summary>
        public string PaymentIntentId { get; set; } = string.Empty;

        /// <summary>
        /// 外部交易ID（Charge ID等）。
        /// External transaction ID (Charge ID, etc.).
        /// </summary>
        public string? ExternalTransactionId { get; set; }

        /// <summary>
        /// 金额（以分为单位）。
        /// Amount (in cents).
        /// </summary>
        public long Amount { get; set; }

        /// <summary>
        /// 货币代码。
        /// Currency code.
        /// </summary>
        public string Currency { get; set; } = string.Empty;

        /// <summary>
        /// 错误消息（如果失败）。
        /// Error message (if failed).
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
