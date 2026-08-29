using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Backtest.Broker;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Backtest.Reporting;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.Backtest.Sweep;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Backtest.Tests;

/// <summary>
/// B4：<see cref="ParameterSweepRunner"/> — 3×3 参数网格返回 9 条互不干扰的独立结果（§9 B4），
/// 外加 <see cref="BacktestMetrics"/> 装配验收。
/// B4: ParameterSweepRunner — a 3×3 grid returns 9 mutually independent results (section 9 B4),
/// plus BacktestMetrics assembly acceptance.
/// </summary>
[TestClass]
public sealed class ParameterSweepRunnerTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static HistoricalDataSet Up(int bars)
    {
        var series = new List<Ohlcv>();
        for (var i = 0; i < bars; i++)
        {
            series.Add(TestBars.Bar("AAPL", T0.AddDays(i), (decimal)(100 + i))); // 持续上涨 / steady uptrend
        }

        return new HistoricalDataSet(new Dictionary<string, IReadOnlyList<Ohlcv>> { ["AAPL"] = series });
    }

    [TestMethod]
    public void Grid3x3_ReturnsNineIndependentResults()
    {
        var data = Up(30);
        var sweep = new ParameterSweepRunner(
            data,
            new[] { "AAPL" },
            new BacktestOptions { InitialEquityUsd = 10000m, WarmupBars = 10 },
            o =>
            {
                o.Environment = ExchangeEnvironment.Paper;
                o.Parameters["Symbol"] = "AAPL";
                o.Parameters["Strategy"] = "MaCross";
            });

        var fast = new[] { 1, 2, 3 };
        var slow = new[] { 5, 10, 15 };
        var grid = new List<IReadOnlyDictionary<string, string>>();
        foreach (var f in fast)
        {
            foreach (var s in slow)
            {
                grid.Add(new Dictionary<string, string>
                {
                    ["FastPeriod"] = f.ToString(),
                    ["SlowPeriod"] = s.ToString(),
                });
            }
        }

        var results = sweep.RunAsync(grid, maxDegreeOfParallelism: 3).GetAwaiter().GetResult();

        // 9 个点，网格顺序 / 9 points, in grid order
        Assert.AreEqual(9, results.Count);

        // 每点参数坐标正确（网格值覆盖基线，无串点）/ each point carries its own grid coordinates (no cross-contamination)
        for (var i = 0; i < 9; i++)
        {
            Assert.AreEqual(grid[i]["FastPeriod"], results[i].Parameters["FastPeriod"], $"point {i} FastPeriod");
            Assert.AreEqual(grid[i]["SlowPeriod"], results[i].Parameters["SlowPeriod"], $"point {i} SlowPeriod");
        }

        for (var i = 0; i < 9; i++)
        {
            var backtest = results[i].Backtest;

            // 每点都完整独立跑通：曲线长度固定，且起点权益恒为初始值 ⇒ 无共享状态
            // Every point runs fully and independently: fixed curve length, and the FIRST bar's equity is
            // always the initial value ⇒ no broker state leaks across grid points.
            var ordered = backtest.EquityCurve.OrderBy(kvp => kvp.Key).ToList();
            Assert.AreEqual(20, ordered.Count, $"point {i} curve length");
            Assert.AreEqual(10000m, ordered[0].Value, $"point {i} first equity must be the initial value");

            // 每点指标装配一致 / per-point metrics assembly is consistent
            Assert.AreEqual(backtest.Trades.Count, backtest.Metrics.TotalTrades, $"point {i} TotalTrades");
            var stats = TradeStatistics.Compute(backtest.Trades);
            Assert.AreEqual(stats.WinRate, backtest.Metrics.WinRate, $"point {i} WinRate");

            // 每点至少产生一笔成交（趋势 + MaCross ⇒ Long 信号落单）/ each point trades at least once (trend + MaCross ⇒ filled Long signals)
            Assert.IsTrue(backtest.Trades.Count > 0, $"point {i} should trade");
        }
    }

    [TestMethod]
    public void SamePointTwice_IsDeterministic()
    {
        var data = Up(20);
        var baseOptions = new BacktestOptions { InitialEquityUsd = 10000m, WarmupBars = 5 };
        var makeSweep = () => new ParameterSweepRunner(
            data,
            new[] { "AAPL" },
            baseOptions,
            o =>
            {
                o.Environment = ExchangeEnvironment.Paper;
                o.Parameters["Symbol"] = "AAPL";
                o.Parameters["Strategy"] = "MaCross";
                o.Parameters["FastPeriod"] = "2";
                o.Parameters["SlowPeriod"] = "10";
            });

        var single = new Dictionary<string, string> { ["FastPeriod"] = "2", ["SlowPeriod"] = "10" };

        var first = makeSweep().RunAsync(new[] { single as IReadOnlyDictionary<string, string> }, 1).GetAwaiter().GetResult()[0];
        var second = makeSweep().RunAsync(new[] { single as IReadOnlyDictionary<string, string> }, 1).GetAwaiter().GetResult()[0];

        var firstValues = first.Backtest.EquityCurve.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();
        var secondValues = second.Backtest.EquityCurve.OrderBy(kvp => kvp.Key).Select(kvp => kvp.Value).ToList();

        CollectionAssertEx.Equal(firstValues, secondValues);
        Assert.AreEqual(first.Backtest.Trades.Count, second.Backtest.Trades.Count);
        Assert.AreEqual(first.Backtest.Metrics.Cagr, second.Backtest.Metrics.Cagr);
        Assert.AreEqual(first.Backtest.Metrics.MaxDrawdown, second.Backtest.Metrics.MaxDrawdown);
    }

    [TestMethod]
    public void RunnerMetrics_AreAssembledFromCurveAndTrades()
    {
        var data = Up(30);
        var service = new BacktestBrokerService(new BacktestOptions { InitialEquityUsd = 10000m, WarmupBars = 10 });
        var services = new ServiceCollection();
        services.TryAddSingleton<IAnalysisService, AnalysisService>();
        services.AddSingleton(service);
        services.AddSingleton<IBinanceUsdFutureService>(_ => service);
        services.AddQuantInfraNetOrchestration(o =>
        {
            o.Environment = ExchangeEnvironment.Paper;
            o.Parameters["Symbol"] = "AAPL";
            o.Parameters["Strategy"] = "MaCross";
            o.Parameters["FastPeriod"] = "1";
            o.Parameters["SlowPeriod"] = "10"; // 默认 SlowPeriod=200 会无信号 ⇒ 显式指定 / default 200 would never signal, so set explicitly
        });

        using var provider = services.BuildServiceProvider();
        var runner = new BacktestRunner(
            provider.GetRequiredService<Quant.Infra.Net.Orchestration.Pipeline.StrategyPipeline>(),
            service,
            provider.GetRequiredService<OrchestrationOptions>(),
            new BacktestOptions { InitialEquityUsd = 10000m, WarmupBars = 10 });

        var result = runner.RunAsync(data, new[] { "AAPL" }).GetAwaiter().GetResult();

        // 指标非默认：曲线 20 点、上涨 ⇒ CAGR>0；成交 ≥1 ⇒ TotalTrades>0；回撤 ≤ 0（§7.2 负值或零）
        // Metrics are non-default: 20 curve points on an uptrend ⇒ CAGR > 0; ≥ 1 trade ⇒ TotalTrades > 0; drawdown ≤ 0 (negative or zero per section 7.2).
        Assert.AreEqual(20, result.EquityCurve.Count);
        Assert.IsTrue(result.Trades.Count > 0);
        Assert.AreEqual(result.Trades.Count, result.Metrics.TotalTrades);
        Assert.IsTrue(result.Metrics.Cagr > 0m, $"CAGR expected > 0 on uptrend, got {result.Metrics.Cagr}");
        Assert.IsTrue(result.Metrics.MaxDrawdown <= 0m);

        // 胜率口径与 TradeStatistics 完全一致 / WinRate matches TradeStatistics exactly
        Assert.AreEqual(TradeStatistics.Compute(result.Trades).WinRate, result.Metrics.WinRate);
    }

    private static class CollectionAssertEx
    {
        public static void Equal(IReadOnlyList<decimal> a, IReadOnlyList<decimal> b)
        {
            Assert.AreEqual(a.Count, b.Count);
            for (var i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i], b[i], $"index {i}");
            }
        }
    }
}
