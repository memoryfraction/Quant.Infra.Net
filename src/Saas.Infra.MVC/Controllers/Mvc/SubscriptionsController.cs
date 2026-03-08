using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Saas.Infra.MVC.Controllers.Mvc
{
    /// <summary>
    /// 订阅管理MVC控制器（用户端）。
    /// Subscription management MVC controller (User side).
    /// </summary>
    [NonController]
    [Authorize]
    public class SubscriptionsController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        /// <summary>
        /// 初始化<see cref="SubscriptionsController"/>的新实例。
        /// Initializes a new instance of the <see cref="SubscriptionsController"/> class.
        /// </summary>
        /// <param name="httpClientFactory">HTTP客户端工厂。 / HTTP client factory.</param>
        /// <param name="configuration">配置。 / Configuration.</param>
        /// <exception cref="ArgumentNullException">当参数为null时抛出。 / Thrown when parameters are null.</exception>
        public SubscriptionsController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// 我的订阅列表页面。
        /// My subscriptions list page.
        /// </summary>
        /// <returns>订阅列表视图。 / Subscriptions list view.</returns>
        [HttpGet]
        [Route("subscriptions")]
        public IActionResult Index()
        {
            try
            {
                var username = User.Identity?.Name;
                Log.Information("Subscriptions page accessed by user: {Username}", username);
                return View();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading subscriptions page");
                return View("Error");
            }
        }

        /// <summary>
        /// 订阅详情页面。
        /// Subscription details page.
        /// </summary>
        /// <param name="id">订阅ID。 / Subscription ID.</param>
        /// <returns>订阅详情视图。 / Subscription details view.</returns>
        [HttpGet]
        [Route("subscriptions/{id}")]
        public IActionResult Details(Guid id)
        {
            if (id == Guid.Empty)
            {
                Log.Warning("Invalid subscription ID");
                return RedirectToAction("Index");
            }

            try
            {
                ViewBag.SubscriptionId = id;
                Log.Information("Subscription details page accessed for {SubscriptionId}", id);
                return View();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading subscription details for {Id}", id);
                return View("Error");
            }
        }
    }
}
