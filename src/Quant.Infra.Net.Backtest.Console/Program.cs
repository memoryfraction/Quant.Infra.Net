using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Backtest;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.SourceData.Model;

// B5 演示：离线跑一次完整 MaCross 回测并打印绩效报告 / Runs one full offline MaCross backtest and prints the performance report.

// 1) 历史数据：离线合成日线（可用真实数据源构建的 HistoricalDataSet 替换）
//    Historical data: offline synthetic daily bars (swap in a HistoricalDataSet from a real source when available).
var t0 = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
var bars = new List<Ohlcv>();
for (var i = 0; i < 120; i++)
{
    var price = Math.Round((decimal)(100.0 * (1 + 0.002 * i) + 3.0 * Math.Sin(i / 7.0)), 2);
    bars.Add(new Ohlcv { Symbol = "AAPL", OpenDateTime = t0.AddDays(i), Open = price, High = price, Low = price, Close = price, Volume = 1m });
}

var data = new HistoricalDataSet(new Dictionary<string, IReadOnlyList<Ohlcv>> { ["AAPL"] = bars });

// 2) 依赖注入：回测层（BacktestBrokerService + 八阶段管道 + BacktestRunner）
//    DI: the backtest layer (BacktestBrokerService + eight-stage pipeline + BacktestRunner).
var services = new ServiceCollection();
services.AddQuantInfraNetBacktest(
    b => { b.InitialEquityUsd = 10000m; b.WarmupBars = 20; },
    o =>
    {
        o.Parameters["Symbol"] = "AAPL";
        o.Parameters["Strategy"] = "MaCross";
        o.Parameters["FastPeriod"] = "5";
        o.Parameters["SlowPeriod"] = "20";
    });

using var provider = services.BuildServiceProvider();
var runner = provider.GetRequiredService<BacktestRunner>();

// 3) 运行并打印绩效报告 / Run and print the performance report.
var result = await runner.RunAsync(data, new[] { "AAPL" });
var m = result.Metrics;

Console.WriteLine($"回测完成 / Backtest complete: {result.EquityCurve.Count} bars, {m.TotalTrades} trades");
Console.WriteLine($"CAGR={m.Cagr:P2}   Sharpe={m.SharpeRatio:F2}   Calmar={m.CalmarRatio:F2}");
Console.WriteLine($"MaxDrawdown={m.MaxDrawdown:P2}（{m.MaxDrawdownDurationDays} 天 / days）");
Console.WriteLine($"WinRate={m.WinRate:P1}   ProfitFactor={m.ProfitFactor:F2}   Commission={m.TotalCommissionUsd} USD");
