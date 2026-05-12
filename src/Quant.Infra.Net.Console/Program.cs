using Microsoft.Extensions.Configuration;
using System;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Console
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            System.Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            // 检查是否指定了 Schwab 演示
            if (args.Length > 0 && args[0] == "schwab")
            {
                await SchwabAuthDemoProgram.RunAsync(args);
                return;
            }
            
            // 默认显示菜单
            await ShowMainMenuAsync();
        }
        
        static async Task ShowMainMenuAsync()
        {
            while (true)
            {
                System.Console.Clear();
                System.Console.WriteLine("╔" + "═".PadRight(78, '═') + "╗");
                System.Console.WriteLine("║" + " Quant.Infra.Net Console".PadRight(78) + "║");
                System.Console.WriteLine("╚" + "═".PadRight(78, '═') + "╝");
                System.Console.WriteLine();
                System.Console.WriteLine("  1. Schwab OAuth 认证演示");
                System.Console.WriteLine("  2. Binance 交易演示 (原有功能)");
                System.Console.WriteLine("  0. 退出");
                System.Console.WriteLine();
                System.Console.Write("请选择 (输入数字): ");
                
                var choice = System.Console.ReadLine();
                
                switch (choice)
                {
                    case "1":
                        await SchwabAuthDemoProgram.RunAsync(new string[0]);
                        break;
                        
                    case "2":
                        System.Console.WriteLine("\nBinance 功能暂时禁用，请使用旧版本 Program.cs");
                        System.Console.WriteLine("按任意键继续...");
                        System.Console.ReadKey();
                        break;
                        
                    case "0":
                        System.Console.WriteLine("\n再见！");
                        return;
                        
                    default:
                        System.Console.WriteLine("\n无效的选择，请重试。");
                        System.Console.WriteLine("按任意键继续...");
                        System.Console.ReadKey();
                        break;
                }
            }
        }
    }
}
