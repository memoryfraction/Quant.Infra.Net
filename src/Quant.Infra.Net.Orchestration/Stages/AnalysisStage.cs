using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Signals;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Orchestration.Stages;

/// <summary>
/// 分析阶段：对 context 中的行情输出结构化分析摘要事件（bar 数、lastClose、mean、std），不产生交易对象。
/// Analysis stage: emits structured analysis summary events (bar count, lastClose, mean, std) for the data in the context; produces no trading objects.
/// </summary>
public sealed class AnalysisStage : IPipelineStage
{
    /// <summary>
    /// 阶段名（固定 "Analysis"）。
    /// Stage name (fixed "Analysis").
    /// </summary>
    public string Name => "Analysis";

    /// <summary>
    /// 执行分析摘要。
    /// Executes the analysis summary.
    /// </summary>
    /// <param name="context">管道上下文（不得为 null）/ Pipeline context (must not be null).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>表示操作完成的任务 / Task representing completion.</returns>
    public Task ExecuteAsync(IPipelineContext context, CancellationToken ct)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var single = context.Get<Ohlcvs>();
        var union = context.Get<HashSet<Ohlcv>>();

        if (single != null && single.OhlcvSet.Count > 0)
        {
            EmitSummary(context, single.Symbol, single.OhlcvSet.Select(o => (double)o.Close).ToList());
        }

        if (union != null && union.Count > 0)
        {
            if (single != null && single.OhlcvSet.Count > 0)
            {
                // 单槽与合并槽并存时，合并槽已含该标的则跳过避免重复摘要。
                // When both slots exist, skip the symbol already summarized from the single slot.
                union = union.Where(o => !string.Equals(o?.Symbol, single.Symbol, StringComparison.OrdinalIgnoreCase)).ToHashSet();
            }

            foreach (var group in union.GroupBy(o => (o?.Symbol ?? "?"), StringComparer.OrdinalIgnoreCase))
            {
                var closes = group.Select(o => (double)o.Close).OrderBy(x => x).ToList();
                EmitSummary(context, group.Key, closes);
            }
        }

        if ((single == null || single.OhlcvSet.Count == 0) && (union == null || union.Count == 0))
        {
            context.AddEvent(PipelineEvent.Create(context.RunId, Name, "no market data in context; analysis skipped"));
        }

        return Task.CompletedTask;
    }

    private static void EmitSummary(IPipelineContext context, string symbol, IReadOnlyList<double> closes)
    {
        var closesSorted = closes.OrderBy(c => c).ToList();
        var last = closesSorted[closesSorted.Count - 1];
        var mean = closesSorted.Average();
        var std = OrchestrationNumerics.PopulationStdDev(closesSorted);
        context.AddEvent(PipelineEvent.Create(
            context.RunId,
            "Analysis",
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}: bars={1} lastClose={2:0.4} mean={3:0.4} std={4:0.4}",
                new object[] { symbol, closesSorted.Count, last, mean, std })));
    }
}


