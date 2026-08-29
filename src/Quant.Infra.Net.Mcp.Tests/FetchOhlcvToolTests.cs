using System.Globalization;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Mcp.Tools;
using Quant.Infra.Net.Mcp.Tests.Stubs;

namespace Quant.Infra.Net.Mcp.Tests;

/// <summary>
/// fetch_ohlcv tool tests: boundary (500-bar hard cap) + Demo (offline) + error paths.
/// </summary>
[TestClass]
public class FetchOhlcvToolTests
{
    [TestMethod]
    public async Task FetchOhlcv_Demo_ReturnsBars()
    {
        var json = await FetchOhlcvTool.FetchOhlcv(
            symbol: "AAPL",
            startDate: "2024-01-01",
            endDate: "2024-01-10",
            dataSource: "Demo");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.AreEqual("AAPL", root.GetProperty("symbol").GetString());
        Assert.AreEqual("Demo", root.GetProperty("provider").GetString());

        var bars = root.GetProperty("bars");
        Assert.AreEqual(JsonValueKind.Array, bars.ValueKind);
        Assert.IsTrue(bars.GetArrayLength() > 0, "expected > 0 bars");

        // each bar must have the standard OHLCV fields.
        foreach (var b in bars.EnumerateArray())
        {
            Assert.IsTrue(b.TryGetProperty("date", out _));
            Assert.IsTrue(b.TryGetProperty("open", out _));
            Assert.IsTrue(b.TryGetProperty("high", out _));
            Assert.IsTrue(b.TryGetProperty("low", out _));
            Assert.IsTrue(b.TryGetProperty("close", out _));
            Assert.IsTrue(b.TryGetProperty("volume", out _));
        }

        Assert.IsFalse(root.GetProperty("truncated").GetBoolean());
        Assert.AreEqual(10, root.GetProperty("totalBars").GetInt32());
        Assert.AreEqual(10, root.GetProperty("returnedBars").GetInt32());
        Assert.AreEqual(FetchOhlcvTool.MaxBars, root.GetProperty("maxBars").GetInt32());
    }

    [TestMethod]
    public async Task FetchOhlcv_Demo_LargeWindow_ReturnsExactly500()
    {
        // Demo generates 1 bar per day; 2024-01-01..2024-12-31 = 366 bars. That's < 500, so not truncated.
        // To force truncation, use a 3-year window: 2022-01-01..2024-12-31 = ~1096 days > 500.
        var json = await FetchOhlcvTool.FetchOhlcv(
            symbol: "AAPL",
            startDate: "2022-01-01",
            endDate: "2024-12-31",
            dataSource: "Demo");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var bars = root.GetProperty("bars");
        Assert.AreEqual(FetchOhlcvTool.MaxBars, bars.GetArrayLength(), "must be exactly 500 when truncated");
        Assert.IsTrue(root.GetProperty("truncated").GetBoolean(), "truncated must be true");
        Assert.IsTrue(root.GetProperty("totalBars").GetInt32() > 500, "totalBars must be > 500");
        Assert.AreEqual(FetchOhlcvTool.MaxBars, root.GetProperty("returnedBars").GetInt32());
    }

    [TestMethod]
    public async Task FetchOhlcv_UnsupportedProvider_ReturnsErrorJson()
    {
        var json = await FetchOhlcvTool.FetchOhlcv(
            symbol: "AAPL",
            startDate: "2024-01-01",
            endDate: "2024-01-10",
            dataSource: "NotARealProvider");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.IsTrue(root.TryGetProperty("error", out var err), "expected error field");
        Assert.IsTrue(err.GetString()!.Contains("Unsupported dataSource"), "error should mention unsupported provider");
        Assert.AreEqual(JsonValueKind.Array, root.GetProperty("bars").ValueKind);
        Assert.AreEqual(0, root.GetProperty("bars").GetArrayLength());
    }

    [TestMethod]
    public async Task FetchOhlcv_EndBeforeStart_Throws()
    {
        try
        {
            await FetchOhlcvTool.FetchOhlcv(
                symbol: "AAPL",
                startDate: "2024-06-30",
                endDate: "2024-01-01");
            Assert.Fail("expected ArgumentException for end < start");
        }
        catch (ArgumentException)
        {
            // expected
        }
    }
}
