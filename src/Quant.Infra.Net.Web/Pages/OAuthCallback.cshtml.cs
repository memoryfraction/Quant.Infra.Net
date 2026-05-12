using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Quant.Infra.Net.Web.Pages;

public class OAuthCallbackModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly ILogger<OAuthCallbackModel> _logger;

    public bool    Success      { get; private set; }
    public string? ErrorMessage { get; private set; }

    public OAuthCallbackModel(IHttpClientFactory http, ILogger<OAuthCallbackModel> logger)
    {
        _http   = http;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(string? code, string? error)
    {
        if (!string.IsNullOrEmpty(error))
        {
            ErrorMessage = $"Schwab 返回错误: {error}";
            return Page();
        }

        if (string.IsNullOrEmpty(code))
        {
            ErrorMessage = "未收到授权码，请重试";
            return Page();
        }

        var appKey    = HttpContext.Session.GetString("AppKey");
        var appSecret = HttpContext.Session.GetString("AppSecret");

        if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(appSecret))
        {
            ErrorMessage = "Session 已过期，请重新登录";
            return Page();
        }

        try
        {
            var token = await ExchangeCodeAsync(code, appKey, appSecret);
            HttpContext.Session.SetString("AccessToken", token);
            Success = true;
            return Page(); // page auto-redirects to /Dashboard after 1.5s
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token exchange failed");
            ErrorMessage = $"获取 Access Token 失败: {ex.Message}";
            return Page();
        }
    }

    private async Task<string> ExchangeCodeAsync(string code, string appKey, string appSecret)
    {
        var client = _http.CreateClient();
        var creds  = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{appKey}:{appSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type",   "authorization_code" },
            { "code",         code },
            { "redirect_uri", "https://127.0.0.1/oauth/callback" }
        });

        var resp    = await client.PostAsync("https://api.schwabapi.com/v1/oauth/token", body);
        var content = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)resp.StatusCode} – {content}");

        var json = JsonSerializer.Deserialize<JsonElement>(content);
        return json.GetProperty("access_token").GetString()
               ?? throw new Exception("access_token 为空");
    }
}
