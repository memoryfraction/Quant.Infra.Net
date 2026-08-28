using Quant.Infra.Net.Orchestration.Models;

namespace Quant.Infra.Net.Orchestration.Abstractions;

/// <summary>
/// 管道阶段抽象：按序执行的业务步骤（数据→分析→信号→目标→风控→执行→状态→通知）。
/// Pipeline stage abstraction: an ordered business step (data/analysis/signal/target/risk/execution/state/notification).
/// </summary>
public interface IPipelineStage
{
    /// <summary>
    /// 阶段名（唯一，用于日志与排序）。
    /// Stage name (unique; used for logging and ordering).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 执行该阶段；通过 PipelineContext 读取输入、写入输出。
    /// Executes the stage; reads inputs from and writes outputs to the PipelineContext.
    /// </summary>
    /// <param name="context">管道上下文（不得为 null）/ Pipeline context (must not be null).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>表示异步操作的任务 / Task representing the async operation.</returns>
    Task ExecuteAsync(IPipelineContext context, CancellationToken ct);
}
