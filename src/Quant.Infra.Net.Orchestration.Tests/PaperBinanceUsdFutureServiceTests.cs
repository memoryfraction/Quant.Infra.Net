using Binance.Net.Enums;
using Quant.Infra.Net.Orchestration.Execution;
using Quant.Infra.Net.Orchestration.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quant.Infra.Net.Orchestration.Tests;

/// <summary>
/// PaperBinanceUsdFutureService 单元测试：纯内存记账，零网络。
/// PaperBinanceUsdFutureService unit tests: pure in-memory accounting, zero network.
/// </summary>
[TestClass]
public class PaperBinanceUsdFutureServiceTests
{
    /// <summary>
    /// 默认初始权益 10000。
    /// Default initial equity is 10000.
    /// </summary>
    [TestMethod]
    public async Task InitialBalance_DefaultsTo10000()
    {
        var svc = new PaperBinanceUsdFutureService();
        var balance = await svc.GetusdFutureAccountBalanceAsync();
        Assert.AreEqual(10000m, balance);
        Assert.IsFalse(await svc.HasUsdFuturePositionAsync("BTCUSDT"));
    }

    /// <summary>
    /// 初始权益取自 OrchestrationOptions.InitialEquityUsd。
    /// Initial equity comes from OrchestrationOptions.InitialEquityUsd.
    /// </summary>
    [TestMethod]
    public async Task InitialBalance_FromOptions()
    {
        var options = new OrchestrationOptions { InitialEquityUsd = 5000m };
        var svc = new PaperBinanceUsdFutureService(options);
        Assert.AreEqual(5000m, await svc.GetusdFutureAccountBalanceAsync());
    }

    /// <summary>
    /// 开仓 30% 多头：持仓存在、名义 3000、未实现盈亏为 0（标记价 = 入场价）。
    /// Opening a 30% long: position exists, notional ≈ 3000, unrealized PnL is 0 (mark == entry).
    /// </summary>
    [TestMethod]
    public async Task SetHoldingsLong_30Percent_OpenPositionCorrect()
    {
        var svc = new PaperBinanceUsdFutureService();
        svc.SetMarkPrice("BTCUSDT", 100d);
        await svc.SetUsdFutureHoldingsAsync("BTCUSDT", 0.3d, PositionSide.Long);

        Assert.IsTrue(await svc.HasUsdFuturePositionAsync("BTCUSDT"));
        var positions = (await svc.GetHoldingPositionAsync()).ToList();
        var btc = positions.Single(p => p.Symbol == "BTCUSDT");
        Assert.AreEqual(PositionSide.Long, btc.PositionSide);
        Assert.AreEqual(100m, btc.EntryPrice);
        Assert.AreEqual(100m, btc.MarkPrice);
        Assert.AreEqual(0m, btc.UnrealizedPnl);
        Assert.AreEqual(3000m, btc.Notional, 1m);
        Assert.AreEqual(10000m, await svc.GetusdFutureAccountBalanceAsync());
    }

    /// <summary>
    /// 多头浮盈：标记价 110 → 权益 +300（+3%）。
    /// Long unrealized gain: mark 110 → equity +300 (+3%).
    /// </summary>
    [TestMethod]
    public async Task LongUnrealizedGain_EquityIncreases()
    {
        var svc = new PaperBinanceUsdFutureService();
        svc.SetMarkPrice("BTCUSDT", 100d);
        await svc.SetUsdFutureHoldingsAsync("BTCUSDT", 0.3d, PositionSide.Long);
        svc.SetMarkPrice("BTCUSDT", 110d);

        Assert.AreEqual(10300m, await svc.GetusdFutureAccountBalanceAsync());
        Assert.AreEqual(0.03, await svc.GetusdFutureUnrealizedProfitRateAsync(), 1e-9);
    }

    /// <summary>
    /// 空头浮盈：标记价 90 → 空头获利 +300。
    /// Short unrealized gain: mark 90 → short gains +300.
    /// </summary>
    [TestMethod]
    public async Task ShortUnrealizedGain_EquityIncreases()
    {
        var svc = new PaperBinanceUsdFutureService();
        svc.SetMarkPrice("ETHUSDT", 100d);
        await svc.SetUsdFutureHoldingsAsync("ETHUSDT", 0.3d, PositionSide.Short);
        Assert.IsFalse(await svc.HasUsdFuturePositionAsync("BTCUSDT"));
        svc.SetMarkPrice("ETHUSDT", 90d);

        Assert.IsTrue(await svc.HasUsdFuturePositionAsync("ETHUSDT"));
        Assert.AreEqual(10300m, await svc.GetusdFutureAccountBalanceAsync());
    }

    /// <summary>
    /// 平仓：持仓清零，已实现盈亏入账。
    /// Liquidation: position clears; realized PnL booked.
    /// </summary>
    [TestMethod]
    public async Task Liquidate_ClearsPositionAndBooksRealizedPnl()
    {
        var svc = new PaperBinanceUsdFutureService();
        svc.SetMarkPrice("BTCUSDT", 100d);
        await svc.SetUsdFutureHoldingsAsync("BTCUSDT", 0.3d, PositionSide.Long);
        svc.SetMarkPrice("BTCUSDT", 120d);
        await svc.LiquidateUsdFutureAsync("BTCUSDT");

        Assert.IsFalse(await svc.HasUsdFuturePositionAsync("BTCUSDT"));
        Assert.AreEqual(0, (await svc.GetHoldingPositionAsync()).Count());
        Assert.AreEqual(10600m, await svc.GetusdFutureAccountBalanceAsync()); // 3000 × 20%
        Assert.AreEqual(0d, await svc.GetusdFutureUnrealizedProfitRateAsync());
    }

    /// <summary>
    /// 参数校验：rate 越界 / symbol 空白 → ArgumentException。
    /// Validation: out-of-range rate / blank symbol → ArgumentException.
    /// </summary>
    [TestMethod]
    public void InvalidParameters_Throw()
    {
        var svc = new PaperBinanceUsdFutureService();
        Assert.ThrowsException<ArgumentException>(() => svc.SetUsdFutureHoldingsAsync("BTCUSDT", 1.2d));
        Assert.ThrowsException<ArgumentException>(() => svc.SetUsdFutureHoldingsAsync(" ", 0.1d));
        Assert.ThrowsException<ArgumentException>(() => svc.LiquidateUsdFutureAsync(" "));
        Assert.ThrowsException<ArgumentException>(() => svc.SetMarkPrice("BTCUSDT", 0d));
    }

    /// <summary>
    /// GetOhlcvListAsync 返回空数据（Paper 零网络）。
    /// GetOhlcvListAsync returns empty data (Paper: zero network).
    /// </summary>
    [TestMethod]
    public async Task GetOhlcv_ReturnsEmptyWithoutNetwork()
    {
        var svc = new PaperBinanceUsdFutureService();
        var now = DateTime.UtcNow;
        var ohlcvs = await svc.GetOhlcvListAsync("BTCUSDT", now.AddDays(-1), now);
        Assert.AreEqual("BTCUSDT", ohlcvs.Symbol);
        Assert.AreEqual(0, ohlcvs.OhlcvSet.Count);
    }

    /// <summary>
    /// 持仓模式读写不抛异常。
    /// Position-mode get/set must not throw.
    /// </summary>
    [TestMethod]
    public async Task PositionMode_RoundTrip()
    {
        var svc = new PaperBinanceUsdFutureService();
        await svc.SetPositionModeAsync(false);
        Assert.IsFalse(svc.IsHedgeMode);
        await svc.ShowPositionModeAsync();
        await svc.SetPositionModeAsync(true);
        Assert.IsTrue(svc.IsHedgeMode);
    }

    /// <summary>
    /// 重新开仓会先结算旧仓已实现盈亏。
    /// Re-opening settles the old position's realized PnL first.
    /// </summary>
    [TestMethod]
    public async Task Reopen_SettlesOldPositionRealizedPnl()
    {
        var svc = new PaperBinanceUsdFutureService();
        svc.SetMarkPrice("BTCUSDT", 100d);
        await svc.SetUsdFutureHoldingsAsync("BTCUSDT", 0.3d, PositionSide.Long);
        svc.SetMarkPrice("BTCUSDT", 110d); // +300 未实现
        await svc.SetUsdFutureHoldingsAsync("BTCUSDT", 0.2d, PositionSide.Long); // 旧仓 110 平仓（+300 已实现），新仓入场 110

        var positions = (await svc.GetHoldingPositionAsync()).ToList();
        var btc = positions.Single(p => p.Symbol == "BTCUSDT");
        Assert.AreEqual(110m, btc.EntryPrice);
        Assert.AreEqual(2060m, btc.Notional, 1m); // 0.2 × (10000 + 300 已实现)
        Assert.AreEqual(10300m, await svc.GetusdFutureAccountBalanceAsync());
    }
}
