using Microsoft.AspNetCore.Mvc;
using Saas.Infra.Services.Product;
using System.Text.Json;

namespace Saas.Infra.MVC.Controllers.Mvc
{
	/// <summary>
	/// MVC仪表板控制器 - 显示用户仪表板和账户信息。
	/// MVC Dashboard Controller - Displays user dashboard and account information.
	/// 注意：这是MVC控制器，返回视图。需要有效的访问令牌。
	/// Note: This is an MVC controller returning views. Requires valid access token.
	/// </summary>
	[NonController]
	public class DashboardController : Controller
	{
		/// <summary>
		/// HTTP客户端工厂，用于调用后端API。
		/// HTTP client factory for calling backend API.
		/// </summary>
		private readonly IHttpClientFactory _httpClientFactory;

		/// <summary>
		/// 日志记录器。
		/// Logger instance.
		/// </summary>
		private readonly ILogger<DashboardController> _logger;

		/// <summary>
		/// 应用程序配置。
		/// Application configuration.
		/// </summary>
        private readonly IConfiguration _configuration;
        private readonly IProductConfigService _productConfigService;

        /// <summary>
        /// 后端API基础URL。
        /// Backend API base URL.
        /// </summary>
        private readonly string _apiBaseUrl;

		/// <summary>
		/// 构造函数 - 依赖注入。
		/// Constructor - Dependency injection.
		/// </summary>
		/// <param name="httpClientFactory">HTTP客户端工厂 / HTTP client factory</param>
		/// <param name="logger">日志记录器 / Logger instance</param>
		/// <param name="configuration">应用程序配置 / Application configuration</param>
		/// <exception cref="ArgumentNullException">当参数为null时抛出 / Thrown when parameter is null</exception>
        public DashboardController(
            IHttpClientFactory httpClientFactory,
            ILogger<DashboardController> logger,
            IConfiguration configuration,
            IProductConfigService productConfigService)
		{
			// Parameter validation
			_httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory), "IHttpClientFactory cannot be null");
			_logger = logger ?? throw new ArgumentNullException(nameof(logger), "ILogger cannot be null");
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration), "IConfiguration cannot be null");
			_apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7268";
            _productConfigService = productConfigService ?? throw new ArgumentNullException(nameof(productConfigService));
		}

		/// <summary>
		/// 显示用户仪表板。需要有效的访问令牌。
		/// Displays the user dashboard. Requires valid access token.
		/// </summary>
		/// <returns>仪表板视图，包含用户信息；如果没有令牌则重定向到登录 / Dashboard view with user info; redirects to login if no token</returns>
		[HttpGet]
		[Route("dashboard")]
		public async Task<IActionResult> Index()
		{
			try
			{
				_logger.LogInformation("Dashboard page accessed");

				// Check if user has access token
				var accessToken = HttpContext.Session.GetString("AccessToken");
				if (string.IsNullOrEmpty(accessToken))
				{
					accessToken = HttpContext.Request.Cookies["AccessToken"];
				}

				if (string.IsNullOrEmpty(accessToken))
				{
					_logger.LogWarning("Dashboard accessed without valid token");
					return RedirectToAction("Login", "Account");
				}

				// Fetch user profile from API
				var client = _httpClientFactory.CreateClient();
				client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

				var response = await client.GetAsync($"{_apiBaseUrl}/api/users/me");

				if (!response.IsSuccessStatusCode)
				{
					_logger.LogWarning("Failed to fetch user profile. Status: {StatusCode}", response.StatusCode);
					if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
					{
						HttpContext.Session.Clear();
						Response.Cookies.Delete("AccessToken");
						return RedirectToAction("Login", "Account");
					}

					return View(new Saas.Infra.MVC.Models.DashboardViewModel
					{
						Username = User.Identity?.Name ?? "User",
						Email = User.Identity?.Name,
						WarningMessage = "Failed to load account details."
					});
				}

				var responseContent = await response.Content.ReadAsStringAsync();
                var userProfile = JsonSerializer.Deserialize<UserProfileViewModel>(
                    responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (userProfile == null)
                {
                    _logger.LogError("Failed to deserialize user profile");
                    return RedirectToAction("Login", "Account");
                }

                // Build dashboard view model including available products and tokens
                var dashboardModel = new Saas.Infra.MVC.Models.DashboardViewModel
                {
                    Id = userProfile.Id,
                    Username = userProfile.Username,
                    Email = userProfile.Email,
                    CreatedAt = userProfile.CreatedAt,
                    AccountStatus = userProfile.Status == 1 ? "Active" : "Disabled",
                    AccessToken = accessToken,
                    RefreshToken = HttpContext.Session.GetString("RefreshToken"),
                    ExpiresIn = HttpContext.Session.GetInt32("ExpiresIn") ?? 0
                };

                try
                {
                    var availableProducts = await _productConfigService.GetAvailableProductsAsync(userProfile.Username);
                    if (availableProducts != null && availableProducts.Any())
                    {
                        dashboardModel.Products = availableProducts;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load available products for user {Username}", userProfile.Username);
					dashboardModel.WarningMessage = "Failed to load available products.";
                }

                _logger.LogInformation("Dashboard loaded for user: {Username}", userProfile.Username);
                return View(dashboardModel);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error loading dashboard");
				return RedirectToAction("Login", "Account");
			}
		}
	}

	/// <summary>
	/// 用户资料视图模型。
	/// User profile view model.
	/// </summary>
	public class UserProfileViewModel
	{
		/// <summary>
		/// 用户ID。
		/// User ID.
		/// </summary>
		public Guid Id { get; set; }

		/// <summary>
		/// 用户名。
		/// Username.
		/// </summary>
		public string Username { get; set; } = string.Empty;

		/// <summary>
		/// 账户创建时间（后端返回字段通常为 CreatedTime）。
		/// Account creation time (backend often returns CreatedTime).
		/// </summary>
		public DateTime CreatedTime { get; set; }

		/// <summary>
		/// 账户状态（1=Active,0=Disabled）。
		/// Account status (1=Active, 0=Disabled).
		/// </summary>
		public short Status { get; set; }

		/// <summary>
		/// 兼容属性：CreatedAt，返回 CreatedTime。
		/// Compatibility property: CreatedAt returns CreatedTime.
		/// </summary>
		public DateTime CreatedAt => CreatedTime;

		/// <summary>
		/// 获取账户创建日期的格式化字符串。
		/// Gets the formatted string of account creation date.
		/// </summary>
		public string CreatedAtFormatted => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

		/// <summary>
		/// 获取用户名的首字母，用于头像。
		/// Gets the first letter of username for avatar.
		/// </summary>
		public string AvatarInitial => string.IsNullOrEmpty(Username) ? "U" : Username[0].ToString().ToUpper();

		/// <summary>
		/// 用户Email（后端返回）
		/// </summary>
		public string? Email { get; set; }
	}
}
