using Quant.Infra.Net.Orchestration.Models;

namespace Quant.Infra.Net.Orchestration.Abstractions;

/// <summary>
/// 风控抽象：在调仓前对目标仓位做前置检查。
/// Risk management abstraction: pre-checks target positions before rebalancing.
/// </summary>
public interface IRiskManager
{
    /// <summary>
    /// 评估目标仓位；任一条规则失败则 Approved=false 并给出全部理由。
    /// Assesses target positions; if any rule fails, Approved=false with all reasons listed.
    /// </summary>
    /// <param name="targets">目标仓位列表（不得为 null）/ Target positions (must not be null).</param>
    /// <param name="current">当前组合快照（不得为 null）/ Current portfolio snapshot (must not be null).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>风控评估结果 / Risk assessment result.</returns>
    Task<RiskAssessment> AssessAsync(IReadOnlyList<TargetPosition> targets, PortfolioSnapshot current, CancellationToken ct);
}
