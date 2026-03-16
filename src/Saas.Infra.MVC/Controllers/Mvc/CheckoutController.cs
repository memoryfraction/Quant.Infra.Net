using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Saas.Infra.Services.Product;
using Serilog;

namespace Saas.Infra.MVC.Controllers.Mvc
{
    /// <summary>
    /// 支付结账MVC控制器。
    /// Checkout MVC controller.
    /// </summary>
    [NonController]
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly IProductApplicationService _productApplicationService;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// 初始化<see cref="CheckoutController"/>的新实例。
        /// Initializes a new instance of the <see cref="CheckoutController"/> class.
        /// </summary>
        /// <param name="productApplicationService">产品应用服务。 / Product application service.</param>
        /// <param name="configuration">配置。 / Configuration.</param>
        /// <exception cref="ArgumentNullException">当参数为null时抛出。 / Thrown when parameters are null.</exception>
        public CheckoutController(IProductApplicationService productApplicationService, IConfiguration configuration)
        {
            _productApplicationService = productApplicationService ?? throw new ArgumentNullException(nameof(productApplicationService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// 支付页面（显示Stripe支付表单）。
        /// Checkout page (displays Stripe payment form).
        /// </summary>
        /// <param name="priceId">价格ID。 / Price ID.</param>
        /// <returns>支付页面视图。 / Checkout page view.</returns>
        [HttpGet]
        [Route("checkout")]
        public async Task<IActionResult> Index([FromQuery] Guid priceId)
        {
            if (priceId == Guid.Empty)
            {
                Log.Warning("Invalid price ID for checkout");
                return RedirectToAction("Index", "Products");
            }

            try
            {
                var price = await _productApplicationService.GetActivePriceWithProductAsync(priceId);
                if (price == null || price.Product == null || !price.Product.IsActive)
                {
                    Log.Warning("Price {PriceId} not found or not active", priceId);
                    return RedirectToAction("Index", "Products");
                }

                ViewBag.StripePublishableKey = _configuration["Stripe:PublishableKey"];
                Log.Information("Checkout page accessed for price {PriceId}", priceId);
                
                return View(price);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading checkout page for price {PriceId}", priceId);
                return View("Error");
            }
        }

        /// <summary>
        /// 支付成功页面。
        /// Payment success page.
        /// </summary>
        /// <param name="subscriptionId">订阅ID（可选）。 / Subscription ID (optional).</param>
        /// <returns>支付成功视图。 / Payment success view.</returns>
        [HttpGet]
        [Route("checkout/success")]
        public IActionResult Success([FromQuery] Guid? subscriptionId)
        {
            Log.Information("Payment success page accessed, subscription: {SubscriptionId}", subscriptionId);
            ViewBag.SubscriptionId = subscriptionId;
            ViewBag.IsSubscriptionCreated = subscriptionId.HasValue && subscriptionId.Value != Guid.Empty;
            return View();
        }
    }
}
