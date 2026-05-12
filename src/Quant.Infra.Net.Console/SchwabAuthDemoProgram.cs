using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Console
{
    /// <summary>
    /// Schwab OAuth 认证演示程序
    /// 运行方式: dotnet run --project src/Quant.Infra.Net.Console -- schwab-auth
    /// </summary>
    public class SchwabAuthDemoProgram
    {
        public static async Task RunAsync(string[] args)
        {
            System.Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            // 显示菜单
            ShowMenu();
            
            while (true)
            {
                System.Console.Write("\n请选择操作 (输入数字): ");
                var choice = System.Console.ReadLine();
                
                switch (choice)
                {
                    case "1":
                        // 显示登录界面预览
                        SchwabAuthDemo.ShowLoginPreview();
                        break;
                        
                    case "2":
                        // 启动真实 OAuth 流程
                        await StartRealOAuthFlow();
                        break;
                        
                    case "3":
                        // 显示配置说明
                        ShowConfigurationGuide();
                        break;
                        
                    case "0":
                        System.Console.WriteLine("\n再见！");
                        return;
                        
                    default:
                        System.Console.WriteLine("\n无效的选择，请重试。");
                        break;
                }
                
                System.Console.WriteLine("\n按任意键继续...");
                System.Console.ReadKey();
                ShowMenu();
            }
        }
        
        static void ShowMenu()
        {
            System.Console.Clear();
            System.Console.WriteLine("╔" + "═".PadRight(78, '═') + "╗");
            System.Console.WriteLine("║" + " Charles Schwab OAuth 认证演示".PadRight(78) + "║");
            System.Console.WriteLine("╚" + "═".PadRight(78, '═') + "╝");
            System.Console.WriteLine();
            System.Console.WriteLine("  1. 查看登录界面预览（模拟）");
            System.Console.WriteLine("  2. 启动真实 OAuth 认证流程（需要 App Key）");
            System.Console.WriteLine("  3. 查看配置说明");
            System.Console.WriteLine("  0. 退出");
            System.Console.WriteLine();
        }
        
        static async Task StartRealOAuthFlow()
        {
            System.Console.Clear();
            System.Console.WriteLine("╔" + "═".PadRight(78, '═') + "╗");
            System.Console.WriteLine("║" + " 启动真实 OAuth 认证流程".PadRight(78) + "║");
            System.Console.WriteLine("╚" + "═".PadRight(78, '═') + "╝");
            System.Console.WriteLine();
            
            // 尝试从配置读取 App Key
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddUserSecrets<SchwabAuthDemoProgram>(optional: true)
                .Build();
            
            var appKey = config["Schwab:AppKey"];
            
            if (string.IsNullOrEmpty(appKey))
            {
                System.Console.WriteLine("⚠️  未找到 Schwab App Key");
                System.Console.WriteLine();
                System.Console.WriteLine("请先配置 App Key：");
                System.Console.WriteLine("  dotnet user-secrets set \"Schwab:AppKey\" \"your-app-key\"");
                System.Console.WriteLine();
                System.Console.Write("或者现在输入 App Key（用于测试）: ");
                appKey = System.Console.ReadLine();
                
                if (string.IsNullOrEmpty(appKey))
                {
                    System.Console.WriteLine("\n✗ 未提供 App Key，无法继续");
                    return;
                }
            }
            
            System.Console.WriteLine($"✓ 使用 App Key: {appKey.Substring(0, Math.Min(10, appKey.Length))}...");
            System.Console.WriteLine();
            
            // 创建认证演示实例
            var authDemo = new SchwabAuthDemo(appKey);
            
            // 启动认证流程
            var authCode = await authDemo.StartAuthenticationAsync();
            
            if (!string.IsNullOrEmpty(authCode))
            {
                System.Console.WriteLine();
                System.Console.WriteLine("下一步：使用授权码交换访问令牌");
                System.Console.WriteLine("（这部分功能在 SchwabBrokerService 中实现）");
            }
        }
        
        static void ShowConfigurationGuide()
        {
            System.Console.Clear();
            System.Console.WriteLine("╔" + "═".PadRight(78, '═') + "╗");
            System.Console.WriteLine("║" + " Schwab API 配置指南".PadRight(78) + "║");
            System.Console.WriteLine("╚" + "═".PadRight(78, '═') + "╝");
            System.Console.WriteLine();
            
            System.Console.WriteLine("📋 配置步骤：");
            System.Console.WriteLine();
            System.Console.WriteLine("1. 获取 Schwab 开发者凭据");
            System.Console.WriteLine("   - 访问: https://developer.schwab.com/");
            System.Console.WriteLine("   - 注册并创建应用");
            System.Console.WriteLine("   - 获取 App Key 和 App Secret");
            System.Console.WriteLine();
            
            System.Console.WriteLine("2. 配置用户机密");
            System.Console.WriteLine("   dotnet user-secrets init");
            System.Console.WriteLine("   dotnet user-secrets set \"Schwab:AppKey\" \"your-app-key\"");
            System.Console.WriteLine("   dotnet user-secrets set \"Schwab:Secret\" \"your-app-secret\"");
            System.Console.WriteLine("   dotnet user-secrets set \"Schwab:AccountNumber\" \"your-account\"");
            System.Console.WriteLine();
            
            System.Console.WriteLine("3. 设置回调 URL");
            System.Console.WriteLine("   在 Schwab 开发者门户中设置:");
            System.Console.WriteLine("   http://localhost:8080/callback");
            System.Console.WriteLine();
            
            System.Console.WriteLine("4. 运行认证流程");
            System.Console.WriteLine("   选择菜单选项 2 启动 OAuth 认证");
            System.Console.WriteLine();
            
            System.Console.WriteLine("📚 更多信息：");
            System.Console.WriteLine("   - 查看 SCHWAB_DEVELOPER_REGISTRATION_GUIDE.md");
            System.Console.WriteLine("   - 查看 SCHWAB_QUICKSTART.md");
            System.Console.WriteLine();
        }
    }
}
