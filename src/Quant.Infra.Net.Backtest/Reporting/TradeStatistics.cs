using Binance.Net.Enums;
using Quant.Infra.Net.Backtest.Models;

namespace Quant.Infra.Net.Backtest.Reporting;

/// <summary>
/// 交易级统计（B4）：从成交记录序列计算胜率 / 盈亏比 / 总手续费。
/// Trade-level statistics (B4): win rate / profit factor / total commission from the trade record sequence.
/// </summary>
/// <remarks>
/// 口径（§7.4 每笔调用一条成交记录）：<see cref="Models.BacktestTrade.NotionalUsd"/> == 0 的记录是
/// "平掉当前状态"，否则"平掉此前状态并重新打开"。已平仓状态按其平仓成交价结算已实现盈亏；
/// 未平仓状态不计入胜率/盈亏比。零手续费零滑点时该口径与 broker 的已实现盈亏一致。
/// Convention (section 7.4, one record per call): NotionalUsd == 0 closes the current state; a
/// non-zero record closes the prior state and reopens. Closed states are settled at their close-fill price;
/// still-open states are excluded from win rate / profit factor.
/// </remarks>
public static class TradeStatistics
{
    /// <summary>
    /// 从成交记录（按时间顺序）计算交易级指标。
    /// Computes trade-level metrics from chronologically-ordered trade records.
    /// </summary>
    /// <param name="trades">成交记录序列（null/空按零处理）/ The trade record sequence (null/empty treated as zero).</param>
    /// <returns>交易级统计结果 / The trade-level summary.</returns>
    public static TradeSummary Compute(IReadOnlyList<BacktestTrade>? trades)
    {
        if (trades == null || trades.Count == 0)
        {
            return new TradeSummary(0, 0, 0d, 0d, 0m);
        }

        var openBySymbol = new Dictionary<string, BacktestTrade>(StringComparer.OrdinalIgnoreCase);
        var grossWin = 0d;
        var grossLoss = 0d; // 以负值累计 / accumulated as a negative
        var closedCount = 0;
        var winningCount = 0;
        var totalCommission = 0m;

        foreach (var trade in trades)
        {
            if (trade == null)
            {
                continue;
            }

            totalCommission += trade.CommissionUsd;

            if (openBySymbol.TryGetValue(trade.Symbol, out var open))
            {
                var realizedPnl = RealizedPnlOf(open, trade.FillPrice);
                closedCount++;
                if (realizedPnl > 0d)
                {
                    winningCount++;
                    grossWin += realizedPnl;
                }
                else if (realizedPnl < 0d)
                {
                    grossLoss += realizedPnl; // 负值 / negative
                }
            }

            if (trade.NotionalUsd > 0m)
            {
                openBySymbol[trade.Symbol] = trade; // 重新打开状态 / reopen the position state
            }
            else
            {
                openBySymbol.Remove(trade.Symbol); // 平空状态 / flat state
            }
        }

        return new TradeSummary(
            closedCount,
            winningCount,
            closedCount > 0 ? (double)winningCount / closedCount : 0d,
            GrossProfitFactor(grossWin, grossLoss),
            totalCommission);
    }

    /// <summary>
    /// 累计盈亏比 = 总盈利 / 总亏损绝对值；无亏损且无盈利为 0；无亏损但有盈利为正无穷。
    /// Gross profit factor = gross profit / |gross loss|; 0 when both are zero; +infinity when profit exists without loss.
    /// </summary>
    private static double GrossProfitFactor(double grossWin, double grossLoss)
    {
        // grossLoss 以负值累计：>= 0 表示没有亏损。
        // Losses are accumulated as negative; >= 0 means no losses at all.
        if (grossLoss >= 0d)
        {
            return grossWin > 0d ? double.PositiveInfinity : 0d;
        }

        return grossWin / Math.Abs(grossLoss);
    }

    /// <summary>
    /// 已平仓状态的已实现盈亏：多头 = N×(卖出−买入)/买入；空头 = N×(买入−卖出)/买入。
    /// Realized P/L of a closed state: long = N×(exit−entry)/entry; short = N×(entry−exit)/entry.
    /// </summary>
    private static double RealizedPnlOf(BacktestTrade open, decimal exitPrice)
    {
        var notional = (double)open.NotionalUsd;
        var entry = (double)open.FillPrice;
        if (notional <= 0d || entry <= 0d)
        {
            return 0d;
        }

        var change = ((double)exitPrice - entry) / entry;
        return open.Side == PositionSide.Short ? -notional * change : notional * change;
    }
}

/// <summary>
/// 交易级统计结果（不可变）。
/// Trade-level statistics summary (immutable).
/// </summary>
/// <param name="ClosedTradeCount">已平仓状态数 / Number of closed position states.</param>
/// <param name="WinningTradeCount">盈利平仓状态数 / Number of closed states in profit.</param>
/// <param name="WinRate">胜率（0..1）/ Win rate (0..1).</param>
/// <param name="ProfitFactor">盈亏比 / Profit factor.</param>
/// <param name="TotalCommissionUsd">总手续费（USD）/ Total commission in USD.</param>
public sealed record TradeSummary(int ClosedTradeCount, int WinningTradeCount, double WinRate, double ProfitFactor, decimal TotalCommissionUsd);
