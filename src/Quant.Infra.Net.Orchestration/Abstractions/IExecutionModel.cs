using Quant.Infra.Net.Orchestration.Models;

namespace Quant.Infra.Net.Orchestration.Abstractions;

/// <summary>
/// 执行模型抽象：把目标仓位转换为券商调仓调用（Paper 环境 = 纯内存）。
/// Execution model abstraction: translates target positions into broker rebalancing calls (Paper environment = pure in-memory).
/// </summary>
public interface IExecutionModel
{
    /// <summary>
    /// 按 TargetWeight 调仓；每个标的产出一条执行报告。
    /// Rebalances according to TargetWeight; produces one execution report per symbol.
    /// </summary>
    /// <param name="targets">目标仓位列表（不得为 null）/ Target positions (must not be null).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>执行报告列表 / Execution reports.</returns>
    Task<IReadOnlyList<ExecutionReport>> RebalanceAsync(IReadOnlyList<TargetPosition> targets, CancellationToken ct);
}
