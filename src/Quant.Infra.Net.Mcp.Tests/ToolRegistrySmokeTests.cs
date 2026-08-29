using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Server;
using Tools = Quant.Infra.Net.Mcp.Tools;

namespace Quant.Infra.Net.Mcp.Tests;

/// <summary>
/// P0 冒烟测试：MCP server 程序集里 [McpServerTool] 注解的工具可被反射发现，
/// 且 server 能以 stdio 传输成功构建（不真正启动 I/O）。
/// P0 smoke tests: [McpServerTool]-annotated methods are discoverable by reflection and the server
/// builds cleanly with the stdio transport (no actual I/O is started).
/// </summary>
[TestClass]
public sealed class ToolRegistrySmokeTests
{
    /// <summary>
    /// 至少存在一个带 [McpServerTool] 注解的公共方法（list_strategies）。
    /// At least one public method carries the [McpServerTool] attribute (list_strategies).
    /// </summary>
    [TestMethod]
    public void AtLeastOneMcpServerTool_IsDiscoveredByReflection()
    {
        var assembly = typeof(QuantInfraNetMcpServer).Assembly;
        var tools = assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttributes<McpServerToolAttribute>().Any())
            .ToList();

        Assert.IsTrue(tools.Count >= 1, "expected at least one [McpServerTool] method in the MCP assembly");
    }

    /// <summary>
    /// list_strategies 工具方法存在，直接调用返回包含全部内置策略名的 JSON。
    /// The list_strategies tool method exists and, when invoked, returns JSON naming every built-in.
    /// </summary>
    [TestMethod]
    public void ListStrategiesTool_ReturnsBuiltInCatalog()
    {
        var task = (Task<string>)typeof(Tools.ListStrategiesTool)
            .GetMethod("ListStrategies", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, null)!;
        var json = task.GetAwaiter().GetResult();

        Assert.IsTrue(json.Contains("MaCross"), "catalog JSON should list MaCross");
        Assert.IsTrue(json.Contains("MeanReversion"), "catalog JSON should list MeanReversion");
        Assert.IsTrue(json.Contains("PairTradingZScore"), "catalog JSON should list PairTradingZScore");
    }

    /// <summary>
    /// server 能以 stdio 传输构建（BuildServer 不抛异常）。
    /// The server builds with the stdio transport (BuildServer does not throw).
    /// </summary>
    [TestMethod]
    public void BuildServer_WithStdioTransport_DoesNotThrow()
    {
        var host = QuantInfraNetMcpServer.BuildServer();
        Assert.IsNotNull(host);
    }
}
