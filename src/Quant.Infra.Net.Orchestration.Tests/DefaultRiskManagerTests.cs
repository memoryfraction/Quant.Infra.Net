using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Risk;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quant.Infra.Net.Orchestration.Tests;

/// <summary>
/// DefaultRiskManager 单元测试：三条规则各自通过/拒绝 + 多因叠加 + 全通过。
/// DefaultRiskManager unit tests: each rule pass/reject + multi-reason + all-pass.
/// </summary>
[TestClass]
public class DefaultRiskManagerTests
{
    private const string SymX = "X";
    private const string SymY = "Y";

    private static DefaultRiskManager NewManager(double maxWeight = 0.5d, double maxGross = 2.0d, double killSwitch = -0.20d)
        => new(new OrchestrationOptions
        {
            MaxWeightPerSymbol = maxWeight,
            MaxGrossExposure = maxGross,
            KillSwitchDrawdownRate = killSwitch
        });

    private static PortfolioSnapshot Snapshot(double unrealizedRate = 0d)
        => new()
        {
            SnapshotUtc = DateTime.UtcNow,
            AccountEquityUsd = 10000m,
            ActualWeights = new Dictionary<string, double>(),
            TargetWeights = new Dictionary<string, double>(),
            UnrealizedProfitRate = unrealizedRate
        };

    private static TargetPosition Pos(string symbol, double weight)
        => new() { Symbol = symbol, TargetWeight = weight };

    /// <summary>
    /// 单标的权重越界 → 拒绝且原因点名该标的。
    /// Per-symbol weight breach → rejected, naming the symbol.
    /// </summary>
    [TestMethod]
    public async Task WeightPerSymbolExceeds_Rejected()
    {
        var manager = NewManager();
        var assessment = await manager.AssessAsync(new[] { Pos(SymX, 0.6d) }, Snapshot(), CancellationToken.None);

        Assert.IsFalse(assessment.Approved);
        Assert.AreEqual(1, assessment.Reasons.Count);
        StringAssert.Contains(assessment.Reasons[0], SymX);
    }

    /// <summary>
    /// 总敞口越界 → 拒绝。
    /// Gross exposure breach → rejected.
    /// </summary>
    [TestMethod]
    public async Task GrossExposureExceeds_Rejected()
    {
        var manager = NewManager(maxGross: 0.9d);
        var assessment = await manager.AssessAsync(
            new[] { Pos(SymX, 0.5d), Pos(SymY, -0.5d) }, Snapshot(), CancellationToken.None);

        Assert.IsFalse(assessment.Approved);
        Assert.AreEqual(1, assessment.Reasons.Count);
        StringAssert.Contains(assessment.Reasons[0], "gross exposure");
    }

    /// <summary>
    /// Kill-switch：回撤到阈值（含等于）→ 拒绝且附"全部平仓"建议。
    /// Kill-switch: drawdown at/below threshold → rejected with liquidation advice.
    /// </summary>
    [TestMethod]
    public async Task KillSwitch_DrawdownAtThreshold_RejectedWithAdvice()
    {
        var manager = NewManager(killSwitch: -0.20d);
        var assessment = await manager.AssessAsync(new[] { Pos(SymX, 0.1d) }, Snapshot(unrealizedRate: -0.20d), CancellationToken.None);

        Assert.IsFalse(assessment.Approved);
        StringAssert.Contains(assessment.Reasons[0], "liquidation");
    }

    /// <summary>
    /// 多规则同时触发 → 全部原因都在列表中。
    /// Multiple rule breaches → all reasons are listed.
    /// </summary>
    [TestMethod]
    public async Task MultipleBreaches_AllReasonsCollected()
    {
        var manager = NewManager(maxWeight: 0.3d, maxGross: 0.4d, killSwitch: -0.10d);
        var assessment = await manager.AssessAsync(
            new[] { Pos(SymX, 0.5d), Pos(SymY, -0.5d) },
            Snapshot(unrealizedRate: -0.15d),
            CancellationToken.None);

        Assert.IsFalse(assessment.Approved);
        Assert.AreEqual(4, assessment.Reasons.Count); // 两个单标的越界 + 总敞口越界 + kill-switch
        StringAssert.Contains(string.Join(" ", assessment.Reasons), SymX);
        StringAssert.Contains(string.Join(" ", assessment.Reasons), SymY);
        StringAssert.Contains(string.Join(" ", assessment.Reasons), "gross exposure");
        StringAssert.Contains(string.Join(" ", assessment.Reasons), "liquidation");
    }

    /// <summary>
    /// 全部通过 → Approved。
    /// All rules pass → approved.
    /// </summary>
    [TestMethod]
    public async Task AllPass_Approved()
    {
        var manager = NewManager();
        var assessment = await manager.AssessAsync(
            new[] { Pos(SymX, 0.3d), Pos(SymY, -0.3d) },
            Snapshot(unrealizedRate: -0.10d),
            CancellationToken.None);

        Assert.IsTrue(assessment.Approved);
        Assert.AreEqual(0, assessment.Reasons.Count);
    }

    /// <summary>
    /// 空目标 + 健康快照 → 通过（空仓也允许）。
    /// Empty targets + healthy snapshot → approved (no position is allowed).
    /// </summary>
    [TestMethod]
    public async Task EmptyTargets_Approved()
    {
        var manager = NewManager();
        var assessment = await manager.AssessAsync(Array.Empty<TargetPosition>(), Snapshot(), CancellationToken.None);
        Assert.IsTrue(assessment.Approved);
    }

    /// <summary>
    /// null targets / null snapshot → ArgumentNullException。
    /// Null targets / null snapshot → ArgumentNullException.
    /// </summary>
    [TestMethod]
    public void NullArguments_Throw()
    {
        var manager = NewManager();
        Assert.ThrowsException<ArgumentNullException>(
            () => manager.AssessAsync(null!, Snapshot(), CancellationToken.None).GetAwaiter().GetResult());
        Assert.ThrowsException<ArgumentNullException>(
            () => manager.AssessAsync(new[] { Pos(SymX, 0.1d) }, null!, CancellationToken.None).GetAwaiter().GetResult());
    }

    /// <summary>
    /// 已取消令牌 → OperationCanceledException。
    /// Cancelled token → OperationCanceledException.
    /// </summary>
    [TestMethod]
    public void CancelledToken_Throws()
    {
        var manager = NewManager();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsException<OperationCanceledException>(
            () => manager.AssessAsync(new[] { Pos(SymX, 0.1d) }, Snapshot(), cts.Token).GetAwaiter().GetResult());
    }
}
