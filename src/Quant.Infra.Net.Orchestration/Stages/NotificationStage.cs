using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using System.Globalization;

namespace Quant.Infra.Net.Orchestration.Stages;

/// <summary>
/// 通知阶段（执行末尾）：把本次运行汇总成 Info（或含错误时 Warning）通知。
/// Notification stage (end of run): summarizes the run into an Info (or Warning with errors) notification.
/// </summary>
public sealed class NotificationStage : IPipelineStage
{
    private readonly INotificationHub _hub;

    /// <summary>
    /// 创建通知阶段。
    /// Creates the notification stage.
    /// </summary>
    /// <param name="hub">通知网关（不得为 null）/ Notification hub (must not be null).</param>
    /// <exception cref="ArgumentNullException">入参为 null 时抛出 / Thrown when null.</exception>
    public NotificationStage(INotificationHub hub)
    {
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
    }

    /// <inheritdoc />
    public string Name => "Notification";

    /// <inheritdoc />
    public async Task ExecuteAsync(IPipelineContext context, CancellationToken ct)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        ct.ThrowIfCancellationRequested();

        var signals = context.Get<IReadOnlyList<Signal>>() ?? Array.Empty<Signal>();
        var reports = context.Get<IReadOnlyList<ExecutionReport>>() ?? Array.Empty<ExecutionReport>();
        var snapshot = context.Get<PortfolioSnapshot>();
        var ok = reports.Count(r => r.Success);

        var summary = string.Format(
            CultureInfo.InvariantCulture,
            "run={0}; signals={1}; execution {2}/{3} ok; equity={4:0.00}; positions={5}",
            context.RunId, signals.Count, ok, reports.Count,
            (snapshot is null ? 0d : (double)snapshot.AccountEquityUsd), snapshot?.ActualWeights.Count ?? 0);

        if (context.Errors.Count > 0)
        {
            var detail = string.Join(" | ", context.Errors.Select(e => e.Message).Take(5));
            context.AddEvent(PipelineEvent.Create(context.RunId, Name, $"WARNING notified: {context.Errors.Count} error(s)"));
            await _hub.PublishAsync(NotificationSeverity.Warning, "Orchestration run finished with errors", summary + " ; errors: " + detail, ct)
                .ConfigureAwait(false);
            return;
        }

        context.AddEvent(PipelineEvent.Create(context.RunId, Name, "INFO summary notified"));
        await _hub.PublishAsync(NotificationSeverity.Info, "Orchestration run summary", summary, ct).ConfigureAwait(false);
    }
}
