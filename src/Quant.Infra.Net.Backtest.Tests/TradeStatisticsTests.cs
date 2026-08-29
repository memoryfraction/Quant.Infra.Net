using Binance.Net.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Backtest.Reporting;

namespace Quant.Infra.Net.Backtest.Tests;

/// <summary>
/// B4：<see cref="TradeStatistics"/> — 胜率/盈亏比/总手续费口径（§9 B4）。
/// B4: TradeStatistics — win rate / profit factor / total commission semantics (section 9 B4).
/// </summary>
[TestClass]
public sealed class TradeStatisticsTests
{
    private static readonly DateTime T0 = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);

    private static BacktestTrade T(int day, string symbol, PositionSide side, decimal fillPrice, decimal notionalUsd, decimal commissionUsd = 0m)
        => new()
        {
            TimestampUtc = T0.AddDays(day),
            Symbol = symbol,
            Side = side,
            FillPrice = fillPrice,
            NotionalUsd = notionalUsd,
            CommissionUsd = commissionUsd,
        };

    [TestMethod]
    public void MixedWinAndLoss_ComputesWinRateProfitFactorAndCommission()
    {
        // t1 多头 @100 开 1000；t2 平 t1（盈利 +100）并 @110 再开 1000；t3 @105.5 平 t2（亏损 −4500/110）
        // t1 long open @100 N=1000; t2 closes t1 (profit +100), reopens @110 N=1000; t3 closes t2 @105.5 (loss −4500/110).
        var trades = new List<BacktestTrade>
        {
            T(1, "AAA", PositionSide.Long, 100m, 1000m, 2m),
            T(2, "AAA", PositionSide.Long, 110m, 1000m, 3m),
            T(3, "AAA", PositionSide.Short, 105.5m, 0m, 0m),
        };

        var stats = TradeStatistics.Compute(trades);

        Assert.AreEqual(2, stats.ClosedTradeCount);
        Assert.AreEqual(1, stats.WinRate * 2); // 1/2
        Assert.AreEqual(1, stats.WinningTradeCount);

        // 盈亏比 = 100 / (1000×4.5/110) = 11000/4500 ≈ 2.4444
        // Profit factor = 100 / (1000×4.5/110) = 11000/4500 ≈ 2.4444
        Assert.AreEqual(11000d / 4500d, stats.ProfitFactor, 1e-6);

        Assert.AreEqual(5m, stats.TotalCommissionUsd);
    }

    [TestMethod]
    public void AllWins_ProfitFactorIsPositiveInfinity()
    {
        // 空头盈利：入场 100，90 平仓 → +200；无亏损 → 盈亏比 = +∞
        // Winning short: entry 100, exit 90 → +200; no losses → profit factor = +∞.
        var trades = new List<BacktestTrade>
        {
            T(1, "BBB", PositionSide.Short, 100m, 2000m),
            T(2, "BBB", PositionSide.Long, 90m, 0m),
        };

        var stats = TradeStatistics.Compute(trades);

        Assert.AreEqual(1, stats.ClosedTradeCount);
        Assert.AreEqual(1, stats.WinningTradeCount);
        Assert.AreEqual(1.0, stats.WinRate);
        Assert.AreEqual(double.PositiveInfinity, stats.ProfitFactor);
    }

    [TestMethod]
    public void MultiSymbol_StatesTrackPerSymbolIndependently()
    {
        var trades = new List<BacktestTrade>
        {
            T(1, "AAA", PositionSide.Long, 100m, 1000m),
            T(1, "BBB", PositionSide.Short, 200m, 1000m),
            T(2, "AAA", PositionSide.Short, 102m, 0m),   // AAA: +20（盈利）/ AAA: +20 (win)
            T(3, "BBB", PositionSide.Long, 210m, 0m),    // BBB: −50（亏损）/ BBB: −50 (loss)
        };

        var stats = TradeStatistics.Compute(trades);

        Assert.AreEqual(2, stats.ClosedTradeCount);
        Assert.AreEqual(1, stats.WinningTradeCount);
        Assert.AreEqual(0.5, stats.WinRate);
        Assert.AreEqual(20d / 50d, stats.ProfitFactor, 1e-9);
    }

    [TestMethod]
    public void OpenStateAtEnd_IsNotCountedAsClosed()
    {
        var trades = new List<BacktestTrade>
        {
            T(1, "AAA", PositionSide.Long, 100m, 1000m), // 从未平仓 / never closed
        };

        var stats = TradeStatistics.Compute(trades);

        Assert.AreEqual(0, stats.ClosedTradeCount);
        Assert.AreEqual(0, stats.WinningTradeCount);
        Assert.AreEqual(0d, stats.WinRate);
        Assert.AreEqual(0d, stats.ProfitFactor);
    }

    [TestMethod]
    public void NullOrEmpty_ReturnsZeroSummary()
    {
        var nullStats = TradeStatistics.Compute(null);
        Assert.AreEqual(0, nullStats.ClosedTradeCount);
        Assert.AreEqual(0d, nullStats.ProfitFactor);
        Assert.AreEqual(0m, nullStats.TotalCommissionUsd);

        var emptyStats = TradeStatistics.Compute(Array.Empty<BacktestTrade>());
        Assert.AreEqual(0, emptyStats.ClosedTradeCount);
        Assert.AreEqual(0d, emptyStats.WinRate);
    }
}
