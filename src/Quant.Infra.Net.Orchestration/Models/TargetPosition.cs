namespace Quant.Infra.Net.Orchestration.Models;

/// <summary>
/// 目标仓位：调仓前的期望权重（正=多头，负=空头，0=空仓）。
/// Target position: desired portfolio weight before rebalancing (positive = long, negative = short, 0 = flat).
/// </summary>
public class TargetPosition
{
    /// <summary>
    /// 标的代码。
    /// Trading symbol.
    /// </summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>
    /// 目标权重（组合比例）/ Target weight (portfolio fraction).
    /// </summary>
    public double TargetWeight { get; init; }

    /// <summary>
    /// 来源信号，供审计溯源（Signal→TargetPosition→ExecutionReport 全链路）/ Originating signal for audit trail (Signal→TargetPosition→ExecutionReport).
    /// </summary>
    public Signal? OriginSignal { get; init; }
}
