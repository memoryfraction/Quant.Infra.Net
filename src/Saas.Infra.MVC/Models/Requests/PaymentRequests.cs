using System;
using System.ComponentModel.DataAnnotations;

namespace Saas.Infra.MVC.Models.Requests
{
    /// <summary>
    /// 创建支付意图请求模型。
    /// Create payment intent request model.
    /// </summary>
    public class CreatePaymentIntentRequest
    {
        /// <summary>
        /// 价格ID。
        /// Price ID.
        /// </summary>
        [Required(ErrorMessage = "Price ID is required")]
        public Guid PriceId { get; set; }

        /// <summary>
        /// 支付网关（Stripe / OxaPay / USDT）。
        /// Payment gateway (Stripe / OxaPay / USDT).
        /// </summary>
        [Required(ErrorMessage = "Gateway is required")]
        [StringLength(50, ErrorMessage = "Gateway cannot exceed 50 characters")]
        public string Gateway { get; set; } = "Stripe";
    }

    /// <summary>
    /// 确认支付请求模型。
    /// Confirm payment request model.
    /// </summary>
    public class ConfirmPaymentRequest
    {
        /// <summary>
        /// 支付意图ID（Stripe PaymentIntent ID）。
        /// Payment intent ID (Stripe PaymentIntent ID).
        /// </summary>
        [Required(ErrorMessage = "Payment intent ID is required")]
        public string PaymentIntentId { get; set; } = string.Empty;

        /// <summary>
        /// 价格ID。
        /// Price ID.
        /// </summary>
        [Required(ErrorMessage = "Price ID is required")]
        public Guid PriceId { get; set; }

        /// <summary>
        /// 支付网关。
        /// Payment gateway.
        /// </summary>
        [Required(ErrorMessage = "Gateway is required")]
        public string Gateway { get; set; } = "Stripe";
    }

    /// <summary>
    /// 创建托管结账会话请求模型。
    /// Create hosted checkout session request model.
    /// </summary>
    public class CreateCheckoutSessionRequest
    {
        /// <summary>
        /// 价格ID。
        /// Price ID.
        /// </summary>
        [Required(ErrorMessage = "Price ID is required")]
        public Guid PriceId { get; set; }

        /// <summary>
        /// 支付网关。
        /// Payment gateway.
        /// </summary>
        [Required(ErrorMessage = "Gateway is required")]
        [StringLength(50, ErrorMessage = "Gateway cannot exceed 50 characters")]
        public string Gateway { get; set; } = "Stripe";

        /// <summary>
        /// 支付成功回调地址。
        /// Payment success callback URL.
        /// </summary>
        [Required(ErrorMessage = "Success URL is required")]
        public string SuccessUrl { get; set; } = string.Empty;

        /// <summary>
        /// 支付取消回调地址。
        /// Payment cancel callback URL.
        /// </summary>
        [Required(ErrorMessage = "Cancel URL is required")]
        public string CancelUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// 确认托管结账会话请求模型。
    /// Confirm hosted checkout session request model.
    /// </summary>
    public class ConfirmCheckoutSessionRequest
    {
        /// <summary>
        /// 结账会话ID。
        /// Checkout session ID.
        /// </summary>
        [Required(ErrorMessage = "Session ID is required")]
        public string SessionId { get; set; } = string.Empty;

        /// <summary>
        /// 价格ID。
        /// Price ID.
        /// </summary>
        [Required(ErrorMessage = "Price ID is required")]
        public Guid PriceId { get; set; }

        /// <summary>
        /// 支付网关。
        /// Payment gateway.
        /// </summary>
        [Required(ErrorMessage = "Gateway is required")]
        [StringLength(50, ErrorMessage = "Gateway cannot exceed 50 characters")]
        public string Gateway { get; set; } = "Stripe";
    }
}
