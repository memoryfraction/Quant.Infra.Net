using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Runtime.Console.Strategies;

namespace Quant.Infra.Net.Runtime.Tests;

/// <summary>
/// R9 验收：QQQM 逆向 MA200 定投公式的确定性验证（手工构造的已知 close/SMA200，不联网）：
/// ratio = close/SMA200；deviation = 1 − ratio；
/// targetWeight = deviation ≥ 0 ? Base + Add·deviation : Base + Trim·deviation；再 clamp 到 [Min, Max]。
/// 同时覆盖 QqqmReverseDcaStage 在缓存数据槽（SignalDataLoader 装载规则第 1 步）下的端到端 Signal/TargetPosition 产出。
/// R9 acceptance: deterministic checks of the reverse-MA200 DCA formula (hand-built known close/SMA200, no network),
/// plus an end-to-end stage run over the cached data slot (step 1 of SignalDataLoader's rule).
/// </summary>
[TestClass]
public sealed class QqqmReverseDcaStrategyTests
{
    /// <summary>跌破均线（deviation ≥ 0）：0.5 + 1.5 × 0.20 = 0.80。</summary>
    [TestMethod]
    public void ComputeTargetWeight_Below_Ma_Adds_Weight()
    {
        Assert.AreEqual(0.80, QqqmReverseDcaStrategy.ComputeTargetWeight(80, 100, 0.5, 1.5, 1.0, 0.0, 1.0), 1e-12);
    }

    /// <summary>跌破更深（deviation = 0.5）：0.5 + 1.5 × 0.5 = 1.25 → clamp 到 Max=1.0。</summary>
    [TestMethod]
    public void ComputeTargetWeight_Deep_Dip_Clamps_To_Max()
    {
        Assert.AreEqual(1.0, QqqmReverseDcaStrategy.ComputeTargetWeight(50, 100, 0.5, 1.5, 1.0, 0.0, 1.0), 1e-12);
    }

    /// <summary>突破均线（deviation &lt; 0）：0.5 + 1.0 × (−0.25) = 0.25。</summary>
    [TestMethod]
    public void ComputeTargetWeight_Above_Ma_Trims_Weight()
    {
        Assert.AreEqual(0.25, QqqmReverseDcaStrategy.ComputeTargetWeight(125, 100, 0.5, 1.5, 1.0, 0.0, 1.0), 1e-12);
    }

    /// <summary>突破更深：0.5 + 1.0 × (−0.6) = −0.1 → clamp 到 Min=0.0。</summary>
    [TestMethod]
    public void ComputeTargetWeight_Deep_Rally_Clamps_To_Min()
    {
        Assert.AreEqual(0.0, QqqmReverseDcaStrategy.ComputeTargetWeight(160, 100, 0.5, 1.5, 1.0, 0.0, 1.0), 1e-12);
    }

    /// <summary>恰好等于均线（deviation = 0）：恒等于 BaseWeight = 0.5。</summary>
    [TestMethod]
    public void ComputeTargetWeight_At_Ma_Equals_Base()
    {
        Assert.AreEqual(0.5, QqqmReverseDcaStrategy.ComputeTargetWeight(100, 100, 0.5, 1.5, 1.0, 0.0, 1.0), 1e-12);
    }

    /// <summary>
    /// 端到端（不联网）：缓存 200 根收盘价（末根 80，SMA200 = 均值 = 99.9）入 context 后执行 Stage，
    /// 应产出 Long 信号与对应 TargetPosition（Strength/TargetWeight = 0.5 + 1.5 × deviation，OriginSignal 指向该信号）。
    /// End-to-end (offline): 200 cached closes (last = 80, SMA200 = 99.9) yield a Long signal + matching TargetPosition.
    /// </summary>
    [TestMethod]
    public async Task Stage_Cached_Series_Emits_Signal_And_TargetPosition()
    {
        var closes = new List<double>(Enumerable.Repeat(100.0, 199)) { 80.0 }; // SMA200 = (199×100 + 80)/200 = 99.9
        var context = new PipelineContext(1, new Dictionary<string, string>
        {
            ["Symbol"] = "QQQM",
            ["MaPeriod"] = "200",
            ["BaseWeight"] = "0.5",
            ["AddIntensity"] = "1.5",
            ["TrimIntensity"] = "1.0",
            ["MaxWeight"] = "1.0",
            ["MinWeight"] = "0.0",
        });

        // SignalDataLoader 装载规则第 1 步 = context 缓存槽（单槽 Ohlcvs，Symbol 匹配）/ Step 1 = the cached Ohlcvs slot.
        var ohlcvs = new Quant.Infra.Net.SourceData.Model.Ohlcvs { Symbol = "QQQM" };
        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < closes.Count; i++)
        {
            ohlcvs.OhlcvSet.Add(new Quant.Infra.Net.SourceData.Model.Ohlcv
            {
                Symbol = "QQQM",
                OpenDateTime = baseTime.AddDays(i),
                Open = (decimal)closes[i],
                High = (decimal)closes[i],
                Low = (decimal)closes[i],
                Close = (decimal)closes[i],
                Volume = 1m,
            });
        }
        context.Set(ohlcvs);

        await new QqqmReverseDcaStrategy.QqqmReverseDcaStage(null, null).ExecuteAsync(context, CancellationToken.None);

        var expectedWeight = QqqmReverseDcaStrategy.ComputeTargetWeight(80.0, 99.9, 0.5, 1.5, 1.0, 0.0, 1.0);
        var signals = context.Get<IReadOnlyList<Quant.Infra.Net.Orchestration.Models.Signal>>();
        Assert.IsNotNull(signals);
        Assert.AreEqual(1, signals.Count);
        Assert.AreEqual(Quant.Infra.Net.Orchestration.Models.SignalDirection.Long, signals[0].Direction);
        Assert.AreEqual(expectedWeight, signals[0].Strength, 1e-9);
        StringAssert.Contains(signals[0].Reason, "ratio=");
        StringAssert.Contains(signals[0].Reason, "deviation=");
        StringAssert.Contains(signals[0].Reason, "targetWeight=");

        // 与内置阶段同槽位契约：TargetPosition 以 IReadOnlyList 槽位写入（Risk/Execution/PortfolioState 读该槽位）/
        // Matches the built-in stages' slot contract: TargetPosition is written as an IReadOnlyList slot.
        var targets = context.Get<IReadOnlyList<Quant.Infra.Net.Orchestration.Models.TargetPosition>>();
        Assert.IsNotNull(targets);
        Assert.AreEqual(1, targets.Count);
        Assert.AreEqual(expectedWeight, targets[0].TargetWeight, 1e-9);
        Assert.AreSame(signals[0], targets[0].OriginSignal);
    }
}
