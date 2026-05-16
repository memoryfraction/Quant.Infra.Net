using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Quant.Infra.Net.Web.Pages;

/// <summary>
/// Error page model.
/// 错误页面模型。
/// </summary>
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    /// <summary>
    /// Current request id.
    /// 当前请求编号。
    /// </summary>
    public string? RequestId { get; set; }

    /// <summary>
    /// Whether a request id is available.
    /// 是否存在可显示的请求编号。
    /// </summary>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    /// <summary>
    /// Loads error page data.
    /// 加载错误页面数据。
    /// </summary>
    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}

