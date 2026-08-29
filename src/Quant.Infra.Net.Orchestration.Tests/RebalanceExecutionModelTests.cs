using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Execution;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quant.Infra.Net.Orchestration.Tests;

/// <summary>
/// 测试用：SetUsdFutureHoldingsAsync 固定抛异常的券商包装（其余委托给内部 Paper 服务）。
/// Test double: SetUsdFutureHoldingsAsync always throws; everything else delegates to an inner Paper service.
/// </summary>
internal sealed class FailingSetBinanceBroker : IBinanceUsdFutureService
{
    private readonly PaperBinanceUsdFutureService _inner;

    public FailingSetBinanceBroker()
    {
        _inner = new PaperBinanceUsdFutureService();
    }

    public ExchangeEnvironment ExchangeEnvironment { get; set; } = ExchangeEnvironment.Paper;
    public Task<Ohlcvs> GetOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt, ResolutionLevel resolutionLevel = ResolutionLevel.Hourly)
        => _inner.GetOhlcvListAsync(symbol, startDt, endDt, resolutionLevel);
    public Task<IEnumerable<string>> GetUsdFutureSymbolsAsync() => _inner.GetUsdFutureSymbolsAsync();
    public Task<IEnumerable<BinancePositionDetailsUsdt>> GetHoldingPositionAsync() => _inner.GetHoldingPositionAsync();
    public Task<decimal> GetusdFutureAccountBalanceAsync() => _inner.GetusdFutureAccountBalanceAsync();
    public Task<double> GetusdFutureUnrealizedProfitRateAsync() => _inner.GetusdFutureUnrealizedProfitRateAsync();
    public Task LiquidateUsdFutureAsync(string symbol) => _inner.LiquidateUsdFutureAsync(symbol);
    public Task SetUsdFutureHoldingsAsync(string symbol, double rate, PositionSide positionSide = PositionSide.Both)
        => throw new InvalidOperationException("broker unavailable (fake failure)");
    public Task<bool> HasUsdFuturePositionAsync(string symbol) => _inner.HasUsdFuturePositionAsync(symbol);
    public Task ShowPositionModeAsync() => Task.CompletedTask;
    public Task SetPositionModeAsync(bool isHedgeMode = true) => Task.CompletedTask;
}

/// <summary>
/// RebalanceExecutionModel 单元测试（注入 PaperBinanceUsdFutureService，零网络）。
/// RebalanceExecutionModel unit tests (inject PaperBinanceUsdFutureService; zero network).
/// </summary>
[TestClass]
public class RebalanceExecutionModelTests
{
    private static (RebalanceExecutionModel Model, PaperBinanceUsdFutureService Paper, OrchestrationOptions Options) NewModel(
        double minRebalanceDelta = 0.02d)
    {
        var options = new OrchestrationOptions { MinRebalanceDelta = minRebalanceDelta };
        var paper = new PaperBinanceUsdFutureService(options);
        var model = new RebalanceExecutionModel(new BinanceUsdFutureExecutionBrokerAdapter(paper), options);
        return (model, paper, options);
    }

    /// <summary>
    /// 开仓：目标 +0.3 → 多头持仓，权重 ≈ +0.3。
    /// Open position: target +0.3 → long position with weight ≈ +0.3.
    /// </summary>
    [TestMethod]
    public async Task OpenLong_PositionAndWeightCorrect()
    {
        var (model, paper, _) = NewModel();
        paper.SetMarkPrice("AAPL", 100d);
        var targets = new[]
        {
            new TargetPosition { Symbol = "AAPL", TargetWeight = 0.3d }
        };

        var reports = await model.RebalanceAsync(targets, CancellationToken.None);

        Assert.AreEqual(1, reports.Count);
        Assert.IsTrue(reports[0].Success);
        Assert.IsFalse(await paper.HasUsdFuturePositionAsync("MSFT"));
        Assert.IsTrue(await paper.HasUsdFuturePositionAsync("AAPL"));
        Assert.AreEqual(0.3, reports[0].CurrentWeight, 1e-6);
        var positions = (await paper.GetHoldingPositionAsync()).ToList();
        Assert.AreEqual(PositionSide.Long, positions.Single(p => p.Symbol == "AAPL").PositionSide);
    }

    /// <summary>
    /// 死区：|Δ| &lt; MinRebalanceDelta → 不调仓（名义持仓不变）。
    /// Dead zone: |delta| &lt; MinRebalanceDelta → no trade (notional unchanged).
    /// </summary>
    [TestMethod]
    public async Task DeadZone_SkipsRebalance()
    {
        var (model, paper, _) = NewModel(minRebalanceDelta: 0.05d);
        paper.SetMarkPrice("AAPL", 100d);

        await model.RebalanceAsync(new[] { new TargetPosition { Symbol = "AAPL", TargetWeight = 0.3d } }, CancellationToken.None);
        var before = (await paper.GetHoldingPositionAsync()).Single(p => p.Symbol == "AAPL").Notional;

        var reports = await model.RebalanceAsync(new[] { new TargetPosition { Symbol = "AAPL", TargetWeight = 0.32d } }, CancellationToken.None);

        var after = (await paper.GetHoldingPositionAsync()).Single(p => p.Symbol == "AAPL").Notional;
        Assert.AreEqual(before, after);
        Assert.IsTrue(reports[0].Success);
        Assert.AreEqual(0.3, reports[0].CurrentWeight, 1e-9);
    }

    /// <summary>
    /// 目标 0 → 走 Liquidate 平仓。
    /// Target 0 → liquidate path.
    /// </summary>
    [TestMethod]
    public async Task ZeroTarget_Liquidates()
    {
        var (model, paper, _) = NewModel();
        paper.SetMarkPrice("AAPL", 100d);
        await model.RebalanceAsync(new[] { new TargetPosition { Symbol = "AAPL", TargetWeight = 0.3d } }, CancellationToken.None);

        var reports = await model.RebalanceAsync(new[] { new TargetPosition { Symbol = "AAPL", TargetWeight = 0.0d } }, CancellationToken.None);

        Assert.IsFalse(await paper.HasUsdFuturePositionAsync("AAPL"));
        Assert.IsTrue(reports[0].Success);
        Assert.AreEqual(0d, reports[0].CurrentWeight, 1e-9);
    }

    /// <summary>
    /// 空头：目标 −0.2 → Short 持仓。
    /// Short: target −0.2 → Short position.
    /// </summary>
    [TestMethod]
    public async Task NegativeTarget_OpensShort()
    {
        var (model, paper, _) = NewModel();
        paper.SetMarkPrice("ETHUSDT", 100d);

        var reports = await model.RebalanceAsync(new[] { new TargetPosition { Symbol = "ETHUSDT", TargetWeight = -0.2d } }, CancellationToken.None);

        Assert.IsTrue(reports[0].Success);
        var position = (await paper.GetHoldingPositionAsync()).Single(p => p.Symbol == "ETHUSDT");
        Assert.AreEqual(PositionSide.Short, position.PositionSide);
        Assert.AreEqual(-0.2, reports[0].CurrentWeight, 1e-6);
    }

    /// <summary>
    /// 券商调用失败 → 该标的 Success=false 且带错误信息（不抛出）。
    /// Broker call failure → that symbol reports Success=false with a message (does not throw).
    /// </summary>
    [TestMethod]
    public async Task BrokerSetFails_ReportMarkedFailed()
    {
        var options = new OrchestrationOptions();
        var broker = new FailingSetBinanceBroker();
        var model = new RebalanceExecutionModel(new BinanceUsdFutureExecutionBrokerAdapter(broker), options);

        var reports = await model.RebalanceAsync(new[] { new TargetPosition { Symbol = "AAPL", TargetWeight = 0.3d } }, CancellationToken.None);

        Assert.AreEqual(1, reports.Count);
        Assert.IsFalse(reports[0].Success);
        StringAssert.Contains(reports[0].ErrorMessage!, "broker unavailable");
    }

    /// <summary>
    /// 参数校验：null targets / null 条目 / 空白 symbol → 异常。
    /// Validation: null targets / null entries / blank symbols → exceptions.
    /// </summary>
    [TestMethod]
    public void InvalidArguments_Throw()
    {
        var (model, _, _) = NewModel();
        Assert.ThrowsException<ArgumentNullException>(() => model.RebalanceAsync(null!, CancellationToken.None).GetAwaiter().GetResult());
        Assert.ThrowsException<ArgumentNullException>(() => model.RebalanceAsync(new TargetPosition[] { null! }, CancellationToken.None).GetAwaiter().GetResult());
        Assert.ThrowsException<ArgumentException>(() => model.RebalanceAsync(new[] { new TargetPosition { Symbol = " " } }, CancellationToken.None).GetAwaiter().GetResult());
    }
}
