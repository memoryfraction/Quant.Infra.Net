using Binance.Net.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Backtest.Broker;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.Orchestration;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Backtest.Tests;

/// <summary>
/// B3：<see cref="BacktestRunner"/> 行为 — 曲线长度/走向、风控拒单时曲线持平、
/// NextBarOpen 填充于下一根开盘、MaCross 全程无异常（两种 FillTiming 各一次）。
/// B3: BacktestRunner behavior — curve length/direction, flat curve on risk rejection,
/// NextBarOpen fills at the next bar's open, MaCross runs end-to-end without exceptions (both FillTimings).
/// </summary>
[TestClass]
public sealed class BacktestRunnerTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static HistoricalDataSet Up(int bars, string symbol = "AAPL", double start = 100d)
    {
        var list = new List<Ohlcv>();
        for (var i = 0; i < bars; i++)
        {
            list.Add(TestBars.Bar(symbol, T0.AddDays(i), (decimal)(start + i)));
        }

        return new HistoricalDataSet(new Dictionary<string, IReadOnlyList<Ohlcv>>
        {
            [symbol] = list,
        });
    }

    private static HistoricalDataSet Down(int bars, string symbol = "AAPL", double start = 120d)
    {
        var list = new List<Ohlcv>();
        for (var i = 0; i < bars; i++)
        {
            list.Add(TestBars.Bar(symbol, T0.AddDays(i), (decimal)(start - i))); // 时间轴上每根 -1 / each bar -1 along the timeline
        }

        return new HistoricalDataSet(new Dictionary<string, IReadOnlyList<Ohlcv>>
        {
            [symbol] = list,
        });
    }

    private static BacktestRunner BuildRunner(
        BacktestOptions backtestOptions,
        Action<OrchestrationOptions>? configureOrch = null,
        Action<IServiceCollection>? extraRegistrations = null)
    {
        var broker = new BacktestBrokerService(backtestOptions);
        var services = new ServiceCollection();
        services.TryAddSingleton<IAnalysisService, AnalysisService>();
        services.AddSingleton(broker);
        services.AddSingleton<IBinanceUsdFutureService>(_ => broker); // TryAdd 之前注册 → 覆盖 Paper 默认 / registered before the TryAdd default

        extraRegistrations?.Invoke(services);

        services.AddQuantInfraNetOrchestration(o =>
        {
            o.Environment = ExchangeEnvironment.Paper;
            o.Parameters["Symbol"] = "AAPL";
            o.Parameters["Strategy"] = "MaCross";
            o.Parameters["FastPeriod"] = "1";
            o.Parameters["SlowPeriod"] = "5";
            configureOrch?.Invoke(o);
        });

        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<StrategyPipeline>();
        var orchestrationOptions = provider.GetRequiredService<OrchestrationOptions>();
        return new BacktestRunner(pipeline, broker, orchestrationOptions, backtestOptions);
    }

    [TestMethod]
    public async Task MaCross_Uptrend_SameBarClose_CurveLengthAndDirection()
    {
        var data = Up(30);
        var runner = BuildRunner(new BacktestOptions
        {
            InitialEquityUsd = 10000m,
            WarmupBars = 10,
        });

        var result = await runner.RunAsync(data, new[] { "AAPL" });

        // 长度 = 时间轴 − WarmupBars / length = timeline − WarmupBars
        Assert.AreEqual(20, result.EquityCurve.Count);

        var ordered = result.EquityCurve.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();

        // 走向：多头开仓后价格持续上涨 → 曲线从期初 10000 起严格递增
        // Direction: long position in an uptrend → strictly increasing from the initial 10000.
        Assert.AreEqual(10000m, ordered[0]);
        for (var i = 1; i < ordered.Count; i++)
        {
            Assert.IsTrue(ordered[i] > ordered[i - 1],
                $"curve must be strictly increasing at index {i}: {ordered[i - 1]} -> {ordered[i]}");
        }

        // 首笔成交=第 11 根（idx 10）收盘 110（SameBarClose 填充锚点）/ first trade fills at bar idx 10 close = 110
        Assert.IsTrue(result.Trades.Count >= 1);
        Assert.AreEqual(110m, result.Trades[0].FillPrice);
        Assert.AreEqual(Binance.Net.Enums.PositionSide.Long, result.Trades[0].Side);

        Assert.IsTrue(result.RunEvents.Count > 0);
    }

    [TestMethod]
    public async Task MaCross_Uptrend_NextBarOpen_FillsAtTheNextBarOpen()
    {
        var data = Up(12);
        var runner = BuildRunner(new BacktestOptions
        {
            InitialEquityUsd = 10000m,
            WarmupBars = 5,
            FillTiming = FillTiming.NextBarOpen,
        });

        var result = await runner.RunAsync(data, new[] { "AAPL" });

        Assert.IsTrue(result.Trades.Count >= 1, "a long must have been queued and filled at the next bar's open");

        // §7.1 示例语义：bar idx5（收盘 105）的信号 → bar idx6 开盘 106 成交
        // Section 7.1 semantics: the signal at bar idx 5 (close 105) fills at bar idx 6 open = 106,
        // NOT at the signal bar's own close (105).
        Assert.AreEqual(106m, result.Trades[0].FillPrice);
        Assert.AreEqual(data.Timeline[6], result.Trades[0].TimestampUtc);

        Assert.AreEqual(7, result.EquityCurve.Count);
    }

    [TestMethod]
    public async Task RiskRejection_EquityCurveStaysFlat_WithNoTrades()
    {
        var data = Up(30);
        var runner = BuildRunner(
            new BacktestOptions { InitialEquityUsd = 10000m, WarmupBars = 10 },
            extraRegistrations: services => services.TryAddSingleton<IRiskManager, AlwaysRejectRiskManager>());

        var result = await runner.RunAsync(data, new[] { "AAPL" });

        Assert.AreEqual(20, result.EquityCurve.Count);
        foreach (var equity in result.EquityCurve.Values)
        {
            Assert.AreEqual(10000m, equity, "risk-rejected bars must never move equity");
        }

        Assert.AreEqual(0, result.Trades.Count);
        Assert.IsTrue(result.RunEvents.Any(e => e.Message.Contains("risk check REJECTED")),
            "the run events must record the risk rejection");
    }

    [TestMethod]
    public async Task MaCross_Downtrend_AllowShortOff_StaysFlatWithNoTrades()
    {
        var data = Down(30);
        var runner = BuildRunner(new BacktestOptions
        {
            InitialEquityUsd = 10000m,
            WarmupBars = 10,
        });

        var result = await runner.RunAsync(data, new[] { "AAPL" });

        // Fast < Slow → Direction=Flat（AllowShort=false）→ 目标权重 0 → 无交易、权益持平
        // Fast < Slow → Flat (shorting off) → zero target weight → no trades, flat equity.
        Assert.AreEqual(20, result.EquityCurve.Count);
        foreach (var equity in result.EquityCurve.Values)
        {
            Assert.AreEqual(10000m, equity);
        }

        Assert.AreEqual(0, result.Trades.Count);
    }

    [TestMethod]
    public async Task WarmupLargerThanTimeline_YieldsAnEmptyCurveWithoutThrowing()
    {
        var data = Up(5);
        var runner = BuildRunner(new BacktestOptions
        {
            InitialEquityUsd = 10000m,
            WarmupBars = 50,
        });

        var result = await runner.RunAsync(data, new[] { "AAPL" });

        Assert.AreEqual(0, result.EquityCurve.Count);
        Assert.AreEqual(0, result.Trades.Count);
    }

    private sealed class AlwaysRejectRiskManager : IRiskManager
    {
        public Task<RiskAssessment> AssessAsync(IReadOnlyList<TargetPosition> targets, PortfolioSnapshot current, CancellationToken ct)
        {
            var assessment = new RiskAssessment { Approved = false };
            assessment.Reasons.Add("forced rejection (B3 test)");
            return Task.FromResult(assessment);
        }
    }
}
