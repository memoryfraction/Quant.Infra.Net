using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Orchestration.Signals;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.Shared.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quant.Infra.Net.Orchestration.Tests;

/// <summary>
/// 测试数据基类：确定性手工序列（无网络、无随机）。
/// Test data base: deterministic hand-built series (no network, no randomness).
/// </summary>
internal static class TestSeries
{
    /// <summary>
    /// 构建合成 Ohlcvs（Daily，UTC，升序）。
    /// Builds a synthetic Ohlcvs (Daily, UTC, ascending).
    /// </summary>
    /// <param name="symbol">标的 / Symbol.</param>
    /// <param name="closes">收盘价序列 / Closes.</param>
    /// <returns>Ohlcvs 实例 / Ohlcvs instance.</returns>
    public static Ohlcvs Build(string symbol, double[] closes)
    {
        var baseDt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var set = new HashSet<Ohlcv>();
        for (var i = 0; i < closes.Length; i++)
        {
            var c = (decimal)closes[i];
            set.Add(new Ohlcv
            {
                Symbol = symbol,
                OpenDateTime = baseDt.AddDays(i),
                CloseDateTime = baseDt.AddDays(i + 1),
                Open = c,
                High = c,
                Low = c,
                Close = c,
                Volume = 1m
            });
        }

        return new Ohlcvs
        {
            Symbol = symbol,
            ResolutionLevel = ResolutionLevel.Daily,
            StartDateTimeUtc = baseDt,
            EndDateTimeUtc = baseDt.AddDays(closes.Length),
            OhlcvSet = set
        };
    }

    /// <summary>
    /// 将多组 (symbol, closes) 合并进 context 的 HashSet 槽（多标的缓存约定）。
    /// Merges multiple (symbol, closes) pairs into the context's merged HashSet slot (multi-symbol cache convention).
    /// </summary>
    /// <param name="ctx">上下文 / Context.</param>
    /// <param name="series">symbol 到收盘价序列 / Symbol to closes.</param>
    public static void SetUnion(PipelineContext ctx, params (string Symbol, double[] Closes)[] series)
    {
        var union = new HashSet<Ohlcv>();
        foreach (var (sym, closes) in series)
        {
            union.UnionWith(Build(sym, closes).OhlcvSet);
        }

        ctx.Set(union);
    }

    /// <summary>
    /// 确定性正弦基序列（近似平稳）。
    /// Deterministic sinusoidal series (approximately stationary).
    /// </summary>
    /// <param name="n">长度 / Length.</param>
    /// <param name="drift">每步漂移 / Per-step drift.</param>
    /// <returns>序列 / Series.</returns>
    public static double[] Wave(int n, double drift = 0.0)
    {
        var result = new double[n];
        for (var i = 0; i < n; i++)
        {
            result[i] = 100.0 + 10.0 * Math.Sin(i / 6.0) + 0.1 * Math.Sin(i / 1.7) + drift * i;
        }

        return result;
    }

    /// <summary>
    /// 确定性"随机游走"（sin 交替步，近似平稳有界）。
    /// Deterministic pseudo-random walk (sin alternating steps, bounded and near-stationary).
    /// </summary>
    /// <param name="n">长度 / Length.</param>
    /// <param name="step">单步幅度 / Step size.</param>
    /// <returns>序列 / Series.</returns>
    public static double[] RandomWalk(int n, double step = 2.0)
    {
        var result = new double[n];
        result[0] = 100.0;
        for (var i = 1; i < n; i++)
        {
            result[i] = result[i - 1] + (Math.Sin(i * 0.7) >= 0 ? step : -step);
        }

        return result;
    }

    /// <summary>
    /// 种子 AR(1) 平稳序列（rho&lt;1，均值回复；.NET Core 固定种子算法稳定）。
    /// Seeded AR(1) stationary series (rho &lt; 1, mean-reverting; .NET Core fixed-seed algorithm is stable).
    /// </summary>
    /// <param name="n">长度 / Length.</param>
    /// <param name="rho">自回归系数 / AR coefficient.</param>
    /// <param name="seed">随机种子 / Random seed.</param>
    /// <returns>序列 / Series.</returns>
    public static double[] Ar1(int n, double rho, double seed = 42d)
    {
        var rnd = new Random((int)seed);
        var result = new double[n];
        var prev = 0.0;
        for (var i = 0; i < n; i++)
        {
            prev = rho * prev + (rnd.NextDouble() - 0.5) * 2.0;
            result[i] = prev;
        }

        return result;
    }

    /// <summary>
    /// 种子白噪声随机游走（单位根；.NET Core 固定种子算法稳定）。
    /// Seeded white-noise random walk (unit root; .NET Core fixed-seed algorithm is stable).
    /// </summary>
    /// <param name="n">长度 / Length.</param>
    /// <param name="step">单步幅度 / Step size.</param>
    /// <param name="seed">随机种子 / Random seed.</param>
    /// <returns>序列 / Series.</returns>
    public static double[] RandWalkIid(int n, double step = 1.0, double seed = 7d)
    {
        var rnd = new Random((int)seed);
        var result = new double[n];
        result[0] = 0.0;
        for (var i = 1; i < n; i++)
        {
            result[i] = result[i - 1] + (rnd.NextDouble() - 0.5) * step * 2.0;
        }

        return result;
    }
}

/// <summary>
/// PairTradingZScoreSignalGenerator 单元测试（设计 §6 M2 测试矩阵）。
/// PairTradingZScoreSignalGenerator unit tests (design §6 M2 test matrix).
/// </summary>
[TestClass]
public class PairTradingZScoreSignalGeneratorTests
{
    private static PairTradingZScoreSignalGenerator NewGen() => new(new AnalysisService());

    /// <summary>
    /// 数据不足（任一标的 &lt; 50 根）→ 空集 + 事件。
    /// Insufficient data (either symbol &lt; 50 bars) → empty set + event.
    /// </summary>
    [TestMethod]
    public async Task InsufficientData_ReturnsEmptyAndLogsEvent()
    {
        var gen = NewGen();
        var ctx = new PipelineContext(100, new Dictionary<string, string> { ["SymbolA"] = "AAA", ["SymbolB"] = "BBB" });
        TestSeries.SetUnion(ctx, ("AAA", TestSeries.Wave(20)), ("BBB", TestSeries.Wave(20)));

        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(0, signals.Count);
        Assert.IsTrue(ctx.Events.Any(e => e.Message.Contains("insufficient data")));
    }

    /// <summary>
    /// 高相关 + 平稳价差（B = 0.8A + 10 + 种子 AR(1)，末点显著上偏）→ 产出方向相反的信号对。
    /// High correlation + stationary spread (B = 0.8A + 10 + seeded AR(1), last point significantly high) → emits opposite-direction signal pair.
    /// </summary>
    [TestMethod]
    public async Task StationaryHighCorrelation_EmitsOppositeDirectionPair()
    {
        const int n = 120;
        var a = TestSeries.Wave(n);
        var s = TestSeries.Ar1(n, 0.5, seed: 42);
        s[n - 1] = 3.0; // 末点显著偏正 → lastZ 约 +3 ≥ 1.5 → A=Short / B=Long
        var b = a.Zip(s, (x, si) => 0.8 * x + 10.0 + si).ToArray();

        var gen = NewGen();
        var ctx = new PipelineContext(100, new Dictionary<string, string>
        {
            ["SymbolA"] = "AAA",
            ["SymbolB"] = "BBB",
            ["UseAdfFilter"] = "true",
            ["MinCorrelation"] = "0.8"
        });
        TestSeries.SetUnion(ctx, ("AAA", a), ("BBB", b));

        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(2, signals.Count);
        Assert.AreEqual("AAA", signals[0].Symbol);
        Assert.AreEqual("BBB", signals[1].Symbol);
        Assert.AreNotEqual(signals[0].Direction, signals[1].Direction);
        Assert.AreNotEqual(SignalDirection.Flat, signals[0].Direction);
        Assert.IsTrue(signals[0].Strength > 1.5);
        StringAssert.Contains(signals[0].Reason, "corr=");
        StringAssert.Contains(signals[0].Reason, "lastZ=");
    }

    /// <summary>
    /// 低相关 → 空集 + 相关性拒绝事件。
    /// Low correlation → empty set + correlation rejection event.
    /// </summary>
    [TestMethod]
    public async Task LowCorrelation_ReturnsEmptyWithCorrelationEvent()
    {
        const int n = 120;
        var a = TestSeries.Wave(n);
        var b = a.Select((_, i) => (i % 2 == 0 ? 200.0 : -200.0)).ToArray();

        var gen = NewGen();
        var ctx = new PipelineContext(100, new Dictionary<string, string>
        {
            ["SymbolA"] = "AAA",
            ["SymbolB"] = "BBB",
            ["MinCorrelation"] = "0.8"
        });
        TestSeries.SetUnion(ctx, ("AAA", a), ("BBB", b));

        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(0, signals.Count);
        Assert.IsTrue(ctx.Events.Any(e => e.Message.Contains("MinCorrelation") || e.Message.Contains("corr")));
    }

    /// <summary>
    /// UseAdfFilter=true 且价差为种子白噪声随机游走（单位根）→ 空集 + ADF 拒绝事件。
    /// UseAdfFilter=true with a seeded white-noise random-walk spread (unit root) → empty set + ADF rejection event.
    /// </summary>
    [TestMethod]
    public async Task NonStationarySpread_AdfFilterRejects()
    {
        const int n = 120;
        var a = TestSeries.RandWalkIid(n, 1.0, seed: 101).Select(x => x + 100.0).ToArray();
        var w = TestSeries.RandWalkIid(n, 1.0, seed: 202);
        var b = a.Zip(w, (x, wi) => x + wi).ToArray();

        var gen = NewGen();
        var ctx = new PipelineContext(100, new Dictionary<string, string>
        {
            ["SymbolA"] = "AAA",
            ["SymbolB"] = "BBB",
            ["UseAdfFilter"] = "true",
            ["MinCorrelation"] = "0.5"
        });
        TestSeries.SetUnion(ctx, ("AAA", a), ("BBB", b));

        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(0, signals.Count, "ADF should reject a random-walk spread");
        Assert.IsTrue(ctx.Events.Any(e => e.Message.Contains("ADF")), "expected an ADF rejection event");
    }

    /// <summary>
    /// UseAdfFilter=false 时同一组数据不再被 ADF 拦截（仅相关性门生效）。
    /// UseAdfFilter=false on the same data: ADF no longer blocks (correlation gate only).
    /// </summary>
    [TestMethod]
    public async Task UseAdfFilterFalse_SkipsAdfGate()
    {
        const int n = 120;
        var a = TestSeries.RandWalkIid(n, 1.0, seed: 101).Select(x => x + 100.0).ToArray();
        var w = TestSeries.RandWalkIid(n, 1.0, seed: 202);
        var b = a.Zip(w, (x, wi) => x + wi).ToArray();

        var gen = NewGen();
        var ctx = new PipelineContext(100, new Dictionary<string, string>
        {
            ["SymbolA"] = "AAA",
            ["SymbolB"] = "BBB",
            ["UseAdfFilter"] = "false",
            ["MinCorrelation"] = "0.5"
        });
        TestSeries.SetUnion(ctx, ("AAA", a), ("BBB", b));

        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);

        Assert.AreEqual(2, signals.Count, "with ADF disabled the pair must produce a signal pair");
        StringAssert.Contains(signals[0].Reason, "ADF=skipped");
    }

    /// <summary>
    /// 缺 SymbolA/SymbolB 参数 → 空集 + 拒绝事件。
    /// Missing SymbolA/SymbolB parameters → empty set + rejection event.
    /// </summary>
    [TestMethod]
    public async Task MissingSymbols_ReturnsEmpty()
    {
        var gen = NewGen();
        var ctx = new PipelineContext(100);
        var signals = await gen.GenerateSignalsAsync(ctx, CancellationToken.None);
        Assert.AreEqual(0, signals.Count);
        Assert.IsTrue(ctx.Events.Any(e => e.Message.Contains("missing SymbolA/SymbolB")));
    }

    /// <summary>
    /// null 上下文 → ArgumentNullException。
    /// Null context → ArgumentNullException.
    /// </summary>
    [TestMethod]
    public void NullContext_Throws()
    {
        var gen = NewGen();
        Assert.ThrowsException<ArgumentNullException>(() => gen.GenerateSignalsAsync(null!, CancellationToken.None).GetAwaiter().GetResult());
    }
}
