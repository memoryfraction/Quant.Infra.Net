using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Runtime;
using Quant.Infra.Net.Runtime.Models;
using Quant.Infra.Net.Runtime.Console;
using Quant.Infra.Net.Runtime.Console.Strategies;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

// R5：唯一 Demo 宿主——四模式共用本 Program；唯一开关 = appsettings.json 的 "Runtime:RunMode"（输出为英文，防乱码）。
// R5: the single demo host — all four modes share this Program; the ONLY switch is "Runtime:RunMode" in appsettings.json (output in English).
var config = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory).AddJsonFile("appsettings.json").Build();
var services = new ServiceCollection();
services.AddQuantInfraNet(
    rt => config.GetSection("Runtime").Bind(rt),
    o => config.GetSection("Orchestration").Bind(o),
    b => config.GetSection("Backtest").Bind(b),
    strategyAssemblies: typeof(Program).Assembly);
using var sp = services.BuildServiceProvider();
if (args.Length > 0 && args[0] == "QqqmDoc")
{
    return QqqmDocWalkthrough.RunAsync().GetAwaiter().GetResult();
}

if (sp.GetRequiredService<RuntimeOptions>().RunMode == RunMode.Backtest)
{
    var t0 = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc); // 固定窗口起点（Demo 数据源确定性输出）/ fixed window origin (deterministic Demo source)
    var symbol = sp.GetRequiredService<OrchestrationOptions>().Parameters["Symbol"];
    var ohlcvs = sp.GetRequiredService<ITraditionalFinanceSourceDataService>().DownloadOhlcvListAsync(symbol, t0, t0.AddYears(2)).GetAwaiter().GetResult();
    var data = new HistoricalDataSet(new Dictionary<string, IReadOnlyList<Ohlcv>> { [symbol] = ohlcvs.OhlcvSet.OrderBy(b => b.OpenDateTime).ToList() });
    var result = sp.GetRequiredService<BacktestRunner>().RunAsync(data, new[] { symbol }).GetAwaiter().GetResult();
    var m = result.Metrics;
    Console.WriteLine($"Backtest complete: {result.EquityCurve.Count} bars, {m.TotalTrades} trades");
    Console.WriteLine($"CAGR={m.Cagr:P2}   Sharpe={m.SharpeRatio:F2}   Calmar={m.CalmarRatio:F2}");
    Console.WriteLine($"MaxDrawdown={m.MaxDrawdown:P2}   WinRate={m.WinRate:P1}   ProfitFactor={m.ProfitFactor:F2}   Commission={m.TotalCommissionUsd} USD");
}
else
{
    var ctx = sp.GetRequiredService<PipelineRunner>().RunOnceAsync().GetAwaiter().GetResult();
    Console.WriteLine($"runId={ctx.RunId} strategy={ctx.GetParameter("Strategy") ?? "-"}");
    foreach (var e in ctx.Events) { Console.WriteLine($"{e.TimestampUtc:HH:mm:ss} INF [{e.Stage}] {e.Message}"); }
    Console.WriteLine($"cycle complete: events={ctx.Events.Count} errors={ctx.Errors.Count}");
    return 0;
}
return 0;
