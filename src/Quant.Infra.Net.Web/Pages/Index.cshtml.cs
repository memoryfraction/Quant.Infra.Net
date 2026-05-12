using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Quant.Infra.Net.Web.Pages;

/// <summary>
/// Schwab sign-in page model.
/// Schwab 登录页模型。
/// </summary>
public class IndexModel : PageModel
{
    private const string RedirectUri = "https://127.0.0.1";

    private readonly IHttpClientFactory _http;
    private readonly ILogger<IndexModel> _logger;

    /// <summary>
    /// User-visible error message.
    /// 用户可见的错误消息。
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a new Schwab sign-in page model.
    /// 创建 Schwab 登录页模型。
    /// </summary>
    public IndexModel(IHttpClientFactory http, ILogger<IndexModel> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Handles initial page load and Schwab OAuth redirects.
    /// 处理初始页面加载和 Schwab OAuth 回调。
    /// </summary>
    public async Task<IActionResult> OnGetAsync(string? code, string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            ErrorMessage = $"Schwab authorization failed: {error}";
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            return await HandleAuthorizationCodeAsync(code);
        }

        if (!string.IsNullOrEmpty(HttpContext.Session.GetString("AccountNumber")))
            return RedirectToPage("/Dashboard");

        return Page();
    }

    /// <summary>
    /// Starts demo mode with simulated account data.
    /// 启动使用模拟账户数据的演示模式。
    /// </summary>
    public IActionResult OnPostDemo()
    {
        HttpContext.Session.SetString("AccountNumber", "DEMO");
        HttpContext.Session.SetString("AppKey", "DEMO");
        HttpContext.Session.SetString("AppSecret", "DEMO");
        HttpContext.Session.SetString("DemoMode", "true");
        return RedirectToPage("/Dashboard");
    }

    /// <summary>
    /// Stores credentials in session and redirects to Schwab authorization.
    /// 将凭据保存到会话并跳转到 Schwab 授权页面。
    /// </summary>
    public IActionResult OnPostStartAuth(string appKey, string appSecret, string accountNumber)
    {
        if (!TryStoreCredentials(appKey, appSecret, accountNumber))
            return Page();

        var url = "https://api.schwabapi.com/v1/oauth/authorize"
            + "?response_type=code"
            + $"&client_id={Uri.EscapeDataString(appKey.Trim())}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}";

        return Redirect(url);
    }

    /// <summary>
    /// Exchanges a manually submitted authorization code.
    /// 交换手工提交的授权码。
    /// </summary>
    public async Task<IActionResult> OnPostAsync(
        string appKey, string appSecret, string accountNumber, string? authCode)
    {
        if (!TryStoreCredentials(appKey, appSecret, accountNumber))
            return Page();

        if (string.IsNullOrWhiteSpace(authCode))
        {
            ErrorMessage = "Please paste the authorization code or the full callback URL.";
            return Page();
        }

        return await HandleAuthorizationCodeAsync(authCode, appKey.Trim(), appSecret.Trim());
    }

    private bool TryStoreCredentials(string appKey, string appSecret, string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(appKey) ||
            string.IsNullOrWhiteSpace(appSecret) ||
            string.IsNullOrWhiteSpace(accountNumber))
        {
            ErrorMessage = "Please enter Client ID, Client Secret, and account number.";
            return false;
        }

        HttpContext.Session.SetString("AppKey", appKey.Trim());
        HttpContext.Session.SetString("AppSecret", appSecret.Trim());
        HttpContext.Session.SetString("AccountNumber", accountNumber.Trim());
        HttpContext.Session.Remove("DemoMode");
        return true;
    }

    private async Task<IActionResult> HandleAuthorizationCodeAsync(string code)
    {
        var appKey = HttpContext.Session.GetString("AppKey");
        var appSecret = HttpContext.Session.GetString("AppSecret");

        if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(appSecret))
        {
            ErrorMessage = "Session expired. Please reopen https://127.0.0.1 and authorize again.";
            return Page();
        }

        return await HandleAuthorizationCodeAsync(code, appKey, appSecret);
    }

    private async Task<IActionResult> HandleAuthorizationCodeAsync(string code, string appKey, string appSecret)
    {
        try
        {
            var token = await ExchangeCodeAsync(NormalizeAuthorizationCode(code), appKey, appSecret);
            HttpContext.Session.SetString("AccessToken", token);
            return RedirectToPage("/Dashboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token exchange failed");
            ErrorMessage = $"Authorization code token exchange failed: {BuildDetailedError(ex)}";
            return Page();
        }
    }

    private async Task<string> ExchangeCodeAsync(string code, string appKey, string appSecret)
    {
        var client = _http.CreateClient();
        client.DefaultRequestVersion = HttpVersion.Version11;
        client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{appKey}:{appSecret}"));

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", RedirectUri }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.schwabapi.com/v1/oauth/token")
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = body
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("Quant.Infra.Net.Web/1.0");

        var resp = await client.SendAsync(request);
        var content = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)resp.StatusCode}: {content}");

        var json = JsonSerializer.Deserialize<JsonElement>(content);
        return json.GetProperty("access_token").GetString()
               ?? throw new Exception("access_token is empty");
    }

    private static string NormalizeAuthorizationCode(string input)
    {
        var value = input.Trim();

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            value = query["code"] ?? value;
        }

        return Uri.UnescapeDataString(value);
    }

    private static string BuildDetailedError(Exception ex)
    {
        var messages = new List<string>();
        for (var current = ex; current != null; current = current.InnerException)
            messages.Add(current.Message);

        return string.Join(" -> ", messages);
    }
}
