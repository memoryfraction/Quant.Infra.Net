using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Saas.Infra.MVC.Controllers.Mvc
{
    /// <summary>
    /// 嘉信理财控制器。
    /// Charles Schwab controller.
    /// </summary>
    [Authorize]
    public class SchwabController : Controller
    {
        /// <summary>
        /// 嘉信理财主页。
        /// Schwab home page.
        /// </summary>
        /// <returns>视图结果。 / View result.</returns>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// 股票查询页面。
        /// Stock quotes page.
        /// </summary>
        /// <returns>视图结果。 / View result.</returns>
        public IActionResult Stocks()
        {
            return View();
        }

        /// <summary>
        /// 期权链查询页面。
        /// Option chain page.
        /// </summary>
        /// <returns>视图结果。 / View result.</returns>
        public IActionResult OptionChain()
        {
            return View();
        }

        /// <summary>
        /// 账户管理页面。
        /// Account management page.
        /// </summary>
        /// <returns>视图结果。 / View result.</returns>
        public IActionResult Account()
        {
            return View();
        }

        /// <summary>
        /// 授权页面。
        /// Authorization page.
        /// </summary>
        /// <returns>视图结果。 / View result.</returns>
        public IActionResult Authorize()
        {
            return View();
        }
    }
}
