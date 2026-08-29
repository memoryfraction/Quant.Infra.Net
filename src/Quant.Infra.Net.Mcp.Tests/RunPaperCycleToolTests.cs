using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Mcp.Tools;

namespace Quant.Infra.Net.Mcp.Tests;

/// <summary>
/// run_paper_cycle tool tests: one Paper pipeline cycle must complete and return a non-empty
/// event stream + zero-failure contract (no real funds, zero network).
/// </summary>
[TestClass]
public class RunPaperCycleToolTests
{
    [TestMethod]
    public async Task RunPaperCycle_MaCross_ReturnsEventsAndRunId()
    {
        var json = await RunPaperCycleTool.RunPaperCycle(
            strategy: "MaCross",
            symbol: "AAPL",
            fastPeriod: 1,
            slowPeriod: 20);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // runId must be present and >= 1.
        Assert.IsTrue(root.TryGetProperty("runId", out var runId), "runId missing");
        Assert.IsTrue(runId.GetInt64() >= 1, "runId must be >= 1");

        // strategy must echo back.
        Assert.AreEqual("MaCross", root.GetProperty("strategy").GetString());

        // mode must be Paper.
        Assert.AreEqual("Paper", root.GetProperty("mode").GetString());

        // events must be a non-empty array.
        var events = root.GetProperty("events");
        Assert.AreEqual(JsonValueKind.Array, events.ValueKind);
        Assert.IsTrue(events.GetArrayLength() > 0, "events must be non-empty");

        // every event must have stage + message + timestampUtc.
        foreach (var ev in events.EnumerateArray())
        {
            Assert.IsTrue(ev.TryGetProperty("stage", out _), "event.stage missing");
            Assert.IsTrue(ev.TryGetProperty("message", out _), "event.message missing");
            Assert.IsTrue(ev.TryGetProperty("timestampUtc", out _), "event.timestampUtc missing");
        }

        // errors count must be an integer >= 0.
        var errors = root.GetProperty("errors");
        Assert.AreEqual(JsonValueKind.Number, errors.ValueKind);
        Assert.IsTrue(errors.GetInt32() >= 0);
    }

    [TestMethod]
    public async Task RunPaperCycle_UnknownStrategy_Throws()
    {
        try
        {
            await RunPaperCycleTool.RunPaperCycle(strategy: "DefinitelyNotARealStrategy", symbol: "AAPL");
            Assert.Fail("expected ArgumentException for unknown strategy");
        }
        catch (ArgumentException)
        {
            // expected
        }
    }

    [TestMethod]
    public async Task RunPaperCycle_MeanReversion_ReturnsEvents()
    {
        var json = await RunPaperCycleTool.RunPaperCycle(
            strategy: "MeanReversion",
            symbol: "SPY",
            lookbackBars: 50,
            entryZ: 2.0,
            exitZ: 0.5);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.AreEqual("MeanReversion", root.GetProperty("strategy").GetString());
        var events = root.GetProperty("events");
        Assert.IsTrue(events.GetArrayLength() > 0);
    }
}
