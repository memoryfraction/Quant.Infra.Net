using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Backtest.Broker;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Runtime;
using Quant.Infra.Net.Runtime.Models;
using Quant.Infra.Net.Runtime.Console.Strategies;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;
using ScottPlot;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Backtest.Models;
using C = System.Console;

namespace Quant.Infra.Net.Runtime.Console;

/// <summary>
/// R9/R10 文档宿主：真实 QQQM 回测 + ScottPlot 图表。
/// 用法：dotnet run --project Quant.Infra.Net.Runtime.Console -- QqqmDoc
/// 数据源策略：优先 Stooq（stooq.com 免费公开日线）；stooq.com 不可达/反爬时回退到
/// docs/assets/_qqqm_yfinance.json（真实 QQQM 日线，Yahoo Finance 公开图表 API 快照，
/// 由 docs/assets/qqqm_fetch_data.js 重新拉取刷新）。非 SLA 数据源，仅用于研究/回测。
/// </summary>
public static class QqqmDocWalkthrough
{
    private const string Symbol = "QQQM";
    private const int MaPeriod = 200;

    public static async Task<int> RunAsync()
    {
        C.WriteLine("=== Quant.Infra.Net CompleteWalkthrough — QQQM reverse-MA200 DCA (real data backtest) ===");
        var t0 = new DateTime(2021, 1, 4, 0, 0, 0, DateTimeKind.Utc); // 覆盖 2021-09 起 MA200 有值 → 含 2022 熊市

        var services = new ServiceCollection();
        services.AddQuantInfraNet(
            rt =>
            {
                rt.RunMode = RunMode.Backtest;
                rt.DataSource = DataSourceKind.Stooq; // 默认优先 Stooq；不可达时由下方缓存兜底
            },
            o =>
            {
                foreach (var (k, v) in QqqmReverseDcaStrategy.DefaultParameters) { o.Parameters[k] = v; }
                o.MaxWeightPerSymbol = 1.0; // strategy target weight can reach 1.0; relax the demo default of 0.5
                o.MaxGrossExposure = 1.0;
            },
            b => b.WarmupBars = 200);
        using var sp = services.BuildServiceProvider();

        List<Ohlcv> bars;
        string sourceDesc;
        var cached = LoadCachedQqqm();
        if (cached.Count > 0)
        {
            bars = cached;
            sourceDesc = "local cached real QQQM daily closes (docs/assets/_qqqm_yfinance.json; refresh with docs/assets/qqqm_fetch_data.js)";
        }
        else
        {
            C.WriteLine("local cache not found — trying Stooq (stooq.com free public daily bars)");
            try
            {
                var ohlcvs = sp.GetRequiredService<ITraditionalFinanceSourceDataService>()
                    .DownloadOhlcvListAsync(Symbol, t0, DateTime.UtcNow).GetAwaiter().GetResult();
                bars = ohlcvs.OhlcvSet.OrderBy(b => b.OpenDateTime).ToList();
                sourceDesc = "Stooq (stooq.com free public daily bars)";
            }
            catch (Exception ex)
            {
                C.WriteLine("FATAL: no data (no local cache and stooq.com unreachable: " + ex.Message.Split('\n')[0] + ")");
                C.WriteLine("Fix: run `node docs/assets/qqqm_fetch_data.js` to create the local cache, then re-run.");
                return 1;
            }
        }
        C.WriteLine($"data source (resolved): {sourceDesc}");
        var closes = bars.Select(b => (double)b.Close).ToList();
        C.WriteLine($"bars loaded: {bars.Count}  ({bars.First().OpenDateTime:yyyy-MM-dd} .. {bars.Last().OpenDateTime:yyyy-MM-dd})");
        C.WriteLine($"initial equity: 10000 USD | warmup bars: 200 | commission/slippage: 0 bps | fill: SameBarClose");

        var data = new HistoricalDataSet(new Dictionary<string, IReadOnlyList<Ohlcv>> { [Symbol] = bars });
        var pipeline = new StrategyPipeline(QqqmReverseDcaStrategy.BuildPipeline(sp));
        var broker = (IBacktestBroker)sp.GetRequiredService<IBinanceUsdFutureService>();
        var runner = new BacktestRunner(
            pipeline, broker, sp.GetRequiredService<OrchestrationOptions>(), sp.GetRequiredService<BacktestOptions>());
        var result = await runner.RunAsync(data, new[] { Symbol });
        var m = result.Metrics;

        C.WriteLine("=== backtest metrics (real run) ===");
        C.WriteLine($"bars={result.EquityCurve.Count}  trades={m.TotalTrades}");
        C.WriteLine($"CAGR={m.Cagr:P2}  Sharpe={m.SharpeRatio:F2}  Calmar={m.CalmarRatio:F2}");
        C.WriteLine($"MaxDrawdown={m.MaxDrawdown:P2}  WinRate={m.WinRate:P1}  ProfitFactor={m.ProfitFactor:F2}  Commission={m.TotalCommissionUsd} USD");

        C.WriteLine("=== equity curve (every 20 bars) ===");
        var curve = result.EquityCurve.OrderBy(kv => kv.Key).ToList();
        for (var i = 0; i < curve.Count; i += 20)
        {
            var kv = curve[i];
            C.WriteLine($"{kv.Key:yyyy-MM-dd}  equity={kv.Value:F2} USD");
        }
        var last = curve[^1];
        C.WriteLine($"{last.Key:yyyy-MM-dd}  equity={last.Value:F2} USD");

        C.WriteLine("=== 2022 drawdown window: strategy activity (real numbers) ===");
        var w0 = new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var w1 = new DateTime(2022, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var inWindow = bars.Where(b => b.OpenDateTime >= w0 && b.OpenDateTime <= w1).ToList();
        if (inWindow.Count > 0)
        {
            C.WriteLine($"QQQM 2022: first close={inWindow.First().Close:0.0000}  last close={inWindow.Last().Close:0.0000}  change={((double)inWindow.Last().Close / (double)inWindow.First().Close - 1.0):P2}");
        }
        C.WriteLine("per-bar target weight (first 12 + last 12 of the window, MA200 formula):");
        var idx = bars.ToList();
        var printed = 0;
        for (var i = MaPeriod - 1; i < idx.Count && printed < 12; i++)
        {
            var b = idx[i];
            if (b.OpenDateTime < w0 || b.OpenDateTime > w1) { continue; }
            var sma = closes.Skip(i - MaPeriod + 1).Take(MaPeriod).Average();
            var w = QqqmReverseDcaStrategy.ComputeTargetWeight((double)b.Close, sma, 0.5, 1.5, 1.0, 0.0, 1.0);
            C.WriteLine($"{b.OpenDateTime:yyyy-MM-dd}  close={(double)b.Close,8:0.0000}  SMA200={sma,9:0.0000}  targetWeight={w,6:0.0000}");
            printed++;
        }
        C.WriteLine("  ...");
        printed = 0;
        for (var i = idx.Count - 1; i >= MaPeriod - 1 && printed < 12; i--)
        {
            var b = idx[i];
            if (b.OpenDateTime < w0 || b.OpenDateTime > w1) { continue; }
            var sma = closes.Skip(i - MaPeriod + 1).Take(MaPeriod).Average();
            var w = QqqmReverseDcaStrategy.ComputeTargetWeight((double)b.Close, sma, 0.5, 1.5, 1.0, 0.0, 1.0);
            C.WriteLine($"{b.OpenDateTime:yyyy-MM-dd}  close={(double)b.Close,8:0.0000}  SMA200={sma,9:0.0000}  targetWeight={w,6:0.0000}");
            printed++;
        }

        C.WriteLine("=== trades (real run) ===");
        foreach (var t in result.Trades.Take(40))
        {
            C.WriteLine($"{t.TimestampUtc:yyyy-MM-dd}  {t.Symbol}  side={t.Side}  fill={t.FillPrice}  notional={t.NotionalUsd} USD  commission={t.CommissionUsd} USD");
        }

        // —— ScottPlot 图表（参照核心库 Portfolio.DrawChart 的既有画图模式）
        var assetsDir = FindRepoRoot() + Path.DirectorySeparatorChar + "docs" + Path.DirectorySeparatorChar + "assets";
        Directory.CreateDirectory(assetsDir);
        var equityPath = Path.Combine(assetsDir, "qqqm-reverse-dca-equity-curve.png");
        var weightPath = Path.Combine(assetsDir, "qqqm-reverse-dca-target-weight.png");

        var eqX = curve.Select(kv => kv.Key.ToOADate()).ToArray();
        var eqY = curve.Select(kv => (double)kv.Value).ToArray();
        var pltEq = new Plot();
        var sig = pltEq.Add.Scatter(eqX, eqY);
        sig.Color = Color.FromHex("#1f77b4");
        sig.LineWidth = 2;
        pltEq.Axes.DateTimeTicksBottom();
        pltEq.Title("QQQM reverse-MA200 DCA - backtest equity curve (real daily closes, 2021-09 to present)");
        pltEq.XLabel("Date");
        pltEq.YLabel("Equity (USD)");
        pltEq.SavePng(equityPath, 960, 540);
        C.WriteLine($"chart written: {equityPath}");

        var wX = new List<double>();
        var wY = new List<double>();
        for (var i = MaPeriod - 1; i < idx.Count; i++)
        {
            var sma = closes.Skip(i - MaPeriod + 1).Take(MaPeriod).Average();
            wX.Add(idx[i].OpenDateTime.ToOADate());
            wY.Add(QqqmReverseDcaStrategy.ComputeTargetWeight(closes[i], sma, 0.5, 1.5, 1.0, 0.0, 1.0));
        }
        var pltW = new Plot();
        var sigW = pltW.Add.Scatter(wX.ToArray(), wY.ToArray());
        sigW.Color = Color.FromHex("#d62728");
        sigW.LineWidth = 2;
        pltW.Axes.DateTimeTicksBottom();
        pltW.Title("QQQM reverse-MA200 DCA - target weight over time (base=0.5, add=1.5, trim=1.0)");
        pltW.XLabel("Date");
        pltW.YLabel("Target weight");
        pltW.SavePng(weightPath, 960, 540);
        C.WriteLine($"chart written: {weightPath}");

        C.WriteLine("done.");
        return 0;
    }

    /// <summary>从 docs/assets/_qqqm_yfinance.json 加载真实 QQQM 日线缓存（stooq.com 不可达时的兜底）。</summary>
    private static List<Ohlcv> LoadCachedQqqm()
    {
        var p = Path.Combine(FindRepoRoot(), "docs", "assets", "_qqqm_yfinance.json");
        if (!File.Exists(p)) { return new List<Ohlcv>(); }
        var txt = File.ReadAllText(p);
        // 极简 JSON 扫描：提取 {"t":..., "c":...} 序列（避免引入额外 JSON 依赖）
        var bars = new List<Ohlcv>();
        var re = new System.Text.RegularExpressions.Regex(@"\{""t"":\s*(\d{9,12})\s*,\s*""c"":\s*([0-9.]+)\s*\}");
        foreach (System.Text.RegularExpressions.Match mm in re.Matches(txt))
        {
            var epoch = long.Parse(mm.Groups[1].Value);
            var close = (decimal)double.Parse(mm.Groups[2].Value, CultureInfo.InvariantCulture);
            var dt = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(epoch);
            bars.Add(new Ohlcv
            {
                Symbol = Symbol,
                OpenDateTime = dt,
                CloseDateTime = dt.AddDays(1),
                Open = close, High = close, Low = close, Close = close, Volume = 1m,
            });
        }
        bars.Sort((a, b) => a.OpenDateTime.CompareTo(b.OpenDateTime));
        return bars;
    }

    private static string FindRepoRoot()
    {
        var envRoot = Environment.GetEnvironmentVariable("QUANT_INFRA_NET_ROOT");
        if (!string.IsNullOrEmpty(envRoot) && Directory.Exists(Path.Combine(envRoot, "docs")))
        {
            return envRoot;
        }
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            // The true repo root is the directory that contains BOTH docs/ and src/.
            // (src/ itself holds Quant.Infra.Net.sln, so matching on the .sln alone
            // would stop one level too early and break the docs/assets/... cache path.)
            if (Directory.Exists(Path.Combine(dir.FullName, "docs")) &&
                Directory.Exists(Path.Combine(dir.FullName, "src")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("repository root not found from " + AppContext.BaseDirectory);
    }
}