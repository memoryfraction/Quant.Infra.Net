using Microsoft.AspNetCore.Mvc;
using Saas.Infra.MVC.Models;
using System.Diagnostics;

namespace Saas.Infra.MVC.Controllers.Mvc
{
	/// <summary>
	/// 提供主页和隐私页面的视图。
	/// Provides views for the home page and privacy page.
	/// </summary>
	public class HomeController : Controller
	{
		/// <summary>
		/// 获取主页视图。
		/// Gets the home page view.
		/// </summary>
		/// <returns>主页视图。 / The home page view.</returns>
		public IActionResult Index()
		{
			return View();
		}

		/// <summary>
		/// 获取隐私政策页面视图。
		/// Gets the privacy policy page view.
		/// </summary>
		/// <returns>隐私政策页面视图。 / The privacy policy page view.</returns>
		public IActionResult Privacy()
		{
			return View();
		}

		/// <summary>
		/// 获取错误页面视图。禁用响应缓存以确保显示最新的错误信息。
		/// Gets the error page view. Response caching is disabled to ensure the latest error information is displayed.
		/// </summary>
		/// <returns>包含错误信息的错误页面视图。 / The error page view with error information.</returns>
		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error()
		{
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}
	}
}
