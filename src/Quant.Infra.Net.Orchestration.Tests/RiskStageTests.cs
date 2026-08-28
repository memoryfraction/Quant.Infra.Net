using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Orchestration.Risk;
using Quant.Infra.Net.Orchestration.Stages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quant.Infra.Net.Orchestration.Tests;

/// <summary>
/// 测试用：记录 PublishAsync 调用（永不抛出）。
/// Test double: records PublishAsync calls (never throws).
/// </summary>
internal sealed class RecordingNotificationHub : INotificationHub
{
    public List<(NotificationSeverity Severity, string Title, string Message)> Published { get; } = new();

    public Task PublishAsync(NotificationSeverity severity, string title, string message, CancellationToken ct)
    {
        Published.Add((severity, title, message));
        return Task.CompletedTask;
    }
}

/// <summary>
/// 测试用：固定快照的状态存储。
/// Test double: fixed-snapshot state store.
/// </summary>
internal sealed class FixedSnapshotStore : IPortfolioStateStore
{
    private readonly PortfolioSnapshot _snapshot;

    public FixedSnapshotStore(PortfolioSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public Task SaveSnapshotAsync(PortfolioSnapshot snapshot, CancellationToken ct)
        => Task.CompletedTask;

    public Task<PortfolioSnapshot?> GetLatestAsync(CancellationToken ct)
        => Task.FromResult(_snapshot);
}

/// <summary>
/// RiskStage 单元测试：通过/拒绝路径 + Warning 通知 + PipelineAbortException。
/// RiskStage unit tests: pass/reject paths + Warning notification + PipelineAbortException.
/// </summary>
[TestClass]
public class RiskStageTests
{
    private static PortfolioSnapshot HealthySnapshot()
        => new()
        {
            SnapshotUtc = DateTime.UtcNow,
            AccountEquityUsd = 10000m,
            ActualWeights = new Dictionary<string, double>(),
            TargetWeights = new Dictionary<string, double>(),
            UnrealizedProfitRate = 0d
        };

    private static RiskStage NewStage(RecordingNotificationHub hub, IPortfolioStateStore? store = null)
        => new(new DefaultRiskManager(new OrchestrationOptions()), hub, store ?? new FixedSnapshotStore(HealthySnapshot()));

    /// <summary>
    /// 通过：Approved=true 写入上下文，不通知。
    /// Pass: Approved=true recorded in context, no notification.
    /// </summary>
    [TestMethod]
    public async Task Pass_ApprovedInContext_NoNotification()
    {
        var hub = new RecordingNotificationHub();
        var ctx = new PipelineContext(700);
        ctx.Set<IReadOnlyList<TargetPosition>>(new List<TargetPosition>
        {
            new() { Symbol = "AAA", TargetWeight = 0.3d },
            new() { Symbol = "BBB", TargetWeight = -0.3d }
        });

        await NewStage(hub).ExecuteAsync(ctx, CancellationToken.None);

        Assert.IsTrue(ctx.Get<RiskAssessment>()!.Approved);
        Assert.AreEqual(0, hub.Published.Count);
        Assert.IsTrue(ctx.Events.Any(e => e.Stage == "Risk" && e.Message.Contains("passed")));
    }

    /// <summary>
    /// 拒绝：Warning 通知 + PipelineAbortException + Approved=false 入上下文。
    /// Reject: Warning notification + PipelineAbortException + Approved=false recorded.
    /// </summary>
    [TestMethod]
    public async Task Reject_WarnsAndAborts()
    {
        var hub = new RecordingNotificationHub();
        var ctx = new PipelineContext(701);
        ctx.Set<IReadOnlyList<TargetPosition>>(new List<TargetPosition>
        {
            new() { Symbol = "AAA", TargetWeight = 0.9d } // 超过 MaxWeightPerSymbol(0.5)
        });

        await Assert.ThrowsExceptionAsync<PipelineAbortException>(
            () => NewStage(hub).ExecuteAsync(ctx, CancellationToken.None));

        Assert.IsFalse(ctx.Get<RiskAssessment>()!.Approved);
        Assert.AreEqual(1, hub.Published.Count);
        Assert.AreEqual(NotificationSeverity.Warning, hub.Published[0].Severity);
        StringAssert.Contains(hub.Published[0].Message, "AAA");
    }

    /// <summary>
    /// 无状态存储快照时回退合成快照（浮盈 0 → 仍通过/拒绝取决于规则）。
    /// Without a stored snapshot, a synthetic default is used (rate 0).
    /// </summary>
    [TestMethod]
    public async Task NoStoredSnapshot_SynthesizedDefault()
    {
        var hub = new RecordingNotificationHub();
        var store = new FixedSnapshotStore(null!);
        var ctx = new PipelineContext(702);
        ctx.Set<IReadOnlyList<TargetPosition>>(new List<TargetPosition>
        {
            new() { Symbol = "AAA", TargetWeight = 0.3d }
        });

        await new RiskStage(new DefaultRiskManager(new OrchestrationOptions()), hub, store)
            .ExecuteAsync(ctx, CancellationToken.None);

        Assert.IsTrue(ctx.Get<RiskAssessment>()!.Approved);
    }

    /// <summary>
    /// 异常参数校验。
    /// Argument validation.
    /// </summary>
    [TestMethod]
    public async Task InvalidArguments_Throw()
    {
        var hub = new RecordingNotificationHub();
        Assert.ThrowsException<ArgumentNullException>(() => new RiskStage(null!, hub, new FixedSnapshotStore(HealthySnapshot())));
        var ok = new RiskStage(new DefaultRiskManager(new OrchestrationOptions()), hub, new FixedSnapshotStore(HealthySnapshot()));
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await ok.ExecuteAsync(null!, CancellationToken.None));
    }
}
