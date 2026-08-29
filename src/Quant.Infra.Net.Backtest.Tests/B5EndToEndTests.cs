using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Backtest.Tests;

/// <summary>
/// B5 端到端：3 个内置策略各跑一次完整回测（§9 B5 验收：Metrics 非默认、Trades&gt;0、曲线长度 == 输入 bar 数）。
/// B5 end-to-end: one full backtest per built-in strategy (section 9 B5: non-default metrics, trades &gt; 0, curve length == input bars).
/// </summary>
[TestClass]
public sealed class B5EndToEndTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Ohlcv Bar(string symbol, int day, decimal price)
        => new()
        {
            Symbol = symbol,
            OpenDateTime = T0.AddDays(day),
            Open = price,
            High = price,
            Low = price,
            Close = price,
            Volume = 1m,
        };

    private static Quant.Infra.Net.Backtest.Models.BacktestResult Run(HistoricalDataSet data, IReadOnlyList<string> symbols, Action<Quant.Infra.Net.Orchestration.Models.OrchestrationOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddQuantInfraNetBacktest(b => b.InitialEquityUsd = 10000m, configure);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<BacktestRunner>().RunAsync(data, symbols).GetAwaiter().GetResult();
    }

    [TestMethod]
    public void MaCross_Uptrend_FullBacktestTrades()
    {
        var ups = new List<Ohlcv>();
        for (var i = 0; i < 30; i++)
        {
            ups.Add(Bar("AAPL", i, (decimal)(100 + i))); // 持续上涨 / steady uptrend
        }

        var data = new HistoricalDataSet(new Dictionary<string, IReadOnlyList<Ohlcv>> { ["AAPL"] = ups });
        var result = Run(
            data,
            new[] { "AAPL" },
            o =>
            {
                o.Parameters["Symbol"] = "AAPL";
                o.Parameters["Strategy"] = "MaCross";
                o.Parameters["FastPeriod"] = "1";
                o.Parameters["SlowPeriod"] = "5";
            });

        Assert.AreEqual(30, result.EquityCurve.Count);
        Assert.IsTrue(result.Trades.Count > 0, "MaCross on an uptrend must trade");
        Assert.AreEqual(result.Trades.Count, result.Metrics.TotalTrades);
        AssertMetricsNonDefault(result.Metrics);
        Assert.IsTrue(result.Metrics.Cagr > 0m, $"CAGR expected > 0 on an uptrend, got {result.Metrics.Cagr}");
    }

    [TestMethod]
    public void MeanReversion_Oscillation_FullBacktestTrades()
    {
        // 99/100/101 三值循环：99 → z≈−1.22 ⇒ Long；100 → |z|≈0 ⇒ Flat（平仓）⇒ 确定性地反复开/平仓
        // 99/100/101 cycle: 99 → z ≈ −1.22 ⇒ Long; 100 → |z| ≈ 0 ⇒ Flat (close) ⇒ deterministic open/close churn.
        var cycle = new List<Ohlcv>();
        for (var i = 0; i < 14; i++)
        {
            cycle.Add(Bar("AAPL", 3 * i, 99m));
            cycle.Add(Bar("AAPL", 3 * i + 1, 100m));
            cycle.Add(Bar("AAPL", 3 * i + 2, 101m));
        }

        var data = new HistoricalDataSet(new Dictionary<string, IReadOnlyList<Ohlcv>> { ["AAPL"] = cycle });
        var result = Run(
            data,
            new[] { "AAPL" },
            o =>
            {
                o.Parameters["Symbol"] = "AAPL";
                o.Parameters["Strategy"] = "MeanReversion";
                o.Parameters["EntryZ"] = "1.0";
                o.Parameters["ExitZ"] = "0.8";
                o.Parameters["AllowShort"] = "false";
            });

        Assert.AreEqual(42, result.EquityCurve.Count);
        Assert.IsTrue(result.Trades.Count > 0, "MeanReversion on a 99/100/101 cycle must trade");
        Assert.AreEqual(result.Trades.Count, result.Metrics.TotalTrades);
        AssertMetricsNonDefault(result.Metrics);
    }

    [TestMethod]
    public void PairTradingZScore_CorrelatedPair_FullBacktestTrades()
    {
        // A=100+i（60 根）；B=2·A+10，末根 B 偏离回归线 +20 ⇒ 末 bar lastZ≈+7.7 ≥ 1.5 ⇒ A Short / B Long
        // A = 100+i (60 bars); B = 2A + 10 with the final bar displaced +20 ⇒ lastZ ≈ +7.7 ≥ 1.5 ⇒ A Short / B Long.
        var a = new List<Ohlcv>();
        var b = new List<Ohlcv>();
        for (var i = 0; i < 60; i++)
        {
            var pa = (decimal)(100 + i);
            var pb = (decimal)(210 + 2 * i + (i == 59 ? 20 : 0));
            a.Add(Bar("AAA", i, pa));
            b.Add(Bar("BBB", i, pb));
        }

        var data = new HistoricalDataSet(new Dictionary<string, IReadOnlyList<Ohlcv>>
        {
            ["AAA"] = a,
            ["BBB"] = b,
        });

        var result = Run(
            data,
            new[] { "AAA", "BBB" },
            o =>
            {
                o.Parameters["SymbolA"] = "AAA";
                o.Parameters["SymbolB"] = "BBB";
                o.Parameters["Strategy"] = "PairTradingZScore";
                o.Parameters["MinCorrelation"] = "0.8";
                o.Parameters["UseAdfFilter"] = "false"; // 确定性：跳过平稳性过滤（参数化，非绕过）/ deterministic: skip the stationarity filter (a documented parameter)
            });

        Assert.AreEqual(60, result.EquityCurve.Count);
        Assert.IsTrue(result.Trades.Count > 0, "PairTradingZScore on a correlated pair must trade");

        var symbols = result.Trades.Select(t => t.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        Assert.IsTrue(symbols.Any(x => string.Equals(x, "AAA", StringComparison.OrdinalIgnoreCase)), "AAA must appear in trades");
        Assert.IsTrue(symbols.Any(x => string.Equals(x, "BBB", StringComparison.OrdinalIgnoreCase)), "BBB must appear in trades");

        Assert.AreEqual(result.Trades.Count, result.Metrics.TotalTrades);
        AssertMetricsNonDefault(result.Metrics);
    }

    /// <summary>
    /// §9 B5：“Metrics 非默认值”——9 项指标至少一项非零。
    /// Section 9 B5: “non-default metrics” — at least one of the nine metrics is non-zero.
    /// </summary>
    private static void AssertMetricsNonDefault(Quant.Infra.Net.Backtest.Models.BacktestMetrics m)
    {
        Assert.IsTrue(
            m.Cagr != 0m || m.SharpeRatio != 0d || m.CalmarRatio != 0d || m.MaxDrawdown != 0m
            || m.MaxDrawdownDurationDays != 0 || m.TotalTrades > 0 || m.WinRate != 0d
            || m.ProfitFactor != 0d || m.TotalCommissionUsd != 0m,
            "Metrics must not be entirely default values");
    }
}
