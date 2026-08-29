using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Quant.Infra.Net.Mcp;

/// <summary>
/// Quant.Infra.Net MCP server 入口（stdio 传输，供 Claude Desktop 等 MCP 客户端直接拉起）。
/// Entry point for the Quant.Infra.Net MCP server (stdio transport; Claude Desktop and other MCP
/// clients launch this process and speak MCP over stdin/stdout).
/// </summary>
/// <remarks>
/// 工具目录（全部只读 / 模拟，绝不触真实资金）：
///   list_strategies / run_backtest / run_paper_cycle / fetch_ohlcv
///   （完整说明见 docs/manual/mcp-server-en.md / mcp-server-ch.md）
/// </remarks>
public static class QuantInfraNetMcpServer
{
    /// <summary>
    /// 构建 stdio MCP server 宿主（官方推荐写法：AddMcpServer + WithStdioServerTransport + WithToolsFromAssembly）。
    /// Builds the stdio MCP server host (official pattern: AddMcpServer + WithStdioServerTransport + WithToolsFromAssembly).
    /// </summary>
    /// <param name="services">额外服务描述符（如自定义 ITraditionalFinanceSourceDataService）/
    /// Additional service descriptors (e.g., a custom ITraditionalFinanceSourceDataService).</param>
    /// <returns>构建好的 IHost（RunAsync 后以 stdio 协议服务）/ A built IHost serving the MCP protocol over stdio once RunAsync starts.</returns>
    public static IHost BuildServer(params ServiceDescriptor[] services)
    {
        var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
        builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(typeof(QuantInfraNetMcpServer).Assembly);

        foreach (var descriptor in services)
        {
            builder.Services.Add(descriptor);
        }

        return builder.Build();
    }

    /// <summary>
    /// 主入口：启动 stdio MCP server，阻塞直到客户端断开或进程被终止。
    /// Main entry: starts the stdio MCP server and blocks until the client disconnects or the process exits.
    /// </summary>
    public static async Task<int> MainAsync()
    {
        // 诊断信息必须走 stderr —— stdout 是 MCP JSON-RPC 通道，任何杂散输出都会破坏协议。
        // Diagnostics MUST go to stderr — stdout is the MCP JSON-RPC channel; stray output breaks the protocol.
        Console.Error.WriteLine("[quant.infra.net] MCP server (stdio) ready — waiting for client on stdin...");

        await BuildServer().RunAsync().ConfigureAwait(false);
        return 0;
    }
}
