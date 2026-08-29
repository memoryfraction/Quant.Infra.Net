using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Backtest.Reporting;
using Quant.Infra.Net.Portfolio.Services;

namespace Quant.Infra.Net.Backtest.Metrics;

/// <summary>
/// 绩效指标装配（B4）：权益曲线 → 核心库 <see cref="StrategyPerformanceAnalyzer"/>（CAGR/Sharpe/Calmar/最大回撤），
/// 成交记录 → <see cref="TradeStatistics"/>（胜率/盈亏比/手续费）。不重新实现任何同名指标（§11 护栏）。
/// Metrics assembly (B4): the equity curve feeds the core library's StrategyPerformanceAnalyzer
/// (CAGR/Sharpe/Calmar/max drawdown); the trade records feed TradeStatistics (win rate / profit factor / commission).
/// No same-named metric is re-implemented (section 11 guardrail).
/// </summary>
public static class BacktestMetricsFactory
{
    /// <summary>
    /// 装配一次回测的绩效指标。
    /// Assembles the performance metrics for one backtest run.
    /// </summary>
    /// <param name="equityCurve">权益曲线（模拟时刻 → 权益）/ The equity curve (simulated instant to equity).</param>
    /// <param name="trades">成交记录（按时间顺序）/ The trade records (chronological).</param>
    /// <returns>完整指标（空曲线时为安全默认值）/ The full metrics (safe defaults for an empty curve).</returns>
    public static BacktestMetrics Assemble(IReadOnlyDictionary<DateTime, decimal>? equityCurve, IReadOnlyList<BacktestTrade>? trades)
    {
        if (equityCurve == null || equityCurve.Count < 2)
        {
            return SafeMetrics(trades);
        }

        var ordered = equityCurve.OrderBy(kvp => kvp.Key).ToList();
        var series = new Dictionary<DateTime, decimal>(ordered.Count);
        foreach (var kvp in ordered)
        {
            series[kvp.Key] = kvp.Value;
        }

        // Dictionary 默认按值比较 DateTime 键，无需自定义比较器。
        // The default DateTime key comparer (by value) is exactly what we need.
        var values = ordered.Select(kvp => kvp.Value).ToList();
        var spanDays = (ordered[^1].Key - ordered[0].Key).TotalDays;

        var cagr = spanDays > 0d
            ? StrategyPerformanceAnalyzer.CalculateCAGR(series)
            : 0m;

        var maxDrawdown = StrategyPerformanceAnalyzer.CalculateMaximumDrawdown(values);
        var (_, maxDrawdownDurationDays) = StrategyPerformanceAnalyzer.CalculateMaxDrawdownDuration(values);

        // Sharpe/Calmar 在曲线全部相等（stddev=0 触发除零）时未定义：安全降级为 0。
        // Sharpe/Calmar are undefined for a flat curve (stddev = 0 would divide by zero); degrade to 0.
        var hasDispersion = values.Distinct().Count() > 1;
        var sharpe = values.Count >= 3 && hasDispersion
            ? StrategyPerformanceAnalyzer.CalculateSharpeRatio(series, 0m)
            : 0m;

        var calmar = hasDispersion && maxDrawdown > 0m && spanDays > 0d
            ? StrategyPerformanceAnalyzer.CalculateCalmarRatio(series)
            : 0m;

        var tradeStats = trades == null
            ? new TradeSummary(0, 0, 0d, 0d, 0m)
            : TradeStatistics.Compute(trades);

        return new BacktestMetrics
        {
            Cagr = cagr,
            SharpeRatio = (double)sharpe,
            CalmarRatio = (double)calmar,
            MaxDrawdown = maxDrawdown == 0m ? 0m : -maxDrawdown, // §7.2：负值或零 / negative or zero
            MaxDrawdownDurationDays = maxDrawdownDurationDays,
            TotalTrades = trades?.Count ?? 0,
            WinRate = tradeStats.WinRate,
            ProfitFactor = tradeStats.ProfitFactor,
            TotalCommissionUsd = tradeStats.TotalCommissionUsd,
        };
    }

    private static BacktestMetrics SafeMetrics(IReadOnlyList<BacktestTrade>? trades)
    {
        var tradeStats = trades == null
            ? new TradeSummary(0, 0, 0d, 0d, 0m)
            : TradeStatistics.Compute(trades);

        return new BacktestMetrics
        {
            Cagr = 0m,
            SharpeRatio = 0d,
            CalmarRatio = 0d,
            MaxDrawdown = 0m,
            MaxDrawdownDurationDays = 0,
            TotalTrades = trades?.Count ?? 0,
            WinRate = tradeStats.WinRate,
            ProfitFactor = tradeStats.ProfitFactor,
            TotalCommissionUsd = tradeStats.TotalCommissionUsd,
        };
    }
}
