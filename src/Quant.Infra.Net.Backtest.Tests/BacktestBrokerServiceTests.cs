using Binance.Net.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Backtest.Broker;
using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Orchestration.Execution;

namespace Quant.Infra.Net.Backtest.Tests;

/// <summary>
/// B2：<see cref="BacktestBrokerService"/> 行为——开/平仓权益变化、零成本与 Paper 逐式一致、
/// 非零手续费/滑点方向、Trades 记录完整性。
/// B2: BacktestBrokerService behavior — open/close equity changes, zero-cost parity with Paper,
/// non-zero commission/slippage directions, and complete trade records.
/// </summary>
[TestClass]
public sealed class BacktestBrokerServiceTests
{
    private static BacktestBrokerService NewBroker(
        decimal initialEquityUsd = 10000m,
        decimal commissionBps = 0m,
        decimal slippageBps = 0m)
        => new(new BacktestOptions
        {
            InitialEquityUsd = initialEquityUsd,
            CommissionBps = commissionBps,
            SlippageBps = slippageBps,
        });

    [TestMethod]
    public async Task OpenLong_EquityTracksUnrealizedPnlAgainstMark()
    {
        var broker = NewBroker();
        broker.SetMarkPrice("AAA", 100d);

        await broker.SetUsdFutureHoldingsAsync("AAA", 0.3d, PositionSide.Long);

        // 开仓瞬间：入场价=标记价 → 无浮盈浮亏 / at open: entry = mark → zero unrealized P/L
        Assert.AreEqual(10000m, broker.CurrentEquityUsd);
        Assert.IsTrue(await broker.HasUsdFuturePositionAsync("AAA"));

        var position = (await broker.GetHoldingPositionAsync()).Single();
        Assert.AreEqual(30m, position.Quantity);
        Assert.AreEqual(PositionSide.Long, position.PositionSide);
        Assert.AreEqual(100m, position.EntryPrice);

        // 标记价上涨 → 权益上涨 / mark moves up → equity moves up
        broker.SetMarkPrice("AAA", 110d);
        Assert.AreEqual(10300m, broker.CurrentEquityUsd);
    }

    [TestMethod]
    public async Task Liquidate_RealizesPnlThenEquityFreezes()
    {
        var broker = NewBroker();
        broker.SetMarkPrice("AAA", 100d);
        await broker.SetUsdFutureHoldingsAsync("AAA", 0.3d, PositionSide.Long);
        broker.SetMarkPrice("AAA", 110d);
        await broker.LiquidateUsdFutureAsync("AAA");

        // 平仓后盈亏落袋 / after liquidation the P/L is realized
        Assert.AreEqual(10300m, broker.CurrentEquityUsd);
        Assert.IsFalse(await broker.HasUsdFuturePositionAsync("AAA"));

        // 空仓后市场继续波动不再影响权益 / with a flat position further price moves change nothing
        broker.SetMarkPrice("AAA", 130d);
        Assert.AreEqual(10300m, broker.CurrentEquityUsd);
    }

    [TestMethod]
    public async Task ZeroCost_OperationSequence_EquityIsStatementIdenticalToPaper()
    {
        // D4 锚点：零手续费零滑点时，双方对同一操作序列产生完全一致的权益序列。
        // D4 anchor: at zero commission and zero slippage both sides produce identical equity
        // sequences for the same operation stream.
        var paper = new PaperBinanceUsdFutureService(new Quant.Infra.Net.Orchestration.Models.OrchestrationOptions
        {
            InitialEquityUsd = 10000m,
        });
        var backtest = NewBroker();

        void MarkAll(double px)
        {
            paper.SetMarkPrice("AAA", px);
            backtest.SetMarkPrice("AAA", px);
        }

        MarkAll(100d);
        await OpAndCompare(paper, backtest, () => paper.SetUsdFutureHoldingsAsync("AAA", 0.3d, PositionSide.Long),
            () => backtest.SetUsdFutureHoldingsAsync("AAA", 0.3d, PositionSide.Long));

        MarkAll(105d);
        await OpAndCompare(paper, backtest, () => paper.SetUsdFutureHoldingsAsync("AAA", 0.5d, PositionSide.Short),
            () => backtest.SetUsdFutureHoldingsAsync("AAA", 0.5d, PositionSide.Short));

        MarkAll(95d);
        await OpAndCompare(paper, backtest, () => paper.SetUsdFutureHoldingsAsync("AAA", 0.4d, PositionSide.Long),
            () => backtest.SetUsdFutureHoldingsAsync("AAA", 0.4d, PositionSide.Long));

        await OpAndCompare(paper, backtest, () => paper.LiquidateUsdFutureAsync("AAA"),
            () => backtest.LiquidateUsdFutureAsync("AAA"));

        paper.SetMarkPrice("BBB", 102d);
        backtest.SetMarkPrice("BBB", 102d);
        await OpAndCompare(paper, backtest, () => paper.SetUsdFutureHoldingsAsync("BBB", 0.2d, PositionSide.Short),
            () => backtest.SetUsdFutureHoldingsAsync("BBB", 0.2d, PositionSide.Short));

        // 回测侧还额外产出成交记录 / the backtest side additionally emits trade records
        Assert.AreEqual(5, backtest.Trades.Count);
    }

    private static async Task OpAndCompare(
        PaperBinanceUsdFutureService paper,
        BacktestBrokerService backtest,
        Func<Task> paperOp,
        Func<Task> backtestOp)
    {
        await paperOp();
        var paperEquity = await paper.GetusdFutureAccountBalanceAsync();
        var paperRate = await paper.GetusdFutureUnrealizedProfitRateAsync();

        await backtestOp();
        var backtestEquity = await backtest.GetusdFutureAccountBalanceAsync();
        var backtestRate = await backtest.GetusdFutureUnrealizedProfitRateAsync();

        Assert.AreEqual(paperEquity, backtestEquity);
        Assert.AreEqual(paper.CurrentEquityUsd, backtest.CurrentEquityUsd);
        Assert.AreEqual(paperRate, backtestRate, 1e-12);
    }

    [TestMethod]
    public async Task Commission_ChargedOnTradedNotional_DeductsEquity()
    {
        // 10 bps × 2000 USD 名义 = 2.0 USD / 10 bps on 2000 USD notional = 2.0 USD
        var broker = NewBroker(commissionBps: 10m);
        broker.SetMarkPrice("AAA", 100d);

        await broker.SetUsdFutureHoldingsAsync("AAA", 0.2d, PositionSide.Long);

        Assert.AreEqual(9998m, broker.CurrentEquityUsd);
        Assert.AreEqual(2m, broker.Trades[0].CommissionUsd);
    }

    [TestMethod]
    public async Task Slippage_LongOpen_IsAdverseAndPenalizesRoundTrip()
    {
        // 10 bps 滑点：买入成交价 = 100 × 1.001 = 100.1 / 10 bps slippage: long buy fills at 100 × 1.001 = 100.1
        var broker = NewBroker(slippageBps: 10m);
        broker.SetMarkPrice("AAA", 100d);

        await broker.SetUsdFutureHoldingsAsync("AAA", 0.2d, PositionSide.Long);

        Assert.AreEqual(100.1m, broker.Trades[0].FillPrice);

        // 价格回到 100（回到开仓标记价）：多头因不利滑点而小幅亏损 / price returns to the opening mark: the long loses a sliver
        broker.SetMarkPrice("AAA", 100d);
        Assert.IsTrue(broker.CurrentEquityUsd < 10000m);
        Assert.IsTrue(broker.CurrentEquityUsd > 9990m);
    }

    [TestMethod]
    public async Task Slippage_ShortOpen_IsAdverseSellSide()
    {
        // 10 bps 滑点：做空卖出成交价 = 100 × 0.999 = 99.9 / 10 bps slippage: short sell fills at 100 × 0.999 = 99.9
        var broker = NewBroker(slippageBps: 10m);
        broker.SetMarkPrice("AAA", 100d);

        await broker.SetUsdFutureHoldingsAsync("AAA", 0.2d, PositionSide.Short);

        Assert.AreEqual(99.9m, broker.Trades[0].FillPrice);
        Assert.IsTrue(broker.CurrentEquityUsd < 10000m, "Short opened at the adverse fill, then marked at 100, must show a loss.");
    }

    [TestMethod]
    public async Task Trades_CarrySimulatedTimeAndExactFields()
    {
        var now = new DateTime(2024, 5, 1, 8, 30, 0, DateTimeKind.Utc);
        var broker = NewBroker();
        broker.SetMarkPrice("AAA", 100d);
        broker.SimulatedNowUtc = now;

        await broker.SetUsdFutureHoldingsAsync("AAA", 0.25d, PositionSide.Long);
        await broker.LiquidateUsdFutureAsync("AAA");

        var opens = broker.Trades;
        Assert.AreEqual(2, opens.Count);

        var open = opens[0];
        Assert.AreEqual(now, open.TimestampUtc);
        Assert.AreEqual("AAA", open.Symbol);
        Assert.AreEqual(PositionSide.Long, open.Side);
        Assert.AreEqual(2500m, open.NotionalUsd);
        Assert.AreEqual(100m, open.FillPrice);
        Assert.AreEqual(0m, open.CommissionUsd);

        var close = opens[1];
        Assert.AreEqual(now, close.TimestampUtc);
        Assert.AreEqual(PositionSide.Short, close.Side);
        Assert.AreEqual(0m, close.NotionalUsd);
        Assert.AreEqual(100m, close.FillPrice);
    }

    [TestMethod]
    public async Task Liquidate_WithNoPosition_IsNoOpAndEmitsNoTrade()
    {
        var broker = NewBroker();

        await broker.LiquidateUsdFutureAsync("AAA");

        Assert.AreEqual(0, broker.Trades.Count);
        Assert.AreEqual(10000m, broker.CurrentEquityUsd);
    }
}
