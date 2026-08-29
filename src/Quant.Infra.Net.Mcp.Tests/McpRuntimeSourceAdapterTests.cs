using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Mcp.DataSources;
using Quant.Infra.Net.SourceData.Service;
using Quant.Infra.Net.Mcp.Tests.Stubs;

namespace Quant.Infra.Net.Mcp.Tests;

/// <summary>
/// McpRuntimeSourceAdapter 测试：确认 MCP 数据源能正确适配成运行时的 ITraditionalFinanceSourceDataService，
/// 且 DownloadOhlcvListAsync 委托到底层数据源。
/// Adapter tests: confirm an IMcpSourceDataService adapts into ITraditionalFinanceSourceDataService and
/// DownloadOhlcvListAsync delegates to the underlying source.
/// </summary>
[TestClass]
public sealed class McpRuntimeSourceAdapterTests
{
    [TestMethod]
    public void Adapter_DownloadOhlcvListAsync_DelegatesToInnerSource()
    {
        var inner = new FixedBarSourceDataService(barCount: 7, basePrice: 50.0, provider: "Fake");
        var adapter = new McpRuntimeSourceAdapter(inner);

        var result = adapter.DownloadOhlcvListAsync("AAPL", new DateTime(2024, 1, 1), new DateTime(2024, 1, 31)).GetAwaiter().GetResult();

        Assert.AreEqual("AAPL", result.Symbol);
        Assert.AreEqual(7, result.OhlcvSet.Count, "should return exactly the inner source's bar count");
    }

    [TestMethod]
    public void Adapter_ImplementsITraditionalFinanceSourceDataService()
    {
        var inner = new FixedBarSourceDataService(3);
        var adapter = new McpRuntimeSourceAdapter(inner);
        Assert.IsInstanceOfType(adapter, typeof(ITraditionalFinanceSourceDataService));
    }

    [TestMethod]
    public void Adapter_NullInner_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new McpRuntimeSourceAdapter(null!));
    }
}
