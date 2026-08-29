using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Orchestration.Signals;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Backtest.Tests;

/// <summary>
/// B1：未来函数（look-ahead bias）防线——SliceUpTo 在任何模拟时刻都不得越过该时刻，
/// 且真实信号生成器（MaCross）在早期时刻产生的信号不受未来异常 bar 影响。
/// B1: look-ahead-bias guard — SliceUpTo never crosses the simulated instant, and the real
/// MaCross generator's signal at an early instant is unaffected by a future outlier bar.
/// </summary>
[TestClass]
public sealed class LookAheadBiasTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private const int BarCount = 12;
    private const int SpikeIndex = BarCount - 1;
    private static readonly decimal SpikeClose = 9999m;

    /// <summary>
    /// 12 根温和上涨的 K 线，最后一根植入极端异常收盘（9999）。
    /// 12 gently rising candles with an extreme outlier close (9999) planted on the last bar.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<Ohlcv>> BuildSeries()
    {
        var bars = new List<Ohlcv>();
        for (var i = 0; i < BarCount; i++)
        {
            bars.Add(TestBars.Bar("AAA", Start.AddHours(i), i == SpikeIndex ? SpikeClose : 100m + i));
        }

        return new Dictionary<string, IReadOnlyList<Ohlcv>> { ["AAA"] = bars };
    }

    [TestMethod]
    public void SliceUpTo_AtEveryInstantBeforeSpike_NeverExposesSpikeBar()
    {
        var set = new HistoricalDataSet(BuildSeries());

        for (var i = 0; i < SpikeIndex; i++)
        {
            var slice = set.SliceUpTo("AAA", Start.AddHours(i));

            Assert.IsTrue(
                slice.OhlcvSet.All(static b => b.Close != SpikeClose),
                "SliceUpTo leaked the planted outlier bar at an earlier instant.");
            Assert.AreEqual(i + 1, slice.OhlcvSet.Count);
        }
    }

    /// <summary>
    /// 早期时刻（t5）的 MaCross 信号必须与"异常 bar 从不存在"的数据集产生的信号完全一致；
    /// 并给出反向敏感例（未来数据若被泄漏，信号必然变化），证明断言不是空转。
    /// The MaCross signal at t5 must equal the one produced from a data set where the outlier
    /// bar never exists; a sensitivity control (leaked future data → signal MUST differ) proves
    /// the assertion is not vacuous.
    /// </summary>
    [TestMethod]
    public async Task MaCrossSignal_AtEarlyInstant_IsIdenticalForAsOfDataAndOutlierFreeData()
    {
        var full = new HistoricalDataSet(BuildSeries());
        var clean = new HistoricalDataSet(
            new Dictionary<string, IReadOnlyList<Ohlcv>>
            {
                ["AAA"] = BuildSeries()["AAA"].Take(SpikeIndex).ToList(),
            });

        // 早期时刻 / early instant
        var asOf = Start.AddHours(5);

        var asOfSignal = await RunMaCrossAsync(SeedWith(full, asOf));
        var cleanSignal = await RunMaCrossAsync(SeedWith(clean, asOf));
        var leakedSignal = await RunMaCrossAsync(SeedWith(full, Start.AddHours(SpikeIndex)));

        // 核心断言：早期信号 == 无异常数据的信号（未来值零影响）/ core assertion: zero future influence
        Assert.AreEqual(cleanSignal.Direction, asOfSignal.Direction);
        Assert.AreEqual(cleanSignal.Strength, asOfSignal.Strength, 1e-12);
        Assert.IsNotNull(asOfSignal.Reason);

        // 敏感例：若"未来"数据被泄漏进早期上下文，信号必然不同 / control: leaked future data changes the signal
        Assert.IsTrue(
            leakedSignal.Direction != cleanSignal.Direction || Math.Abs(leakedSignal.Strength - cleanSignal.Strength) > 1e-9,
            "Leaking the outlier bar must change the signal; if this fails the guard is untestable.");
    }

    private static Action<PipelineContext> SeedWith(HistoricalDataSet set, DateTime asOf) =>
        ctx => ctx.Set<Ohlcvs>(set.SliceUpTo("AAA", asOf));

    private static async Task<Signal> RunMaCrossAsync(Action<PipelineContext> seed)
    {
        var context = new PipelineContext(1, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Symbol"] = "AAA",
            ["FastPeriod"] = "1",
            ["SlowPeriod"] = "5",
        });
        seed(context);

        var generator = new MaCrossSignalGenerator(new AnalysisService());
        var signals = await generator.GenerateSignalsAsync(context, CancellationToken.None);

        Assert.AreEqual(1, signals.Count, "MaCross must emit exactly one signal for sufficient data.");
        return signals[0];
    }
}
