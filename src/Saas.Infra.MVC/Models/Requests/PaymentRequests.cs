using System;
using System.ComponentModel.DataAnnotations;

namespace Saas.Infra.MVC.Models.Requests
{
    /// <summary>
    /// 创建订单请求模型。
    /// Create order request model.
    /// </summary>
    public class CreateOrderRequest
    {
        /// <summary>
        /// 价格ID。
        /// Price ID.
        /// </summary>
        [Required(ErrorMessage = "Price ID is required")]
        public Guid PriceId { get; set; }

    }

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
        /// 订单ID。
        /// Order ID.
        /// </summary>
        [Required(ErrorMessage = "Order ID is required")]
        public Guid OrderId { get; set; }

        /// <summary>
        /// 价格ID。
        /// Price ID.
        /// </summary>
        /// 支付网关。
        /// Payment gateway.
        /// </summary>
        [Required(ErrorMessage = "Gateway is required")]
        public string Gateway { get; set; } = "Stripe";
    }
}
