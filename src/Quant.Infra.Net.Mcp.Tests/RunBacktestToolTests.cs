using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;
using Quant.Infra.Net.Mcp.Tests.Stubs;

namespace Quant.Infra.Net.Mcp.Tests;

/// <summary>
/// P2：run_backtest 工具端到端测试（Demo 离线数据源，零网络）。
/// P2: end-to-end tests for the run_backtest tool (Demo offline data source, zero network).
/// </summary>
[TestClass]
public sealed class RunBacktestToolTests
{
    /// <summary>
    /// MaCross + Demo：能跑通，返回 JSON 含全部指标字段（profitFactor 可能为 null = Infinity）。
    /// MaCross + Demo: completes and returns JSON with every metrics field present
    /// (profitFactor may be null when Infinity = no losing trades).
    /// </summary>
    [TestMethod]
    public async Task MaCross_Demo_ReturnsCompleteMetricsJson()
    {
        var json = await Tools.RunBacktestTool.RunBacktest(
            strategy: "MaCross",
            symbol: "AAPL",
            startDate: "2024-01-01",
            endDate: "2024-06-30",
            fastPeriod: 1,
            slowPeriod: 50);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.AreEqual("MaCross", root.GetProperty("strategy").GetString());
        Assert.AreEqual(1, root.GetProperty("symbols").GetArrayLength());
        var metrics = root.GetProperty("metrics");
        Assert.IsTrue(metrics.TryGetProperty("cagrPct", out _), "metrics.cagrPct must exist");
        Assert.IsTrue(metrics.TryGetProperty("sharpe", out _), "metrics.sharpe must exist");
        Assert.IsTrue(metrics.TryGetProperty("calmar", out _), "metrics.calmar must exist");
        Assert.IsTrue(metrics.TryGetProperty("maxDrawdownPct", out _), "metrics.maxDrawdownPct must exist");
        Assert.IsTrue(metrics.TryGetProperty("winRatePct", out _), "metrics.winRatePct must exist");
        // profitFactor may be present (finite) or absent (null = Infinity, no losing trades)
        Assert.IsTrue(metrics.TryGetProperty("profitFactor", out var pf) ||
                      metrics.TryGetProperty("totalTrades", out _), "profitFactor or totalTrades must exist");
        Assert.IsTrue(metrics.TryGetProperty("totalTrades", out _), "metrics.totalTrades must exist");
        Assert.IsTrue(metrics.TryGetProperty("totalCommissionUsd", out _), "metrics.totalCommissionUsd must exist");
        Assert.IsTrue(root.GetProperty("trades").GetInt32() >= 0, "trades count must be >= 0");
        Assert.IsTrue(root.TryGetProperty("interpretation", out _), "interpretation verdict should be present");
    }

    /// <summary>
    /// MeanReversion + Demo：能跑通，返回 JSON。
    /// MeanReversion + Demo: completes and returns JSON.
    /// </summary>
    [TestMethod]
    public async Task MeanReversion_Demo_ReturnsJson()
    {
        var json = await Tools.RunBacktestTool.RunBacktest(
            strategy: "MeanReversion",
            symbol: "AAPL",
            startDate: "2024-01-01",
            endDate: "2024-03-31");

        using var doc = JsonDocument.Parse(json);
        Assert.AreEqual("MeanReversion", doc.RootElement.GetProperty("strategy").GetString());
        Assert.IsTrue(doc.RootElement.TryGetProperty("metrics", out _), "metrics block must exist");
    }

    /// <summary>
    /// 非法日期应抛 ArgumentException / Invalid dates must throw ArgumentException.
    /// </summary>
    [TestMethod]
    public async Task InvalidDate_ThrowsArgumentException()
    {
        try
        {
            await Tools.RunBacktestTool.RunBacktest(
                strategy: "MaCross",
                symbol: "AAPL",
                startDate: "not-a-date",
                endDate: "2024-06-30");
            Assert.Fail("Expected ArgumentException");
        }
        catch (ArgumentException) { }
    }

    /// <summary>
    /// 未提供 symbol 应抛 ArgumentException / Missing symbol must throw ArgumentException.
    /// </summary>
    [TestMethod]
    public async Task MissingSymbol_ThrowsArgumentException()
    {
        try
        {
            await Tools.RunBacktestTool.RunBacktest(
                strategy: "MaCross",
                startDate: "2024-01-01",
                endDate: "2024-06-30");
            Assert.Fail("Expected ArgumentException");
        }
        catch (ArgumentException) { }
    }

    /// <summary>
    /// 非法数据源应抛 ArgumentException / Unsupported data source must throw ArgumentException.
    /// </summary>
    [TestMethod]
    public async Task BadDataSource_ThrowsArgumentException()
    {
        try
        {
            await Tools.RunBacktestTool.RunBacktest(
                strategy: "MaCross",
                symbol: "AAPL",
                startDate: "2024-01-01",
                endDate: "2024-06-30",
                dataSource: "Yahoo");
            Assert.Fail("Expected ArgumentException");
        }
        catch (ArgumentException) { }
    }

    /// <summary>
    /// Stooq 在 MCP 表面被刻意放弃，应抛 ArgumentException /
    /// Stooq is intentionally not exposed on the MCP surface and must throw.
    /// </summary>
    [TestMethod]
    public async Task StooqDataSource_IsRejected()
    {
        try
        {
            await Tools.RunBacktestTool.RunBacktest(
                strategy: "MaCross",
                symbol: "AAPL",
                startDate: "2024-01-01",
                endDate: "2024-06-30",
                dataSource: "Stooq");
            Assert.Fail("Expected ArgumentException for Stooq");
        }
        catch (ArgumentException) { }
    }

    /// <summary>
    /// PairTradingZScore 缺少 legs 应抛 ArgumentException / PairTrading missing legs must throw.
    /// </summary>
    [TestMethod]
    public async Task PairTrading_MissingLegs_ThrowsArgumentException()
    {
        try
        {
            await Tools.RunBacktestTool.RunBacktest(
                strategy: "PairTradingZScore",
                startDate: "2024-01-01",
                endDate: "2024-06-30");
            Assert.Fail("Expected ArgumentException");
        }
        catch (ArgumentException) { }
    }

    /// <summary>
    /// endDate 早于 startDate 应抛 ArgumentException / end before start must throw.
    /// </summary>
    [TestMethod]
    public async Task EndBeforeStart_ThrowsArgumentException()
    {
        try
        {
            await Tools.RunBacktestTool.RunBacktest(
                strategy: "MaCross",
                symbol: "AAPL",
                startDate: "2024-06-30",
                endDate: "2024-01-01");
            Assert.Fail("Expected ArgumentException");
        }
        catch (ArgumentException) { }
    }
}
