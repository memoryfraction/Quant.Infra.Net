using System.Net;
using System.Text;

namespace Quant.Infra.Net.Web;

/// <summary>
/// Listens on https://127.0.0.1/oauth/callback (port 443).
/// When Schwab redirects back with ?code=XXX, this listener
/// writes the code to a temp file and sends a redirect page
/// that forwards the browser to http://localhost:5237/OAuthCallback?code=XXX
/// </summary>
public static class SchwabCallbackListener
{
    // Temp file shared between this listener and the OAuthCallback Razor page
    public static readonly string TempCodeFile =
        Path.Combine(Path.GetTempPath(), "schwab_oauth_code.tmp");

    public static async Task StartAsync(CancellationToken ct, ILogger logger)
    {
        // Requires the app to be run as Administrator (to bind port 443)
        // OR use netsh to pre-register the URL:
        //   netsh http add urlacl url=https://127.0.0.1/oauth/callback/ user=Everyone
        var listener = new HttpListener();
        listener.Prefixes.Add("https://127.0.0.1/oauth/callback/");

        try
        {
            listener.Start();
            logger.LogInformation("[SchwabCallback] Listening on https://127.0.0.1/oauth/callback/");

            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await listener.GetContextAsync();
                }
                catch (HttpListenerException) { break; }

                var query = ctx.Request.Url?.Query ?? "";
                var parsed = System.Web.HttpUtility.ParseQueryString(query);
                var code  = parsed["code"]  ?? "";
                var error = parsed["error"] ?? "";

                // Save code to temp file so the Razor page can read it
                if (!string.IsNullOrEmpty(code))
                    await File.WriteAllTextAsync(TempCodeFile, code, ct);

                // Send a self-redirecting HTML page to the browser
                var redirectUrl = string.IsNullOrEmpty(error)
                    ? $"http://localhost:5237/oauth/callback?code={Uri.EscapeDataString(code)}"
                    : $"http://localhost:5237/oauth/callback?error={Uri.EscapeDataString(error)}";

                var html = $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'>
<meta http-equiv='refresh' content='0;url={redirectUrl}'>
<title>授权中...</title>
<style>
  body{{font-family:sans-serif;display:flex;align-items:center;
       justify-content:center;height:100vh;margin:0;
       background:linear-gradient(135deg,#667eea,#764ba2);color:white;}}
  .box{{text-align:center;}}
  .spinner{{width:40px;height:40px;border:4px solid rgba(255,255,255,.3);
            border-top-color:white;border-radius:50%;
            animation:spin .8s linear infinite;margin:20px auto;}}
  @keyframes spin{{to{{transform:rotate(360deg)}}}}
</style></head>
<body><div class='box'>
  <div class='spinner'></div>
  <p>授权成功，正在跳转...</p>
  <p><a href='{redirectUrl}' style='color:white'>点击这里</a>（如未自动跳转）</p>
</div></body></html>";

                var bytes = Encoding.UTF8.GetBytes(html);
                ctx.Response.ContentType     = "text/html; charset=utf-8";
                ctx.Response.ContentLength64 = bytes.Length;
                await ctx.Response.OutputStream.WriteAsync(bytes, ct);
                ctx.Response.OutputStream.Close();

                logger.LogInformation("[SchwabCallback] Received code, redirecting to app");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("[SchwabCallback] Listener failed: {Msg}. " +
                "Run as Administrator or register URL with netsh.", ex.Message);
        }
        finally
        {
            if (listener.IsListening) listener.Stop();
        }
    }
}
