using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Quant.Infra.Net.Console
{
    /// <summary>
    /// Schwab OAuth 认证演示
    /// 展示如何打开浏览器进行 OAuth 登录
    /// </summary>
    public class SchwabAuthDemo
    {
        private readonly string _appKey;
        private readonly string _redirectUri;
        private readonly HttpListener _httpListener;

        public SchwabAuthDemo(string appKey, string redirectUri = "http://localhost:8080/callback")
        {
            _appKey = appKey;
            _redirectUri = redirectUri;
            _httpListener = new HttpListener();
        }

        /// <summary>
        /// 启动 OAuth 认证流程
        /// </summary>
        public async Task<string> StartAuthenticationAsync()
        {
            try
            {
                // 1. 构建授权 URL
                var authUrl = BuildAuthorizationUrl();
                
                System.Console.WriteLine("=".PadRight(80, '='));
                System.Console.WriteLine("Charles Schwab OAuth 认证流程");
                System.Console.WriteLine("=".PadRight(80, '='));
                System.Console.WriteLine();
                System.Console.WriteLine("步骤 1: 打开浏览器进行授权");
                System.Console.WriteLine($"授权 URL: {authUrl}");
                System.Console.WriteLine();
                System.Console.WriteLine("即将在浏览器中打开 Schwab 登录页面...");
                System.Console.WriteLine("请在浏览器中完成登录和授权操作");
                System.Console.WriteLine();
                
                // 2. 启动本地 HTTP 服务器监听回调
                StartLocalServer();
                
                // 3. 打开浏览器
                await Task.Delay(2000); // 等待 2 秒让用户看到提示
                OpenBrowser(authUrl);
                
                System.Console.WriteLine("✓ 浏览器已打开");
                System.Console.WriteLine("✓ 本地服务器正在监听回调...");
                System.Console.WriteLine();
                System.Console.WriteLine("等待授权完成...");
                System.Console.WriteLine("(完成授权后，浏览器会自动跳转回本地)");
                System.Console.WriteLine();
                
                // 4. 等待回调并获取授权码
                var authorizationCode = await WaitForCallbackAsync();
                
                if (!string.IsNullOrEmpty(authorizationCode))
                {
                    System.Console.WriteLine("=".PadRight(80, '='));
                    System.Console.WriteLine("✓ 授权成功！");
                    System.Console.WriteLine($"授权码: {authorizationCode.Substring(0, Math.Min(20, authorizationCode.Length))}...");
                    System.Console.WriteLine("=".PadRight(80, '='));
                    return authorizationCode;
                }
                else
                {
                    System.Console.WriteLine("✗ 授权失败或被取消");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"✗ 错误: {ex.Message}");
                return string.Empty;
            }
            finally
            {
                StopLocalServer();
            }
        }

        /// <summary>
        /// 构建授权 URL
        /// </summary>
        private string BuildAuthorizationUrl()
        {
            var baseUrl = "https://api.schwabapi.com/v1/oauth/authorize";
            var queryParams = new StringBuilder();
            queryParams.Append($"?client_id={Uri.EscapeDataString(_appKey)}");
            queryParams.Append($"&redirect_uri={Uri.EscapeDataString(_redirectUri)}");
            queryParams.Append("&response_type=code");
            
            return baseUrl + queryParams.ToString();
        }

        /// <summary>
        /// 启动本地 HTTP 服务器
        /// </summary>
        private void StartLocalServer()
        {
            try
            {
                _httpListener.Prefixes.Add($"{_redirectUri}/");
                _httpListener.Start();
                System.Console.WriteLine($"✓ 本地服务器已启动: {_redirectUri}");
            }
            catch (HttpListenerException ex)
            {
                System.Console.WriteLine($"✗ 启动本地服务器失败: {ex.Message}");
                System.Console.WriteLine("提示: 请确保端口 8080 未被占用，或以管理员权限运行");
                throw;
            }
        }

        /// <summary>
        /// 停止本地 HTTP 服务器
        /// </summary>
        private void StopLocalServer()
        {
            try
            {
                if (_httpListener.IsListening)
                {
                    _httpListener.Stop();
                    _httpListener.Close();
                    System.Console.WriteLine("✓ 本地服务器已关闭");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"关闭服务器时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 等待 OAuth 回调
        /// </summary>
        private async Task<string> WaitForCallbackAsync()
        {
            try
            {
                // 等待请求
                var context = await _httpListener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;

                // 解析查询参数
                var queryString = request.Url?.Query;
                if (!string.IsNullOrEmpty(queryString))
                {
                    var queryParams = HttpUtility.ParseQueryString(queryString);
                    var code = queryParams["code"];
                    var error = queryParams["error"];

                    // 发送响应页面
                    string responseHtml;
                    if (!string.IsNullOrEmpty(code))
                    {
                        responseHtml = GetSuccessHtml();
                    }
                    else
                    {
                        responseHtml = GetErrorHtml(error ?? "未知错误");
                    }

                    var buffer = Encoding.UTF8.GetBytes(responseHtml);
                    response.ContentLength64 = buffer.Length;
                    response.ContentType = "text/html; charset=utf-8";
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.OutputStream.Close();

                    return code ?? string.Empty;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"等待回调时出错: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 打开浏览器
        /// </summary>
        private void OpenBrowser(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Windows
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    // Linux
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    // macOS
                    Process.Start("open", url);
                }
                else
                {
                    System.Console.WriteLine($"请手动在浏览器中打开以下 URL:");
                    System.Console.WriteLine(url);
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"打开浏览器失败: {ex.Message}");
                System.Console.WriteLine($"请手动在浏览器中打开以下 URL:");
                System.Console.WriteLine(url);
            }
        }

        /// <summary>
        /// 成功页面 HTML
        /// </summary>
        private string GetSuccessHtml()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>授权成功 - Schwab OAuth</title>
    <style>
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }
        .container {
            background: white;
            padding: 40px;
            border-radius: 10px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
            text-align: center;
            max-width: 500px;
        }
        .success-icon {
            font-size: 64px;
            color: #28a745;
            margin-bottom: 20px;
        }
        h1 {
            color: #333;
            margin-bottom: 10px;
        }
        p {
            color: #666;
            line-height: 1.6;
        }
        .info-box {
            background: #f8f9fa;
            padding: 20px;
            border-radius: 5px;
            margin-top: 20px;
        }
    </style>
</head>
<body>
    <div class='container'>
        <div class='success-icon'>✓</div>
        <h1>授权成功！</h1>
        <p>您已成功授权 Quant Trading System 访问您的 Schwab 账户。</p>
        <div class='info-box'>
            <p><strong>下一步：</strong></p>
            <p>授权码已发送到应用程序。</p>
            <p>您现在可以关闭此窗口。</p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// 错误页面 HTML
        /// </summary>
        private string GetErrorHtml(string error)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>授权失败 - Schwab OAuth</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
        }}
        .container {{
            background: white;
            padding: 40px;
            border-radius: 10px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
            text-align: center;
            max-width: 500px;
        }}
        .error-icon {{
            font-size: 64px;
            color: #dc3545;
            margin-bottom: 20px;
        }}
        h1 {{
            color: #333;
            margin-bottom: 10px;
        }}
        p {{
            color: #666;
            line-height: 1.6;
        }}
        .error-box {{
            background: #fff3cd;
            padding: 20px;
            border-radius: 5px;
            margin-top: 20px;
            border-left: 4px solid #ffc107;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='error-icon'>✗</div>
        <h1>授权失败</h1>
        <p>授权过程中出现错误。</p>
        <div class='error-box'>
            <p><strong>错误信息：</strong></p>
            <p>{error}</p>
            <p>请返回应用程序重试。</p>
        </div>
    </div>
</body>
</html>";
        }

        /// <summary>
        /// 演示模式 - 显示登录界面预览
        /// </summary>
        public static void ShowLoginPreview()
        {
            System.Console.Clear();
            System.Console.WriteLine("╔" + "═".PadRight(78, '═') + "╗");
            System.Console.WriteLine("║" + " Charles Schwab 登录界面预览".PadRight(78) + "║");
            System.Console.WriteLine("╚" + "═".PadRight(78, '═') + "╝");
            System.Console.WriteLine();
            
            System.Console.WriteLine("当您启动 OAuth 认证流程时，浏览器会打开以下页面：");
            System.Console.WriteLine();
            System.Console.WriteLine("┌" + "─".PadRight(78, '─') + "┐");
            System.Console.WriteLine("│ " + "🔐 Charles Schwab - 登录".PadRight(77) + "│");
            System.Console.WriteLine("├" + "─".PadRight(78, '─') + "┤");
            System.Console.WriteLine("│                                                                              │");
            System.Console.WriteLine("│   [Schwab Logo]                                                              │");
            System.Console.WriteLine("│                                                                              │");
            System.Console.WriteLine("│   登录您的账户                                                                │");
            System.Console.WriteLine("│                                                                              │");
            System.Console.WriteLine("│   ┌────────────────────────────────────────────────────────┐               │");
            System.Console.WriteLine("│   │ 用户名或账户号码                                        │               │");
            System.Console.WriteLine("│   └────────────────────────────────────────────────────────┘               │");
            System.Console.WriteLine("│                                                                              │");
            System.Console.WriteLine("│   ┌────────────────────────────────────────────────────────┐               │");
            System.Console.WriteLine("│   │ 密码                                                    │               │");
            System.Console.WriteLine("│   └────────────────────────────────────────────────────────┘               │");
            System.Console.WriteLine("│                                                                              │");
            System.Console.WriteLine("│   [ ] 记住我                                                                 │");
            System.Console.WriteLine("│                                                                              │");
            System.Console.WriteLine("│   ┌────────────────────────────────────────────────────────┐               │");
            System.Console.WriteLine("│   │              [  登  录  ]                               │               │");
            System.Console.WriteLine("│   └────────────────────────────────────────────────────────┘               │");
            System.Console.WriteLine("│                                                                              │");
            System.Console.WriteLine("│   忘记密码？  |  需要帮助？                                                  │");
            System.Console.WriteLine("│                                                                              │");
            System.Console.WriteLine("└" + "─".PadRight(78, '─') + "┘");
            System.Console.WriteLine();
            System.Console.WriteLine("登录后，您会看到授权页面：");
            System.Console.WriteLine();
            System.Console.WriteLine("┌" + "─".PadRight(78, '─') + "┐");
            System.Console.WriteLine("│ " + "🔓 授权应用访问".PadRight(77) + "│");
            System.Console.WriteLine("├" + "─".PadRight(78, '─') + "┤");
            System.Console.WriteLine("│                                                                              │");
            System.Console.WriteLine("│   Quant Trading System 请求访问您的账户                                       │");
            System.Console.WriteLine("│                                                                              │");
            System.Console.WriteLine("│   该应用将能够：                                                              │");
            System.Console.WriteLine("│   ✓ 查看账户信息和余额                                                       │");
            System.Console.WriteLine("│   ✓ 查看持仓和交易历史                                                       │");
            System.Console.WriteLine("│   ✓ 获取市场数据和报价                                                       │");
            System.Console.WriteLine("│   ✓ 代表您执行交易                                                           │");
            System.Console.WriteLine("│                                                                              │");
            System.Console.WriteLine("│   ┌──────────────────┐  ┌──────────────────┐                               │");
            System.Console.WriteLine("│   │   [  拒绝  ]     │  │   [  授权  ]     │                               │");
            System.Console.WriteLine("│   └──────────────────┘  └──────────────────┘                               │");
            System.Console.WriteLine("│                                                                              │");
            System.Console.WriteLine("└" + "─".PadRight(78, '─') + "┘");
            System.Console.WriteLine();
            System.Console.WriteLine("授权成功后，浏览器会自动跳转回应用程序。");
            System.Console.WriteLine();
        }
    }
}
