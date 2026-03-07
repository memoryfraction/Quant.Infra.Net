using Microsoft.AspNetCore.Mvc;
using Saas.Infra.MVC.Models;
using Saas.Infra.MVC.Models.Responses;
using Serilog;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Saas.Infra.MVC.Controllers.Mvc
{
	/// <summary>
	/// MVC账户控制器 - 处理用户认证相关的视图和表单提交。
	/// MVC Account Controller - Handles user authentication views and form submissions.
	/// 注意：这是MVC控制器，返回视图。不同于API控制器(SsoController)返回JSON。
	/// Note: This is an MVC controller returning views. Different from API controller (SsoController) returning JSON.
	/// </summary>
	public class AccountController : Controller
	{
		/// <summary>
		/// HTTP客户端工厂，用于调用后端API。
		/// HTTP client factory for calling backend API.
		/// </summary>
		private readonly IHttpClientFactory _httpClientFactory;

		/// <summary>
		/// 应用程序配置。
		/// Application configuration.
		/// </summary>
		private readonly IConfiguration _configuration;

		/// <summary>
		/// 后端API基础URL。
		/// Backend API base URL.
		/// </summary>
		private readonly string _apiBaseUrl;

		/// <summary>
		/// Redirect validator service
		/// </summary>
		private readonly Saas.Infra.MVC.Services.Redirect.IRedirectValidator _redirectValidator;

		/// <summary>
		/// Product configuration service
		/// </summary>
		private readonly Saas.Infra.MVC.Services.Product.IProductConfigService _productConfigService;

		/// <summary>
		/// 构造函数 - 依赖注入。
		/// Constructor - Dependency injection.
		/// </summary>
		/// <param name="httpClientFactory">HTTP客户端工厂 / HTTP client factory</param>
		/// <param name="configuration">应用程序配置 / Application configuration</param>
		/// <param name="redirectValidator">Redirect validator service</param>
		/// <param name="productConfigService">Product configuration service</param>
		/// <exception cref="ArgumentNullException">当参数为null时抛出 / Thrown when parameter is null</exception>
		public AccountController(
			IHttpClientFactory httpClientFactory,
			IConfiguration configuration,
			Saas.Infra.MVC.Services.Redirect.IRedirectValidator redirectValidator,
			Saas.Infra.MVC.Services.Product.IProductConfigService productConfigService)
		{
			// Parameter validation
			_httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory), "IHttpClientFactory cannot be null");
			_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration), "IConfiguration cannot be null");
			_redirectValidator = redirectValidator ?? throw new ArgumentNullException(nameof(redirectValidator), "IRedirectValidator cannot be null");
			_productConfigService = productConfigService ?? throw new ArgumentNullException(nameof(productConfigService), "IProductConfigService cannot be null");
			_apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7268";
		}

		/// <summary>
		/// 显示登录页面。
		/// Displays the login page.
		/// </summary>
		/// <returns>登录视图 / Login view</returns>
		[HttpGet]
		[Route("account/login")]
		public IActionResult Login()
		{
			Log.Information("Login page accessed");
			
			// Check if user already logged in
			if (HttpContext.Session.GetString("AccessToken") != null)
			{
				return RedirectToAction("Index", "Dashboard");
			}

			return View();
		}

		/// <summary>
		/// 处理登录表单提交。调用后端API进行身份验证。
		/// Handles login form submission. Calls backend API for authentication.
		/// </summary>
		/// <param name="model">登录模型 / Login model</param>
		/// <param name="redirectUrl">Optional redirect URL after successful login</param>
		/// <returns>成功时返回登录成功响应；失败时返回登录页面 / Returns login success response on success; returns login page on failure</returns>
		/// <exception cref="ArgumentNullException">当model为null时抛出 / Thrown when model is null</exception>
		[HttpPost]
		[Route("account/login")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Login([FromForm] LoginViewModel model, [FromQuery] string? redirectUrl = null)
		{
			// Parameter validation
			if (model == null)
				throw new ArgumentNullException(nameof(model), "LoginViewModel cannot be null");

			if (!ModelState.IsValid)
			{
                Log.Warning("Invalid model state for login");
				return View(model);
			}

			try
			{
                Log.Information("Login attempt for email: {Email}", model.Email);

				var client = _httpClientFactory.CreateClient();
				var loginRequest = new
				{
					email = model.Email,
					password = model.Password,
					clientId = "Saas.Infra.Clients"
				};

				var content = new StringContent(
					JsonSerializer.Serialize(loginRequest),
					System.Text.Encoding.UTF8,
					"application/json");

				var response = await client.PostAsync(
					$"{_apiBaseUrl}/sso/generate-token",
					content);

				if (!response.IsSuccessStatusCode)
				{
                    Log.Warning("Login failed for email: {Email}", model.Email);
					ModelState.AddModelError(string.Empty, "Invalid email or password");
					return View(model);
				}

				var responseContent = await response.Content.ReadAsStringAsync();
				var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(
					responseContent,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

				if (tokenResponse == null)
				{
                    Log.Error("Failed to deserialize token response");
					ModelState.AddModelError(string.Empty, "An error occurred during login");
					return View(model);
				}

				// Validate redirect URL
				var validationResult = await _redirectValidator.ValidateAsync(redirectUrl);
				var availableProducts = await _productConfigService.GetAvailableProductsAsync(model.Email);

				// Build login success response
				var loginSuccessResponse = new LoginSuccessResponse
				{
					Success = true,
					AccessToken = tokenResponse.AccessToken,
					RefreshToken = tokenResponse.RefreshToken,
					ExpiresIn = tokenResponse.ExpiresIn,
					RedirectUrl = validationResult.IsValid ? validationResult.ValidatedPath : null,
					ShowProductSelection = !validationResult.IsValid || string.IsNullOrEmpty(validationResult.ValidatedPath),
					AvailableProducts = availableProducts,
					WarningMessage = validationResult.IsValid ? null : "登录成功，但跳转地址无效"
				};

				// Store tokens in session (for backward compatibility)
				HttpContext.Session.SetString("AccessToken", tokenResponse.AccessToken);
				HttpContext.Session.SetString("RefreshToken", tokenResponse.RefreshToken);
				HttpContext.Session.SetInt32("ExpiresIn", tokenResponse.ExpiresIn);

				// Store in cookies for persistence
				Response.Cookies.Append("AccessToken", tokenResponse.AccessToken,
					new Microsoft.AspNetCore.Http.CookieOptions
					{
						HttpOnly = true,
						Secure = true,
						SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
						Expires = DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
					});

				if (model.RememberMe)
				{
					Response.Cookies.Append("RememberEmail", model.Email,
						new Microsoft.AspNetCore.Http.CookieOptions
						{
							Expires = DateTimeOffset.UtcNow.AddDays(30)
						});
				}

                Log.Information("Login successful for email: {Email}", model.Email);
				
				// Return JSON response for AJAX/API clients
				return Json(loginSuccessResponse);
			}
			catch (Exception ex)
			{
                Log.Error(ex, "Error during login for email: {Email}", model.Email);
				ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
				return View(model);
			}
		}

		/// <summary>
		/// 显示注册页面。
		/// Displays the registration page.
		/// </summary>
		/// <returns>注册视图 / Registration view</returns>
		[HttpGet]
		[Route("account/register")]
		public IActionResult Register()
		{
            Log.Information("Registration page accessed");
			return View();
		}

		/// <summary>
		/// 处理注册表单提交。调用后端API创建新用户。
		/// Handles registration form submission. Calls backend API to create new user.
		/// </summary>
		/// <param name="model">注册模型 / Registration model</param>
		/// <returns>成功时重定向到登录页面；失败时返回注册页面 / Redirects to login on success; returns registration page on failure</returns>
		/// <exception cref="ArgumentNullException">当model为null时抛出 / Thrown when model is null</exception>
		[HttpPost]
		[Route("account/register")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Register([FromForm] RegisterViewModel model)
		{
			// Parameter validation
			if (model == null)
				throw new ArgumentNullException(nameof(model), "RegisterViewModel cannot be null");

			if (!ModelState.IsValid)
			{
                Log.Warning("Invalid model state for registration");
				return View(model);
			}

			try
			{
                Log.Information("Registration attempt for email: {Email}", model.Email);

				var client = _httpClientFactory.CreateClient();
				var registerRequest = new
				{
					email = model.Email,
					username = model.Username,
					password = model.Password,
					clientId = "Saas.Infra.Client"
				};

				var content = new StringContent(
					JsonSerializer.Serialize(registerRequest),
					System.Text.Encoding.UTF8,
					"application/json");

				var response = await client.PostAsync(
					$"{_apiBaseUrl}/api/users/register",
					content);

				if (!response.IsSuccessStatusCode)
				{
					var errorContent = await response.Content.ReadAsStringAsync();
                    Log.Warning("Registration failed for email: {Email}. Error: {Error}", model.Email, errorContent);
					ModelState.AddModelError(string.Empty, "Registration failed. Email may already exist.");
					return View(model);
				}

                Log.Information("Registration successful for email: {Email}", model.Email);
				TempData["SuccessMessage"] = "Registration successful! Please log in with your credentials.";
				return RedirectToAction("Login");
			}
			catch (Exception ex)
			{
                Log.Error(ex, "Error during registration for email: {Email}", model.Email);
				ModelState.AddModelError(string.Empty, "An error occurred during registration. Please try again.");
				return View(model);
			}
		}

		/// <summary>
		/// 显示忘记密码页面。
		/// Displays the forgot password page.
		/// </summary>
		/// <returns>忘记密码视图 / Forgot password view</returns>
		[HttpGet]
		[Route("account/forgot-password")]
		public IActionResult ForgotPassword()
		{
            Log.Information("Forgot password page accessed");
			return View();
		}

		/// <summary>
		/// 处理忘记密码表单提交。
		/// Handles forgot password form submission.
		/// </summary>
		/// <param name="model">忘记密码模型 / Forgot password model</param>
		/// <returns>返回忘记密码页面，显示成功消息 / Returns forgot password page with success message</returns>
		/// <exception cref="ArgumentNullException">当model为null时抛出 / Thrown when model is null</exception>
		[HttpPost]
		[Route("account/forgot-password")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ForgotPassword([FromForm] ForgotPasswordViewModel model)
		{
			// Parameter validation
			if (model == null)
				throw new ArgumentNullException(nameof(model), "ForgotPasswordViewModel cannot be null");

			if (!ModelState.IsValid)
			{
                Log.Warning("Invalid model state for forgot password");
				return View(model);
			}

			try
			{
                Log.Information("Forgot password request for email: {Email}", model.Email);

				// TODO: Implement password reset email sending
				// This is a placeholder for the actual implementation
				TempData["InfoMessage"] = "If an account exists with this email, you will receive a password reset link shortly.";

                Log.Information("Forgot password email sent for: {Email}", model.Email);
				return View(model);
			}
			catch (Exception ex)
			{
                Log.Error(ex, "Error during forgot password for email: {Email}", model.Email);
				ModelState.AddModelError(string.Empty, "An error occurred. Please try again.");
				return View(model);
			}
		}

		/// <summary>
		/// 处理用户登出。清除会话和令牌。
		/// Handles user logout. Clears session and tokens.
		/// </summary>
		/// <returns>重定向到主页 / Redirects to home page</returns>
		[HttpPost]
		[Route("account/logout")]
		[ValidateAntiForgeryToken]
		public IActionResult Logout()
		{
            Log.Information("User logout");

			// Clear session
			HttpContext.Session.Clear();

			// Clear cookies
			Response.Cookies.Delete("AccessToken");
			Response.Cookies.Delete("RememberUsername");

			return RedirectToAction("Login", "Account");
		}

		/// <summary>
		/// 显示产品选择页面。
		/// Display product selection page.
		/// </summary>
		/// <returns>ProductSelection view with available products and tokens</returns>
		[HttpGet]
		[Route("account/product-selection")]
		public async Task<IActionResult> ProductSelection()
		{
			// Get access token from session
			var accessToken = HttpContext.Session.GetString("AccessToken");
			var refreshToken = HttpContext.Session.GetString("RefreshToken");
			var expiresIn = HttpContext.Session.GetInt32("ExpiresIn") ?? 0;

			if (string.IsNullOrEmpty(accessToken))
			{
				Log.Warning("ProductSelection accessed without valid session");
				return RedirectToAction("Login", "Account");
			}

			try
			{
				// Get available products
				var availableProducts = await _productConfigService.GetAvailableProductsAsync(string.Empty);

				var viewModel = new ProductSelectionViewModel
				{
					SuccessMessage = "登录成功，请选择要进入的产品",
					Products = availableProducts,
					AccessToken = accessToken,
					RefreshToken = refreshToken,
					ExpiresIn = expiresIn
				};

				Log.Information("ProductSelection page displayed");
				return View(viewModel);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error displaying product selection page");
				return RedirectToAction("Login", "Account");
			}
		}

	}

	/// <summary>
	/// 登录视图模型。
	/// Login view model.
	/// </summary>
	public class LoginViewModel
	{
		/// <summary>
		/// 电子邮件地址。
		/// Email address.
		/// </summary>
		[Required(ErrorMessage = "Email is required")]
		[EmailAddress(ErrorMessage = "Invalid email address")]
		[Display(Name = "Email Address")]
		public string Email { get; set; } = string.Empty;

		/// <summary>
		/// 密码。
		/// Password.
		/// </summary>
		[Required(ErrorMessage = "Password is required")]
		[DataType(DataType.Password)]
		[Display(Name = "Password")]
		public string Password { get; set; } = string.Empty;

		/// <summary>
		/// 是否记住邮箱。
		/// Remember me.
		/// </summary>
		[Display(Name = "Remember me")]
		public bool RememberMe { get; set; }
	}

	/// <summary>
	/// 注册视图模型。
	/// Registration view model.
	/// </summary>
	public class RegisterViewModel
	{
		/// <summary>
		/// 电子邮件地址。
		/// Email address.
		/// </summary>
		[Required(ErrorMessage = "Email is required")]
		[EmailAddress(ErrorMessage = "Invalid email address")]
		[Display(Name = "Email Address")]
		public string Email { get; set; } = string.Empty;

		/// <summary>
		/// 用户名（可选）。
		/// Username (optional).
		/// </summary>
		[StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters")]
		[Display(Name = "Username")]
		public string? Username { get; set; }

		/// <summary>
		/// 密码。
		/// Password.
		/// </summary>
		[Required(ErrorMessage = "Password is required")]
		[StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
		[DataType(DataType.Password)]
		[Display(Name = "Password")]
		public string Password { get; set; } = string.Empty;

		/// <summary>
		/// 确认密码。
		/// Confirm password.
		/// </summary>
		[Required(ErrorMessage = "Password confirmation is required")]
		[DataType(DataType.Password)]
		[Compare("Password", ErrorMessage = "Passwords do not match")]
		[Display(Name = "Confirm Password")]
		public string ConfirmPassword { get; set; } = string.Empty;
	}

	/// <summary>
	/// 忘记密码视图模型。
	/// Forgot password view model.
	/// </summary>
	public class ForgotPasswordViewModel
	{
		/// <summary>
		/// 电子邮件地址。
		/// Email address.
		/// </summary>
		[Required(ErrorMessage = "Email is required")]
		[EmailAddress(ErrorMessage = "Invalid email address")]
		[Display(Name = "Email Address")]
		public string Email { get; set; } = string.Empty;
	}

	/// <summary>
	/// 令牌响应模型 - 来自后端API。
	/// Token response model - from backend API.
	/// </summary>
	public class TokenResponse
	{
		/// <summary>
		/// 访问令牌。
		/// Access token.
		/// </summary>
		public string AccessToken { get; set; } = string.Empty;

		/// <summary>
		/// 刷新令牌。
		/// Refresh token.
		/// </summary>
		public string RefreshToken { get; set; } = string.Empty;

		/// <summary>
		/// 过期时间（秒）。
		/// Expiration time in seconds.
		/// </summary>
		public int ExpiresIn { get; set; }

		/// <summary>
		/// 令牌类型。
		/// Token type.
		/// </summary>
		public string TokenType { get; set; } = "Bearer";
	}
}
