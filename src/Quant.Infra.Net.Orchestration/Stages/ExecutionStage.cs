using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using System.Globalization;

namespace Quant.Infra.Net.Orchestration.Stages;

/// <summary>
/// 执行阶段：调用执行模型完成调仓并写入执行报告。
/// Execution stage: invokes the execution model to rebalance and records reports.
/// </summary>
public sealed class ExecutionStage : IPipelineStage
{
    private readonly IExecutionModel _model;
    private readonly IBinanceUsdFutureService _broker;

    /// <summary>
    /// 创建执行阶段。
    /// Creates the execution stage.
    /// </summary>
    /// <param name="model">执行模型（不得为 null）/ Execution model (must not be null).</param>
    /// <param name="broker">券商服务（不得为 null；Paper 环境下为 PaperBinanceUsdFutureService）/ Broker service (must not be null).</param>
    /// <exception cref="ArgumentNullException">入参为 null 时抛出 / Thrown when null.</exception>
    public ExecutionStage(IExecutionModel model, IBinanceUsdFutureService broker)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
    }

    /// <inheritdoc />
    public string Name => "Execution";

    /// <inheritdoc />
    public async Task ExecuteAsync(IPipelineContext context, CancellationToken ct)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        ct.ThrowIfCancellationRequested();

        // 先用上下文行情刷新 Paper 标记价（保证估值/平仓盈亏基于最新收盘价）。
        // Refresh Paper marks from context data first (valuation/closing PnL uses latest closes).
        StageMarketData.ApplyPaperMarks(context, _broker);

        var targets = context.Get<IReadOnlyList<TargetPosition>>() ?? Array.Empty<TargetPosition>();
        var reports = await _model.RebalanceAsync(targets, ct).ConfigureAwait(false);
        context.Set<IReadOnlyList<ExecutionReport>>(reports);

        var ok = reports.Count(r => r.Success);
        context.AddEvent(PipelineEvent.Create(context.RunId, Name,
            string.Format(CultureInfo.InvariantCulture, "execution done: {0}/{1} ok", ok, reports.Count)));
    }
}
