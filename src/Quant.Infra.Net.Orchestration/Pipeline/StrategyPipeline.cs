using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;

namespace Quant.Infra.Net.Orchestration.Pipeline;

/// <summary>
/// 策略管道：按注册顺序依次执行 Stage；阶段名必须唯一且非空。
/// Strategy pipeline: executes stages in registration order; stage names must be unique and non-blank.
/// </summary>
/// <remarks>
/// 终止语义：任一 Stage 抛出 <see cref="PipelineAbortException"/> → 立即结束本次运行（业务正常终止，重抛给宿主）；
/// 其他异常 → 记入 context.Errors 后立即结束本次运行并重抛；两种情况都保证写入一条终止事件。
/// Termination semantics: a <see cref="PipelineAbortException"/> from any stage ends the run immediately (business-normal, rethrown to the host);
/// any other exception is recorded in context.Errors, ends the run, and is rethrown; both paths always emit a terminal event.
/// </remarks>
public sealed class StrategyPipeline
{
    private readonly List<IPipelineStage> _stages;

    /// <summary>
    /// 初始化管道（校验阶段名非空且不重复）。
    /// Initializes the pipeline (validates that stage names are non-blank and unique).
    /// </summary>
    /// <param name="stages">阶段集合（不得为 null，不得含 null 元素）/ Stages (must not be null nor contain null).</param>
    /// <exception cref="ArgumentNullException">stages 为 null 或含 null 元素时抛出 / Thrown when stages is null or contains null.</exception>
    /// <exception cref="ArgumentException">阶段名为空或重复时抛出 / Thrown when a stage name is blank or duplicated.</exception>
    public StrategyPipeline(IEnumerable<IPipelineStage> stages)
    {
        if (stages == null)
        {
            throw new ArgumentNullException(nameof(stages));
        }

        var list = new List<IPipelineStage>();
        foreach (var stage in stages)
        {
            if (stage == null)
            {
                throw new ArgumentException("Stages must not contain null.", nameof(stages));
            }

            if (string.IsNullOrWhiteSpace(stage.Name))
            {
                throw new ArgumentException("Stage names must not be blank.", nameof(stages));
            }

            list.Add(stage);
        }

        var duplicate = list
            .GroupBy(s => s.Name, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new ArgumentException($"Duplicate stage name: {duplicate.Key}", nameof(stages));
        }

        _stages = list;
    }

    /// <summary>
    /// 已注册阶段（只读，按执行顺序）。
    /// Registered stages (read-only, in execution order).
    /// </summary>
    public IReadOnlyCollection<IPipelineStage> Stages => _stages;

    /// <summary>
    /// 顺序执行全部阶段；终止语义见类型说明。
    /// Executes all stages in order; see type remarks for termination semantics.
    /// </summary>
    /// <param name="context">管道上下文（不得为 null）/ Pipeline context (must not be null).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>表示运行完成的任务 / Task representing the completed run.</returns>
    /// <exception cref="ArgumentNullException">context 为 null 时抛出 / Thrown when context is null.</exception>
    /// <exception cref="PipelineAbortException">阶段抛出时原样重抛 / Rethrown when a stage throws it.</exception>
    public async Task RunAsync(IPipelineContext context, CancellationToken ct)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.AddEvent(PipelineEvent.Create(context.RunId, "Pipeline", $"run started ({_stages.Count} stages)"));

        try
        {
            foreach (var stage in _stages)
            {
                ct.ThrowIfCancellationRequested();
                context.AddEvent(PipelineEvent.Create(context.RunId, stage.Name, $"stage '{stage.Name}' started"));
                await stage.ExecuteAsync(context, ct).ConfigureAwait(false);
                context.AddEvent(PipelineEvent.Create(context.RunId, stage.Name, $"stage '{stage.Name}' completed"));
            }
        }
        catch (PipelineAbortException abort)
        {
            context.AddEvent(PipelineEvent.Create(context.RunId, "Pipeline", $"run aborted (business): {abort.Message}"));
            throw;
        }
        catch (Exception ex)
        {
            context.AddError(ex);
            context.AddEvent(PipelineEvent.Create(context.RunId, "Pipeline", $"run failed: {ex.Message}"));
            throw;
        }

        context.AddEvent(PipelineEvent.Create(context.RunId, "Pipeline", "run finished successfully"));
    }
}
