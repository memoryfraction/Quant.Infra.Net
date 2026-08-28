using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Orchestration.Signals;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quant.Infra.Net.Orchestration.Tests;

/// <summary>
/// MeanReversionSignalGenerator 单元测试（EntryZ/ExitZ/AllowShort + 中性区 + 退化窗口）。
/// MeanReversionSignalGenerator unit tests (EntryZ/ExitZ/AllowShort + neutral zone + degenerate window).
/// </summary>
[TestClass]
public class MeanReversionSignalGeneratorTests
{
    private static MeanReversionSignalGenerator NewGen() => new(new AnalysisService());

    private static PipelineContext NewContext(Dictionary<string, string> parameters)
        => new(300, parameters);

    /// <summary>
    /// 末值显著低于均值（z ≤ −EntryZ）→ Long。
    /// Last value significantly below the mean (z ≤ −EntryZ) → Long.
    /// </summary>
    [TestMethod]
    public async Task LastFarBelowMean_ReturnsLong()
    {
        var closes = Enumerable.Repeat(100.0, 99).Concat(new double[] { 90.0 }).ToArray();
        var ctx = NewContext(new Dictionary<string, string>
        {
            ["Symbol"] = "AAA",
            ["LookbackBars"] = "100",
            ["EntryZ"] = "2.0",
            ["ExitZ"] = "0.5",
            ["AllowShort"] = "true"
        });
        TestSeries.SetUnion(ctx, ("AAA", closes));

        var gen = NewGen();
        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(1, signals.Count);
        Assert.AreEqual(SignalDirection.Long, signals[0].Direction);
        Assert.IsTrue(signals[0].Strength > 2.0);
        StringAssert.Contains(signals[0].Reason, "z=");
    }

    /// <summary>
    /// 末值显著高于均值 + AllowShort=true → Short。
    /// Last value significantly above the mean + AllowShort=true → Short.
    /// </summary>
    [TestMethod]
    public async Task LastFarAboveMean_AllowShortTrue_ReturnsShort()
    {
        var closes = Enumerable.Repeat(100.0, 99).Concat(new double[] { 110.0 }).ToArray();
        var ctx = NewContext(new Dictionary<string, string>
        {
            ["Symbol"] = "AAA",
            ["LookbackBars"] = "100",
            ["EntryZ"] = "2.0",
            ["ExitZ"] = "0.5",
            ["AllowShort"] = "true"
        });
        TestSeries.SetUnion(ctx, ("AAA", closes));

        var gen = NewGen();
        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(1, signals.Count);
        Assert.AreEqual(SignalDirection.Short, signals[0].Direction);
    }

    /// <summary>
    /// 末值显著高于均值 + AllowShort=false → Flat。
    /// Last value significantly above the mean + AllowShort=false → Flat.
    /// </summary>
    [TestMethod]
    public async Task LastFarAboveMean_AllowShortFalse_ReturnsFlat()
    {
        var closes = Enumerable.Repeat(100.0, 99).Concat(new double[] { 110.0 }).ToArray();
        var ctx = NewContext(new Dictionary<string, string>
        {
            ["Symbol"] = "AAA",
            ["LookbackBars"] = "100",
            ["EntryZ"] = "2.0",
            ["ExitZ"] = "0.5",
            ["AllowShort"] = "false"
        });
        TestSeries.SetUnion(ctx, ("AAA", closes));

        var gen = NewGen();
        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(1, signals.Count);
        Assert.AreEqual(SignalDirection.Flat, signals[0].Direction);
    }

    /// <summary>
    /// 完全平坦窗口（std=0）→ Flat 平仓（退化情形）。
    /// Fully flat window (std=0) → Flat exit (degenerate case).
    /// </summary>
    [TestMethod]
    public async Task FlatWindow_ReturnsFlat()
    {
        var closes = Enumerable.Repeat(100.0, 100).ToArray();
        var ctx = NewContext(new Dictionary<string, string>
        {
            ["Symbol"] = "AAA",
            ["LookbackBars"] = "100"
        });
        TestSeries.SetUnion(ctx, ("AAA", closes));

        var gen = NewGen();
        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(1, signals.Count);
        Assert.AreEqual(SignalDirection.Flat, signals[0].Direction);
        StringAssert.Contains(signals[0].Reason, "std=0");
    }

    /// <summary>
    /// 中性区（ExitZ &lt; |z| &lt; EntryZ）→ 空集 + "中性区"事件。
    /// Neutral zone (ExitZ &lt; |z| &lt; EntryZ) → empty set + "neutral zone" event.
    /// </summary>
    [TestMethod]
    public async Task NeutralZone_ReturnsEmptyWithEvent()
    {
        // 50 根 100.0 + 50 根 101.0：mean=100.5，std=0.5，last z = 1.0 ∈ (0.5, 2.0)
        // 50 bars at 100.0 + 50 bars at 101.0: mean=100.5, std=0.5, last z = 1.0 ∈ (0.5, 2.0)
        var closes = Enumerable.Repeat(100.0, 50).Concat(Enumerable.Repeat(101.0, 50)).ToArray();
        var ctx = NewContext(new Dictionary<string, string>
        {
            ["Symbol"] = "AAA",
            ["LookbackBars"] = "100",
            ["EntryZ"] = "2.0",
            ["ExitZ"] = "0.5"
        });
        TestSeries.SetUnion(ctx, ("AAA", closes));

        var gen = NewGen();
        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(0, signals.Count);
        Assert.IsTrue(ctx.Events.Any(e => e.Message.Contains("neutral zone")), "expected a neutral-zone event");
    }

    /// <summary>
    /// 数据不足（&lt; max(10, LookbackBars/10)）→ 空集 + 事件。
    /// Insufficient data (&lt; max(10, LookbackBars/10)) → empty set + event.
    /// </summary>
    [TestMethod]
    public async Task InsufficientData_ReturnsEmpty()
    {
        var closes = TestSeries.Wave(8);
        var ctx = NewContext(new Dictionary<string, string>
        {
            ["Symbol"] = "AAA",
            ["LookbackBars"] = "100"
        });
        TestSeries.SetUnion(ctx, ("AAA", closes));

        var gen = NewGen();
        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(0, signals.Count);
        Assert.IsTrue(ctx.Events.Any(e => e.Message.Contains("insufficient data")));
    }

    /// <summary>
    /// 缺 Symbol 参数 → 空集 + 拒绝事件。
    /// Missing Symbol parameter → empty set + rejection event.
    /// </summary>
    [TestMethod]
    public async Task MissingSymbol_ReturnsEmpty()
    {
        var gen = NewGen();
        var ctx = NewContext(new Dictionary<string, string>());
        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);
        Assert.AreEqual(0, signals.Count);
        Assert.IsTrue(ctx.Events.Any(e => e.Message.Contains("missing Symbol")));
    }

    /// <summary>
    /// Strength = |z|（Long 场景）。
    /// Strength equals |z| in the Long scenario.
    /// </summary>
    [TestMethod]
    public async Task LongCase_StrengthMatchesAbsZ()
    {
        var closes = Enumerable.Repeat(100.0, 99).Concat(new double[] { 90.0 }).ToArray();
        var ctx = NewContext(new Dictionary<string, string>
        {
            ["Symbol"] = "AAA",
            ["LookbackBars"] = "100",
            ["EntryZ"] = "2.0",
            ["ExitZ"] = "0.5"
        });
        TestSeries.SetUnion(ctx, ("AAA", closes));

        var gen = NewGen();
        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        var window = closes.Take(100).ToList();
        var mean = window.Average();
        var std = OrchestrationNumerics.PopulationStdDev(window);
        var expectedAbsZ = Math.Abs((90.0 - mean) / std);
        Assert.AreEqual(expectedAbsZ, signals[0].Strength, 1e-9);
    }
}
