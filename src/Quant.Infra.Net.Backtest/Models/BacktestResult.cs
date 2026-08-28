using Quant.Infra.Net.Orchestration.Models;

namespace Quant.Infra.Net.Backtest.Models;

/// <summary>
/// 回测绩效指标（§7.2；由 B4 的 TradeStatistics + StrategyPerformanceAnalyzer 组装）。
/// Backtest performance metrics (§7.2; assembled in B4 by TradeStatistics + StrategyPerformanceAnalyzer).
/// </summary>
public sealed class BacktestMetrics
{
    /// <summary>
    /// 年化收益率。
    /// Compound annual growth rate.
    /// </summary>
    public decimal Cagr { get; init; }

    /// <summary>
    /// 夏普比率。
    /// Sharpe ratio.
    /// </summary>
    public double SharpeRatio { get; init; }

    /// <summary>
    /// 卡玛比率。
    /// Calmar ratio.
    /// </summary>
    public double CalmarRatio { get; init; }

    /// <summary>
    /// 最大回撤（负值或零，比例）。
    /// Maximum drawdown (negative or zero, as a fraction).
    /// </summary>
    public decimal MaxDrawdown { get; init; }

    /// <summary>
    /// 最大回撤持续天数。
    /// Maximum duration of a drawdown, in days.
    /// </summary>
    public int MaxDrawdownDurationDays { get; init; }

    /// <summary>
    /// 总成交笔数。
    /// Total number of trades.
    /// </summary>
    public int TotalTrades { get; init; }

    /// <summary>
    /// 胜率（平仓位点已实现盈亏 &gt; 0 的比例）。
    /// Win rate (share of closed positions with realized P/L &gt; 0).
    /// </summary>
    public double WinRate { get; init; }

    /// <summary>
    /// 盈亏比（总盈利 / 总亏损绝对值）。
    /// Profit factor (gross profit / absolute gross loss).
    /// </summary>
    public double ProfitFactor { get; init; }

    /// <summary>
    /// 累计手续费（USD）。
    /// Accumulated commission in USD.
    /// </summary>
    public decimal TotalCommissionUsd { get; init; }
}

/// <summary>
/// 单次回测结果（§7.2）。
/// A single backtest result (§7.2).
/// </summary>
public sealed class BacktestResult
{
    /// <summary>
    /// 权益曲线：模拟时刻（UTC）→ 权益（USD）。
    /// Equity curve: simulated instant (UTC) → equity (USD).
    /// </summary>
    public IReadOnlyDictionary<DateTime, decimal> EquityCurve { get; init; } = new Dictionary<DateTime, decimal>();

    /// <summary>
    /// 成交记录（按时间顺序）。
    /// Trade records (chronological).
    /// </summary>
    public IReadOnlyList<BacktestTrade> Trades { get; init; } = Array.Empty<BacktestTrade>();

    /// <summary>
    /// 全程管道事件汇总。
    /// All pipeline events, aggregated.
    /// </summary>
    public IReadOnlyList<PipelineEvent> RunEvents { get; init; } = Array.Empty<PipelineEvent>();

    /// <summary>
    /// 绩效指标。
    /// Performance metrics.
    /// </summary>
    public BacktestMetrics Metrics { get; init; } = new BacktestMetrics();
}
