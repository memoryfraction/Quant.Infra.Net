namespace Quant.Infra.Net.Orchestration.Models;

/// <summary>
/// 组合快照：账户权益、未实现盈亏率、实际/目标权重表。
/// Portfolio snapshot: account equity, unrealized profit rate, actual and target weight tables.
/// </summary>
public class PortfolioSnapshot
{
    /// <summary>
    /// 未实现盈亏率（0.01 = +1%）。
    /// Unrealized profit rate (0.01 = +1%).
    /// </summary>
    public double UnrealizedProfitRate { get; init; }

    /// <summary>
    /// 快照时间（UTC）。
    /// Snapshot timestamp (UTC).
    /// </summary>
    public DateTime SnapshotUtc { get; init; }

    /// <summary>
    /// 账户权益（USD）。
    /// Account equity in USD.
    /// </summary>
    public decimal AccountEquityUsd { get; init; }

    /// <summary>
    /// 实际权重表（标的 → 组合权重，正=多头/负=空头）。
    /// Actual weights (symbol to portfolio fraction; positive = long, negative = short).
    /// </summary>
    public IReadOnlyDictionary<string, double> ActualWeights { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// 目标权重表（标的 → 组合权重，正=多头/负=空头）。
    /// Target weights (symbol to portfolio fraction; positive = long, negative = short).
    /// </summary>
    public IReadOnlyDictionary<string, double> TargetWeights { get; init; } = new Dictionary<string, double>();
}
