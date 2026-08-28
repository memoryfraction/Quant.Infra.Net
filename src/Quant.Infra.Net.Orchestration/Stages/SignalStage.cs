using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;

namespace Quant.Infra.Net.Orchestration.Stages;

/// <summary>
/// 信号阶段：调用已注册的策略生成器，把信号写入 context（IReadOnlyList&lt;Signal&gt; 槽），无论空集与否。
/// Signal stage: invokes the registered strategy generator and writes signals into the context (IReadOnlyList&lt;Signal&gt; slot), even when empty.
/// </summary>
public sealed class SignalStage : IPipelineStage
{
    private readonly ISignalGenerator _generator;

    /// <summary>
    /// 初始化信号阶段。
    /// Initializes the signal stage.
    /// </summary>
    /// <param name="generator">信号生成器（不得为 null）/ Signal generator (must not be null).</param>
    /// <exception cref="ArgumentNullException">generator 为 null 时抛出 / Thrown when generator is null.</exception>
    public SignalStage(ISignalGenerator generator)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
    }

    /// <summary>
    /// 阶段名（固定 "Signal"）。
    /// Stage name (fixed "Signal").
    /// </summary>
    public string Name => "Signal";

    /// <summary>
    /// 执行信号生成。
    /// Executes signal generation.
    /// </summary>
    /// <param name="context">管道上下文（不得为 null）/ Pipeline context (must not be null).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>表示操作完成的任务 / Task representing completion.</returns>
    public async Task ExecuteAsync(IPipelineContext context, CancellationToken ct)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var signals = await _generator.GenerateSignalsAsync(context, ct).ConfigureAwait(false);
        context.Set<IReadOnlyList<Signal>>(signals);

        var summary = signals.Count == 0
            ? "no signals"
            : string.Join("; ", signals.Select(s => $"{s.Symbol}={s.Direction} ({s.Strength:0.00})"));
        context.AddEvent(PipelineEvent.Create(context.RunId, Name, $"generator={_generator.Id}: {summary}"));
    }
}

