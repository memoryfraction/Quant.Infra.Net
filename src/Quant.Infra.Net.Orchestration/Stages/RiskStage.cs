using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using System.Globalization;

namespace Quant.Infra.Net.Orchestration.Stages;

/// <summary>
/// 风控阶段：执行前检查目标组合；不通过 → Warning 通知 + PipelineAbortException 终止本次运行。
/// Risk stage: pre-trading check of targets; on failure → Warning notification + PipelineAbortException ends the run.
/// </summary>
public sealed class RiskStage : IPipelineStage
{
    private readonly IRiskManager _manager;
    private readonly INotificationHub _hub;
    private readonly IPortfolioStateStore _store;

    /// <summary>
    /// 创建风控阶段。
    /// Creates the risk stage.
    /// </summary>
    /// <param name="manager">风控管理器（不得为 null）/ Risk manager (must not be null).</param>
    /// <param name="hub">通知网关（不得为 null）/ Notification hub (must not be null).</param>
    /// <param name="store">组合状态存储（提供当前快照；不得为 null）/ Portfolio state store (supplies the current snapshot; must not be null).</param>
    /// <exception cref="ArgumentNullException">入参为 null 时抛出 / Thrown when null.</exception>
    public RiskStage(IRiskManager manager, INotificationHub hub, IPortfolioStateStore store)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public string Name => "Risk";

    /// <inheritdoc />
    public async Task ExecuteAsync(IPipelineContext context, CancellationToken ct)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        ct.ThrowIfCancellationRequested();

        var targets = context.Get<IReadOnlyList<TargetPosition>>() ?? Array.Empty<TargetPosition>();
        var current = await _store.GetLatestAsync(ct).ConfigureAwait(false)
            ?? new PortfolioSnapshot
            {
                SnapshotUtc = DateTime.UtcNow,
                AccountEquityUsd = 0m,
                ActualWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
                TargetWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase),
                UnrealizedProfitRate = 0d
            };

        var assessment = await _manager.AssessAsync(targets, current, ct).ConfigureAwait(false);
        context.Set<RiskAssessment>(assessment);

        if (!assessment.Approved)
        {
            var message = string.Join("; ", assessment.Reasons);
            context.AddEvent(PipelineEvent.Create(context.RunId, Name,
                string.Format(CultureInfo.InvariantCulture, "risk check REJECTED: {0}", message)));
            await _hub.PublishAsync(NotificationSeverity.Warning, "Risk check rejected", message, ct)
                .ConfigureAwait(false);
            throw new PipelineAbortException(string.Format(CultureInfo.InvariantCulture, "risk check rejected: {0}", message));
        }

        context.AddEvent(PipelineEvent.Create(context.RunId, Name, "risk check passed"));
    }
}
