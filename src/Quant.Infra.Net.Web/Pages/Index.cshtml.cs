using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Quant.Infra.Net.Web.Pages;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<IndexModel> _logger;
    public string? ErrorMessage { get; set; }

    public IndexModel(IHttpClientFactory http, ILogger<IndexModel> logger)
    {
        _http = http;
        _logger = logger;
    }

    private const string RedirectUri = "https://127.0.0.1";

    public async Task<IActionResult> OnGetAsync(string? code, string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            ErrorMessage = $"Schwab 授权失败: {error}";
            return Page();
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            var appKey = HttpContext.Session.GetString("AppKey");
            var appSecret = HttpContext.Session.GetString("AppSecret");

            if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(appSecret))
            {
                ErrorMessage = "Session 已过期。请从 https://127.0.0.1 重新打开本页并再次授权。";
                return Page();
            }

            try
            {
                var token = await ExchangeCodeAsync(NormalizeAuthorizationCode(code), appKey, appSecret);
                HttpContext.Session.SetString("AccessToken", token);
                return RedirectToPage("/Dashboard");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token exchange failed");
                ErrorMessage = $"授权码换取 Token 失败: {BuildDetailedError(ex)}";
                return Page();
            }
        }

        if (!string.IsNullOrEmpty(HttpContext.Session.GetString("AccountNumber")))
            return RedirectToPage("/Dashboard");

        return Page();
    }

    public IActionResult OnPostDemo()
    {
        HttpContext.Session.SetString("AccountNumber", "DEMO");
        HttpContext.Session.SetString("AppKey",        "DEMO");
        HttpContext.Session.SetString("AppSecret",     "DEMO");
        HttpContext.Session.SetString("DemoMode",      "true");
        return RedirectToPage("/Dashboard");
    }

    public IActionResult OnPostStartAuth(string appKey, string appSecret, string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(appKey) ||
            string.IsNullOrWhiteSpace(appSecret) ||
            string.IsNullOrWhiteSpace(accountNumber))
        {
            ErrorMessage = "请填写 Client ID、Client Secret 和账户号码";
            return Page();
        }

        HttpContext.Session.SetString("AppKey",        appKey.Trim());
        HttpContext.Session.SetString("AppSecret",     appSecret.Trim());
        HttpContext.Session.SetString("AccountNumber", accountNumber.Trim());
        HttpContext.Session.Remove("DemoMode");

        var url = "https://api.schwabapi.com/v1/oauth/authorize"
            + $"?response_type=code"
            + $"&client_id={Uri.EscapeDataString(appKey.Trim())}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}";

        return Redirect(url);
    }

    public async Task<IActionResult> OnPostAsync(
        string appKey, string appSecret, string accountNumber, string? authCode)
    {
        if (string.IsNullOrWhiteSpace(appKey) ||
            string.IsNullOrWhiteSpace(appSecret) ||
            string.IsNullOrWhiteSpace(accountNumber))
        {
            ErrorMessage = "请填写 Client ID、Client Secret 和账户号码";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(authCode))
        {
            ErrorMessage = "请粘贴授权码（code= 后面的内容）";
            return Page();
        }

        HttpContext.Session.SetString("AppKey",        appKey.Trim());
        HttpContext.Session.SetString("AppSecret",     appSecret.Trim());
        HttpContext.Session.SetString("AccountNumber", accountNumber.Trim());

        try
        {
            var token = await ExchangeCodeAsync(NormalizeAuthorizationCode(authCode), appKey.Trim(), appSecret.Trim());
            HttpContext.Session.SetString("AccessToken", token);
            return RedirectToPage("/Dashboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token exchange failed");
            ErrorMessage = $"授权码换取 Token 失败: {BuildDetailedError(ex)}";
            return Page();
        }
    }

    private async Task<string> ExchangeCodeAsync(string code, string appKey, string appSecret)
    {
        var client = _http.CreateClient();
        client.DefaultRequestVersion = HttpVersion.Version11;
        client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

        var creds  = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{appKey}:{appSecret}"));

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type",   "authorization_code" },
            { "code",         code },
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

        var resp    = await client.SendAsync(request);
        var content = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)resp.StatusCode}: {content}");

        var json = JsonSerializer.Deserialize<JsonElement>(content);
        return json.GetProperty("access_token").GetString()
               ?? throw new Exception("access_token 为空");
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
