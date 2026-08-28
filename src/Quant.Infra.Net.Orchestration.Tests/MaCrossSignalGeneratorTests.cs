using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Orchestration.Signals;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quant.Infra.Net.Orchestration.Tests;

/// <summary>
/// MaCrossSignalGenerator 单元测试（含经典 200MA 用例）。
/// MaCrossSignalGenerator unit tests (including the mandatory classic 200MA case).
/// </summary>
[TestClass]
public class MaCrossSignalGeneratorTests
{
    private static MaCrossSignalGenerator NewGen() => new(new AnalysisService());

    private static PipelineContext NewContext(Dictionary<string, string> parameters)
        => new(200, parameters);

    /// <summary>
    /// 单调上涨 + 经典 200MA（FastPeriod=1）→ Long。
    /// Monotonic uptrend + classic 200MA (FastPeriod=1) → Long.
    /// </summary>
    [TestMethod]
    public async Task Uptrend_Classic200MA_ReturnsLong()
    {
        var closes = Array.Empty<double>().Concat(Enumerable.Range(1, 250).Select(i => (double)i)).ToArray();
        var ctx = NewContext(new Dictionary<string, string>
        {
            ["Symbol"] = "AAA",
            ["FastPeriod"] = "1",
            ["SlowPeriod"] = "200",
            ["AllowShort"] = "false"
        });
        TestSeries.SetUnion(ctx, ("AAA", closes));

        var gen = NewGen();
        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(1, signals.Count);
        Assert.AreEqual(SignalDirection.Long, signals[0].Direction);
        Assert.AreEqual("AAA", signals[0].Symbol);
        StringAssert.Contains(signals[0].Reason, "slow(200)=");
    }

    /// <summary>
    /// 单调下跌 + AllowShort=false → Flat。
    /// Monotonic downtrend + AllowShort=false → Flat.
    /// </summary>
    [TestMethod]
    public async Task Downtrend_AllowShortFalse_ReturnsFlat()
    {
        var closes = Enumerable.Range(1, 250).Select(i => (double)(251 - i)).ToArray();
        var ctx = NewContext(new Dictionary<string, string>
        {
            ["Symbol"] = "AAA",
            ["FastPeriod"] = "1",
            ["SlowPeriod"] = "200",
            ["AllowShort"] = "false"
        });
        TestSeries.SetUnion(ctx, ("AAA", closes));

        var gen = NewGen();
        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(1, signals.Count);
        Assert.AreEqual(SignalDirection.Flat, signals[0].Direction);
    }

    /// <summary>
    /// 单调下跌 + AllowShort=true → Short。
    /// Monotonic downtrend + AllowShort=true → Short.
    /// </summary>
    [TestMethod]
    public async Task Downtrend_AllowShortTrue_ReturnsShort()
    {
        var closes = Enumerable.Range(1, 250).Select(i => (double)(251 - i)).ToArray();
        var ctx = NewContext(new Dictionary<string, string>
        {
            ["Symbol"] = "AAA",
            ["FastPeriod"] = "1",
            ["SlowPeriod"] = "200",
            ["AllowShort"] = "true"
        });
        TestSeries.SetUnion(ctx, ("AAA", closes));

        var gen = NewGen();
        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(1, signals.Count);
        Assert.AreEqual(SignalDirection.Short, signals[0].Direction);
    }

    /// <summary>
    /// 双均线（Fast=5 / Slow=20）：末段加速上涨 → Long；末段加速下跌 → Short。
    /// Dual MA (Fast=5 / Slow=20): late acceleration up → Long; down → Short.
    /// </summary>
    [TestMethod]
    public async Task DualMa_AccelerationUp_ReturnsLong()
    {
        var closes = TestSeries.Wave(60, drift: 0.0).Concat(Enumerable.Range(1, 10).Select(i => 100.0 + i * 5.0)).ToArray();
        var ctx = NewContext(new Dictionary<string, string>
        {
            ["Symbol"] = "AAA",
            ["FastPeriod"] = "5",
            ["SlowPeriod"] = "20",
            ["AllowShort"] = "true"
        });
        TestSeries.SetUnion(ctx, ("AAA", closes));

        var gen = NewGen();
        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(1, signals.Count);
        Assert.AreEqual(SignalDirection.Long, signals[0].Direction);
    }

    /// <summary>
    /// 数据不足（&lt; SlowPeriod + 1）→ 空集 + 事件。
    /// Insufficient data (&lt; SlowPeriod + 1) → empty set + event.
    /// </summary>
    [TestMethod]
    public async Task InsufficientData_ReturnsEmpty()
    {
        var closes = TestSeries.Wave(150);
        var ctx = NewContext(new Dictionary<string, string>
        {
            ["Symbol"] = "AAA",
            ["SlowPeriod"] = "200"
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
    /// Strength = |fast − slow| / |slow| 且 &gt; 0（上涨场景）。
    /// Strength equals |fast - slow| / |slow| and is positive in an uptrend.
    /// </summary>
    [TestMethod]
    public async Task Uptrend_StrengthIsPositiveFraction()
    {
        var closes = Enumerable.Range(1, 250).Select(i => (double)i).ToArray();
        var ctx = NewContext(new Dictionary<string, string> { ["Symbol"] = "AAA" }); // 使用默认 Fast=1 / Slow=200
        TestSeries.SetUnion(ctx, ("AAA", closes));

        var gen = NewGen();
        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(1, signals.Count);
        var expectedSlow = Enumerable.Range(51, 200).Average();
        var expectedSpeed = Math.Abs(250.0 - expectedSlow) / expectedSlow;
        Assert.IsTrue(signals[0].Strength > 0);
        Assert.AreEqual(expectedSpeed, signals[0].Strength, 1e-9);
    }
}
