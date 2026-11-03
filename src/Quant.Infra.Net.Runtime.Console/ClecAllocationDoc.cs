using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Backtest.Broker;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Runtime.Console.Strategies;
using Quant.Infra.Net.Runtime.Models;
using Quant.Infra.Net.SourceData.Model;
using C = System.Console;

namespace Quant.Infra.Net.Runtime.Console;

/// <summary>
/// CLEC 三桶配置回测文档宿主：用真实 QQQ / QLD / BIL 复权日线，把 alpha-wealth-lab.com/planner/allocation/
/// 的 10 个阵型（703 / 802 / 613 / 433 / 442 / 514 / 424 / 415 / 505 / 226）逐一回测，并为每个阵型生成
/// docs/backtests/clec-{code}.md（内嵌 Mermaid 图表）与总览 docs/backtests/README.md。
/// 用法：dotnet run --project src/Quant.Infra.Net.Runtime.Console -- ClecDoc
/// CLEC three-sleeve allocation backtest host: runs every mix from alpha-wealth-lab.com/planner/allocation/
/// on real adjusted QQQ / QLD / BIL daily closes and writes docs/backtests/clec-{code}.md (with embedded
/// Mermaid charts) plus the overview docs/backtests/README.md.
/// </summary>
public static class ClecAllocationDoc
{
    private const string BaseSym = "QQQ";
    private const string LevSym = "QLD";
    private const string CashSym = "BIL";
    private const decimal InitialEquity = 10_000m;
    private const decimal CommissionBps = 5m;
    private const decimal SlippageBps = 2m;
    private static readonly DateTime End = new(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);
    private const string AwlUrl = "https://www.alpha-wealth-lab.com/planner/allocation/";

    private static readonly (string Id, string En, string Zh, DateTime From, DateTime To)[] StressWindows =
    {
        ("gfc", "2007–09 financial crisis", "2007–09 金融危机", D(2007, 10, 31), D(2009, 3, 9)),
        ("eu", "2011 euro-debt scare", "2011 欧债危机", D(2011, 7, 7), D(2011, 10, 3)),
        ("q4", "2018 Q4 sell-off", "2018 四季度暴跌", D(2018, 10, 1), D(2018, 12, 24)),
        ("covid", "2020 COVID crash", "2020 新冠暴跌", D(2020, 2, 19), D(2020, 3, 20)),
        ("y2022", "2022 rate-hike bear market", "2022 加息熊市", D(2021, 11, 19), D(2022, 12, 28)),
    };

    private static DateTime D(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>运行全部回测并写文档 / Runs every backtest and writes the docs.</summary>
    public static async Task<int> RunAsync()
    {
        C.WriteLine("=== Quant.Infra.Net — CLEC allocation backtests (QQQ / QLD / BIL, real adjusted daily closes) ===");
        var root = FindRepoRoot();
        var bars = LoadCache(Path.Combine(root, "docs", "assets", "_clec_yfinance.json"));
        if (bars is null)
        {
            C.WriteLine("FATAL: docs/assets/_clec_yfinance.json not found. Run `node docs/assets/clec_fetch_data.js` first.");
            return 1;
        }

        foreach (var (s, list) in bars)
        {
            C.WriteLine($"{s}: {list.Count} bars ({list[0].OpenDateTime:yyyy-MM-dd} .. {list[^1].OpenDateTime:yyyy-MM-dd})");
        }

        var data = new HistoricalDataSet(bars.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<Ohlcv>)kv.Value));
        var bench = await RunMixAsync(new ClecMix("QQQ", 1, 0, 0, "Benchmark: 100% QQQ, buy and hold", "基准：100% QQQ 买入持有"), data);
        C.WriteLine(Line(bench));

        var results = new List<Run>();
        foreach (var mix in ClecMix.Presets)
        {
            var r = await RunMixAsync(mix, data);
            results.Add(r);
            C.WriteLine(Line(r));
        }

        var outDir = Path.Combine(root, "docs", "backtests");
        Directory.CreateDirectory(outDir);
        foreach (var r in results)
        {
            var path = Path.Combine(outDir, $"clec-{r.Mix.Code}.md");
            await File.WriteAllTextAsync(path, MixDoc(r, bench, results), new UTF8Encoding(false));
            C.WriteLine("written: " + Path.GetRelativePath(root, path));
        }

        var index = Path.Combine(outDir, "README.md");
        await File.WriteAllTextAsync(index, IndexDoc(results, bench), new UTF8Encoding(false));
        C.WriteLine("written: " + Path.GetRelativePath(root, index));
        return 0;
    }

    // ------------------------------------------------------------------ backtest

    private sealed record Run(ClecMix Mix, BacktestResult Result, List<(DateTime T, double E)> Curve, int Rebalances)
    {
        public double Final => Curve[^1].E;
        public double Years => (Curve[^1].T - Curve[0].T).TotalDays / 365.25;
        public double Cagr => Math.Pow(Final / Curve[0].E, 1 / Years) - 1;
        public double Vol => DailyReturns().StdDev() * Math.Sqrt(252);
        public double Sharpe => Result.Metrics.SharpeRatio;
        public (double Dd, DateTime Peak, DateTime Trough, DateTime? Recovered) MaxDd => Drawdown(Curve);
        public double Calmar => Cagr / Math.Abs(MaxDd.Dd);

        public List<double> DailyReturns()
        {
            var r = new List<double>(Curve.Count);
            for (var i = 1; i < Curve.Count; i++) r.Add(Curve[i].E / Curve[i - 1].E - 1);
            return r;
        }

        public SortedDictionary<int, double> Yearly()
        {
            var y = new SortedDictionary<int, double>();
            var prev = Curve[0].E;
            foreach (var g in Curve.GroupBy(p => p.T.Year))
            {
                var last = g.Last().E;
                y[g.Key] = last / prev - 1;
                prev = last;
            }
            return y;
        }

        public double WindowDd(DateTime from, DateTime to)
            => Drawdown(Curve.Where(p => p.T >= from && p.T <= to).ToList()).Dd;
    }

    private static async Task<Run> RunMixAsync(ClecMix mix, HistoricalDataSet data)
    {
        var services = new ServiceCollection();
        services.AddQuantInfraNet(
            rt =>
            {
                rt.RunMode = RunMode.Backtest;
                rt.DataSource = DataSourceKind.Demo; // 数据已预加载并按 as-of 注入 / data preloaded, injected as-of
            },
            o =>
            {
                o.MaxWeightPerSymbol = 1.0;
                o.MaxGrossExposure = 1.0000001;
                o.KillSwitchDrawdownRate = -10.0; // 配置策略不设熔断 / no kill switch for an allocation
                o.MinRebalanceDelta = 0.001;
                o.InitialEquityUsd = InitialEquity;
            },
            b =>
            {
                b.InitialEquityUsd = InitialEquity;
                b.CommissionBps = CommissionBps;
                b.SlippageBps = SlippageBps;
                b.WarmupBars = 0;
                b.FillTiming = FillTiming.SameBarClose; // 缓存只有收盘价 / the cache holds closes only
            });
        using var sp = services.BuildServiceProvider();

        var stage = new ClecAllocationStage(mix, BaseSym, LevSym, CashSym);
        var pipeline = new StrategyPipeline(ClecAllocationStage.BuildPipeline(sp, stage));
        var broker = (IBacktestBroker)sp.GetRequiredService<IBinanceUsdFutureService>();
        var runner = new BacktestRunner(pipeline, broker, sp.GetRequiredService<OrchestrationOptions>(), sp.GetRequiredService<BacktestOptions>());
        var result = await runner.RunAsync(data, new[] { BaseSym, LevSym, CashSym });
        var curve = result.EquityCurve.OrderBy(kv => kv.Key).Select(kv => (kv.Key, (double)kv.Value)).ToList();
        return new Run(mix, result, curve, stage.Rebalances);
    }

    private static (double Dd, DateTime Peak, DateTime Trough, DateTime? Recovered) Drawdown(List<(DateTime T, double E)> c)
    {
        if (c.Count == 0) return (0, default, default, null);
        double peak = c[0].E, worst = 0;
        DateTime peakT = c[0].T, wPeak = c[0].T, wTrough = c[0].T;
        foreach (var (t, e) in c)
        {
            if (e > peak) { peak = e; peakT = t; }
            var dd = e / peak - 1;
            if (dd < worst) { worst = dd; wPeak = peakT; wTrough = t; }
        }

        var peakValue = c.First(p => p.T == wPeak).E;
        DateTime? rec = c.Where(p => p.T > wTrough && p.E >= peakValue).Select(p => (DateTime?)p.T).FirstOrDefault();
        return (worst, wPeak, wTrough, rec);
    }

    private static string Line(Run r)
        => string.Format(CultureInfo.InvariantCulture, "{0,-4} final={1,10:N0}  CAGR={2,7:P2}  vol={3,7:P2}  Sharpe={4,5:F2}  MaxDD={5,8:P2}  trades={6}",
            r.Mix.Code, r.Final, r.Cagr, r.Vol, r.Sharpe, r.MaxDd.Dd, r.Result.Metrics.TotalTrades);

    // ------------------------------------------------------------------ markdown

    private static string P(double x, int d = 1) => (x * 100).ToString("F" + d, CultureInfo.InvariantCulture) + "%";
    private static string N(double x) => x.ToString("N0", CultureInfo.InvariantCulture);
    private static string F(double x, int d = 2) => x.ToString("F" + d, CultureInfo.InvariantCulture);
    private static string Ds(DateTime t) => t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string W(ClecMix m) => $"{P(m.Base, 0)} {BaseSym} / {P(m.Lev, 0)} {LevSym} / {P(m.Cash, 0)} {CashSym}";

    private static string MixDoc(Run r, Run bench, List<Run> all)
    {
        var m = r.Mix;
        var dd = r.MaxDd;
        var bdd = bench.MaxDd;
        var years = r.Yearly();
        var byears = bench.Yearly();
        var sb = new StringBuilder();
        var start = r.Curve[0].T;
        var end = r.Curve[^1].T;

        sb.AppendLine($"# CLEC {m.Code} backtest / CLEC {m.Code} 阵型回测");
        sb.AppendLine();
        sb.AppendLine($"> **{m.Code} = {W(m)}** · beta {F(m.Beta)} · {m.UseEn} / {m.UseZh}");
        sb.AppendLine(">");
        sb.AppendLine($"> Real adjusted daily closes, **{Ds(start)} → {Ds(end)}** ({F(r.Years, 1)} years), rebalanced on the first trading day of each year. Generated by [Quant.Infra.Net](../../README.md); try the mix interactively at [Alpha Wealth Lab · 433 Allocation & Beta Check]({AwlUrl}).");
        sb.AppendLine($"> 真实复权日线，**{Ds(start)} → {Ds(end)}**（{F(r.Years, 1)} 年），每年第一个交易日再平衡。由 [Quant.Infra.Net](../../README.md) 生成；可在 [Alpha Wealth Lab · 433 配置与 Beta 体检]({AwlUrl}) 交互试算。");
        sb.AppendLine();
        sb.AppendLine("[← All mixes / 全部阵型](README.md)");
        sb.AppendLine();

        sb.AppendLine("## Result vs 100% QQQ / 结果对比 100% QQQ");
        sb.AppendLine();
        sb.AppendLine($"| Metric / 指标 | CLEC {m.Code} | 100% QQQ |");
        sb.AppendLine("|---|---:|---:|");
        sb.AppendLine($"| $10,000 grows to / 一万美元变成 | **${N(r.Final)}** | ${N(bench.Final)} |");
        sb.AppendLine($"| CAGR / 年化收益 | **{P(r.Cagr, 2)}** | {P(bench.Cagr, 2)} |");
        sb.AppendLine($"| Volatility / 年化波动 | {P(r.Vol)} | {P(bench.Vol)} |");
        sb.AppendLine($"| Max drawdown / 最大回撤 | **{P(dd.Dd)}** | {P(bdd.Dd)} |");
        sb.AppendLine($"| Max drawdown dates / 回撤区间 | {Ds(dd.Peak)} → {Ds(dd.Trough)} | {Ds(bdd.Peak)} → {Ds(bdd.Trough)} |");
        sb.AppendLine($"| Recovered on / 回本日 | {(dd.Recovered is { } rc ? Ds(rc) : "not yet / 尚未")} | {(bdd.Recovered is { } brc ? Ds(brc) : "not yet / 尚未")} |");
        sb.AppendLine($"| Sharpe (rf = 0) | {F(r.Sharpe)} | {F(bench.Sharpe)} |");
        sb.AppendLine($"| Calmar (CAGR / MaxDD) | {F(r.Calmar)} | {F(bench.Calmar)} |");
        sb.AppendLine($"| Best year / 最好年份 | {BestWorst(years, true)} | {BestWorst(byears, true)} |");
        sb.AppendLine($"| Worst year / 最差年份 | {BestWorst(years, false)} | {BestWorst(byears, false)} |");
        sb.AppendLine($"| Losing years / 亏损年数 | {years.Count(y => y.Value < 0)} / {years.Count} | {byears.Count(y => y.Value < 0)} / {byears.Count} |");
        sb.AppendLine($"| Rebalances · trades · costs / 再平衡 · 成交 · 费用 | {r.Rebalances} · {r.Result.Metrics.TotalTrades} · ${N((double)r.Result.Metrics.TotalCommissionUsd)} | buy & hold |");
        sb.AppendLine();
        var rank = all.OrderByDescending(x => x.Calmar).ToList().FindIndex(x => x.Mix.Code == m.Code) + 1;
        sb.AppendLine($"Among the 10 CLEC mixes, {m.Code} ranks **#{all.OrderByDescending(x => x.Cagr).ToList().FindIndex(x => x.Mix.Code == m.Code) + 1} by CAGR** and **#{rank} by Calmar** (return per unit of drawdown).");
        sb.AppendLine($"在 10 个 CLEC 阵型里，{m.Code} 的**年化收益排第 {all.OrderByDescending(x => x.Cagr).ToList().FindIndex(x => x.Mix.Code == m.Code) + 1}**，**Calmar（收益 / 回撤）排第 {rank}**。");
        sb.AppendLine();

        sb.AppendLine("## Allocation / 配置");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("pie showData");
        sb.AppendLine($"    title CLEC {m.Code} target weights (%)");
        if (m.Base > 0) sb.AppendLine($"    \"{BaseSym} (base index)\" : {F(m.Base * 100, 0)}");
        if (m.Lev > 0) sb.AppendLine($"    \"{LevSym} (2x leveraged)\" : {F(m.Lev * 100, 0)}");
        if (m.Cash > 0) sb.AppendLine($"    \"{CashSym} (cash / T-bills)\" : {F(m.Cash * 100, 0)}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart LR");
        sb.AppendLine("    A[New bar] --> B{First trading day<br/>of the year?}");
        sb.AppendLine($"    B -- yes --> C[\"Rebalance to<br/>{P(m.Base, 0)} {BaseSym} · {P(m.Lev, 0)} {LevSym} · {P(m.Cash, 0)} {CashSym}\"]");
        sb.AppendLine("    B -- no --> D[Hold: weights drift with the market]");
        sb.AppendLine("    C --> E[Risk → Execution → Portfolio state]");
        sb.AppendLine("    D --> E");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Growth of $10,000 (year-end) / 一万美元净值（年末）");
        sb.AppendLine();
        sb.AppendLine($"Line 1 = CLEC {m.Code}, line 2 = 100% QQQ. / 线 1 = CLEC {m.Code}，线 2 = 100% QQQ。");
        sb.AppendLine();
        var ye = YearEnd(r.Curve);
        var bye = YearEnd(bench.Curve);
        var labels = string.Join(", ", ye.Select(p => p.Label));
        var maxY = Math.Max(ye.Max(p => p.E), bye.Max(p => p.E));
        sb.AppendLine("```mermaid");
        sb.AppendLine("xychart-beta");
        sb.AppendLine($"    title \"CLEC {m.Code} vs 100% QQQ, growth of $10,000\"");
        sb.AppendLine($"    x-axis [{labels}]");
        sb.AppendLine($"    y-axis \"USD\" 0 --> {Math.Ceiling(maxY / 10000) * 10000:0}");
        sb.AppendLine($"    line [{string.Join(", ", ye.Select(p => p.E.ToString("0", CultureInfo.InvariantCulture)))}]");
        sb.AppendLine($"    line [{string.Join(", ", bye.Select(p => p.E.ToString("0", CultureInfo.InvariantCulture)))}]");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Calendar-year returns / 年度收益");
        sb.AppendLine();
        var yMin = Math.Floor(Math.Min(years.Values.Min(), byears.Values.Min()) * 10) * 10;
        var yMax = Math.Ceiling(Math.Max(years.Values.Max(), byears.Values.Max()) * 10) * 10;
        sb.AppendLine("```mermaid");
        sb.AppendLine("xychart-beta");
        sb.AppendLine($"    title \"CLEC {m.Code} calendar-year return (%)\"");
        sb.AppendLine($"    x-axis [{string.Join(", ", years.Keys.Select(y => $"\"{Label(y)}\""))}]");
        sb.AppendLine($"    y-axis \"%\" {yMin:0} --> {yMax:0}");
        sb.AppendLine($"    bar [{string.Join(", ", years.Values.Select(v => (v * 100).ToString("0.0", CultureInfo.InvariantCulture)))}]");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine($"| Year / 年份 | CLEC {m.Code} | 100% QQQ | Difference / 差值 |");
        sb.AppendLine("|---|---:|---:|---:|");
        foreach (var (y, v) in years)
        {
            var b = byears.TryGetValue(y, out var bv) ? bv : double.NaN;
            sb.AppendLine($"| {Label(y)} | {P(v)} | {P(b)} | {(v - b >= 0 ? "+" : "")}{P(v - b)} |");
        }
        sb.AppendLine();
        sb.AppendLine($"`{start.Year}*` starts on {Ds(start)}; `{end.Year}*` ends on {Ds(end)} (partial years). / 带 * 的为不完整年份。");
        sb.AppendLine();

        sb.AppendLine("## Drawdown (quarter-end) / 回撤（季末）");
        sb.AppendLine();
        var mdd = QuarterEndDrawdown(r.Curve);
        sb.AppendLine("```mermaid");
        sb.AppendLine("xychart-beta");
        sb.AppendLine($"    title \"CLEC {m.Code} drawdown from peak (%)\"");
        sb.AppendLine($"    x-axis [{string.Join(", ", mdd.Select(p => $"\"{p.T.Year}Q{(p.T.Month - 1) / 3 + 1}\""))}]");
        sb.AppendLine($"    y-axis \"%\" {Math.Floor(mdd.Min(p => p.Dd) * 10) * 10:0} --> 0");
        sb.AppendLine($"    line [{string.Join(", ", mdd.Select(p => (p.Dd * 100).ToString("0.0", CultureInfo.InvariantCulture)))}]");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Stress test: real bear markets / 压力测试：真实熊市");
        sb.AppendLine();
        sb.AppendLine("Worst peak-to-trough fall of the portfolio inside each window (real QLD prices, so daily-reset decay is included).");
        sb.AppendLine("每个区间内组合从高点到低点的最大跌幅（用的是 QLD 真实价格，已包含二倍 ETF 每日重置的损耗）。");
        sb.AppendLine();
        sb.AppendLine($"| Window / 区间 | Dates / 日期 | CLEC {m.Code} | 100% QQQ |");
        sb.AppendLine("|---|---|---:|---:|");
        foreach (var w in StressWindows)
        {
            sb.AppendLine($"| {w.En} / {w.Zh} | {Ds(w.From)} → {Ds(w.To)} | **{P(r.WindowDd(w.From, w.To))}** | {P(bench.WindowDd(w.From, w.To))} |");
        }
        sb.AppendLine();
        sb.AppendLine($"On $1,000,000 a repeat of the worst window would mean a paper loss of about **${N(1_000_000 * Math.Abs(StressWindows.Min(w => r.WindowDd(w.From, w.To))))}**. Cash is what lets you hold on, and add, instead of being forced to sell.");
        sb.AppendLine($"以 100 万美元计，重演最差区间约浮亏 **${N(1_000_000 * Math.Abs(StressWindows.Min(w => r.WindowDd(w.From, w.To))))}**。现金的作用是让你扛得住、还能补仓，而不是被迫卖出。");
        sb.AppendLine();

        AppendMethod(sb);
        return sb.ToString();
    }

    private static string IndexDoc(List<Run> all, Run bench)
    {
        var sb = new StringBuilder();
        var start = bench.Curve[0].T;
        var end = bench.Curve[^1].T;
        sb.AppendLine("# CLEC allocation backtests / CLEC 阵型回测总览");
        sb.AppendLine();
        sb.AppendLine($"> Every mix from [Alpha Wealth Lab · 433 Allocation & Beta Check]({AwlUrl}) backtested on **real adjusted daily closes, {Ds(start)} → {Ds(end)}** ({F(bench.Years, 1)} years, including 2008, 2020 and 2022), with **{BaseSym}** as the base index, **{LevSym}** as the 2x leveraged ETF and **{CashSym}** as cash. Each mix is rebalanced on the first trading day of each year. Generated by [Quant.Infra.Net](../../README.md).");
        sb.AppendLine(">");
        sb.AppendLine($"> 把 [Alpha Wealth Lab · 433 配置与 Beta 体检]({AwlUrl}) 里的每个阵型，用**真实复权日线（{Ds(start)} → {Ds(end)}，{F(bench.Years, 1)} 年，含 2008、2020、2022）**逐一回测：原型 = **{BaseSym}**，二倍杠杆 = **{LevSym}**，现金 = **{CashSym}**，每年第一个交易日再平衡。由 [Quant.Infra.Net](../../README.md) 生成。");
        sb.AppendLine();
        sb.AppendLine("| Mix / 阵型 | Weights / 比例 | Beta | $10k → | CAGR | Max DD / 最大回撤 | 2008 crisis / 金融危机 | 2022 | Sharpe | Calmar | Report / 报告 |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|");
        var gfc = StressWindows[0];
        var y22 = StressWindows[^1];
        foreach (var r in all)
        {
            sb.AppendLine($"| **{r.Mix.Code}** | {F(r.Mix.Base * 100, 0)} / {F(r.Mix.Lev * 100, 0)} / {F(r.Mix.Cash * 100, 0)} | {F(r.Mix.Beta, 1)} | ${N(r.Final)} | {P(r.Cagr)} | {P(r.MaxDd.Dd)} | {P(r.WindowDd(gfc.From, gfc.To))} | {P(r.WindowDd(y22.From, y22.To))} | {F(r.Sharpe)} | {F(r.Calmar)} | [clec-{r.Mix.Code}.md](clec-{r.Mix.Code}.md) |");
        }
        sb.AppendLine($"| *100% QQQ* | *benchmark* | 1.0 | ${N(bench.Final)} | {P(bench.Cagr)} | {P(bench.MaxDd.Dd)} | {P(bench.WindowDd(gfc.From, gfc.To))} | {P(bench.WindowDd(y22.From, y22.To))} | {F(bench.Sharpe)} | {F(bench.Calmar)} | — |");
        sb.AppendLine();

        var codes = string.Join(", ", all.Select(r => $"\"{r.Mix.Code}\""));
        sb.AppendLine("```mermaid");
        sb.AppendLine("xychart-beta");
        sb.AppendLine("    title \"CAGR by mix (%)\"");
        sb.AppendLine($"    x-axis [{codes}]");
        sb.AppendLine($"    y-axis \"%\" 0 --> {Math.Ceiling(all.Max(r => r.Cagr) * 100 / 5) * 5:0}");
        sb.AppendLine($"    bar [{string.Join(", ", all.Select(r => (r.Cagr * 100).ToString("0.0", CultureInfo.InvariantCulture)))}]");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("xychart-beta");
        sb.AppendLine("    title \"Max drawdown by mix (%)\"");
        sb.AppendLine($"    x-axis [{codes}]");
        sb.AppendLine($"    y-axis \"%\" {Math.Floor(all.Min(r => r.MaxDd.Dd) * 100 / 10) * 10:0} --> 0");
        sb.AppendLine($"    bar [{string.Join(", ", all.Select(r => (r.MaxDd.Dd * 100).ToString("0.0", CultureInfo.InvariantCulture)))}]");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("**How to read this / 怎么看：**");
        sb.AppendLine("- Beta (base + 2 × leveraged) predicts the *shape*: higher beta means more return in long bull runs and deeper holes in crashes. / Beta 决定形态：Beta 越高，长牛里赚得越多，暴跌里坑越深。");
        sb.AppendLine("- Cash is not dead weight. It caps the drawdown and, at each annual rebalance, buys the index back after a bad year. / 现金不是闲钱：它压住回撤，并在每年再平衡时把跌掉的指数买回来。");
        sb.AppendLine("- These are past numbers on one index (Nasdaq-100), one of the strongest of the period. Pick the mix whose *worst year* you could live through, not the one with the biggest final number. / 这是过去的数据，而且纳指 100 是这段时间最强的指数之一。请选择你扛得住*最差年份*的阵型，而不是终值最大的那个。");
        sb.AppendLine();
        AppendMethod(sb);
        return sb.ToString();
    }

    private static void AppendMethod(StringBuilder sb)
    {
        sb.AppendLine("## Method & caveats / 方法与局限");
        sb.AppendLine();
        sb.AppendLine($"- **Data / 数据:** Yahoo Finance daily *adjusted* closes (dividends and splits reinvested) for {BaseSym}, {LevSym} and {CashSym}, cached in [`docs/assets/_clec_yfinance.json`](../assets/_clec_yfinance.json). {CashSym} (1–3 month T-bills) stands in for the cash sleeve because SGOV / BOXX only start in 2020. / Yahoo 复权日收盘价；现金用 BIL（1–3 个月美债）代替，因为 SGOV / BOXX 2020 年才上市。");
        sb.AppendLine($"- **Leverage / 杠杆:** real {LevSym} prices, so daily-reset decay and fund fees are already in the numbers (unlike a simple \"2 × index\" estimate). / 使用 QLD 真实价格，已包含每日重置损耗与基金费用。");
        sb.AppendLine("- **Engine / 引擎:** Quant.Infra.Net bar-by-bar backtester. The strategy stage only sees bars up to the current day; Risk → Execution → Portfolio state are the framework's built-in stages. / 逐 bar 回放，策略只能看到当天及以前的数据。");
        sb.AppendLine($"- **Rebalancing / 再平衡:** back to target weights on the first trading day of each calendar year, filled at that day's close, {CommissionBps} bps commission + {SlippageBps} bps slippage per trade. No taxes, no withdrawals, no contributions. / 每年首个交易日按收盘价再平衡，每笔 {CommissionBps} bp 佣金 + {SlippageBps} bp 滑点；未计税、无提领、无追加投入。");
        sb.AppendLine("- **Sharpe** uses a 0% risk-free rate. / Sharpe 按 0% 无风险利率计算。");
        sb.AppendLine("- **Bias / 偏差:** QQQ is a survivor and one of the best-performing indices of 2007–2026; other indices or periods (e.g. 2000–2002, when the Nasdaq-100 fell 83%) can look much worse. / 存在幸存者偏差，2000–2002 纳指 100 跌了 83%，换个区间结果可能差很多。");
        sb.AppendLine();
        sb.AppendLine("**Reproduce / 复现：**");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("node docs/assets/clec_fetch_data.js");
        sb.AppendLine("dotnet run --project src/Quant.Infra.Net.Runtime.Console -- ClecDoc");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("> The CLEC mixes come from the published *CLEC Handbook* methodology as implemented by Alpha Wealth Lab; this project is not affiliated with or endorsed by CLEC or its authors. Backtests are not forecasts. **Not investment advice.** / 阵型来自《CLEC 宝典》公开方法论（经 Alpha Wealth Lab 实现）；本项目与 CLEC 及作者无关联、未获背书。回测不代表未来，**不构成投资建议。**");
    }

    private static string BestWorst(SortedDictionary<int, double> y, bool best)
    {
        var kv = best ? y.MaxBy(p => p.Value) : y.MinBy(p => p.Value);
        return $"{P(kv.Value)} ({Label(kv.Key)})";
    }

    private static int _firstYear;
    private static int _lastYear;

    private static string Label(int year) => year == _firstYear || year == _lastYear ? $"{year}*" : year.ToString(CultureInfo.InvariantCulture);

    private static List<(string Label, double E)> YearEnd(List<(DateTime T, double E)> c)
    {
        var list = new List<(string, double)> { ($"\"{c[0].T:yyyy-MM}\"", c[0].E) };
        list.AddRange(c.GroupBy(p => p.T.Year).Select(g => ($"\"{g.Key}\"", g.Last().E)));
        return list;
    }

    private static List<(DateTime T, double Dd)> QuarterEndDrawdown(List<(DateTime T, double E)> c)
    {
        var res = new List<(DateTime, double)>();
        var peak = double.MinValue;
        var month = -1;
        (DateTime T, double Dd) last = default;
        foreach (var (t, e) in c)
        {
            peak = Math.Max(peak, e);
            var key = t.Year * 4 + (t.Month - 1) / 3;
            if (month != -1 && key != month) res.Add(last);
            month = key;
            last = (t, e / peak - 1);
        }
        res.Add(last);
        return res;
    }

    // ------------------------------------------------------------------ data

    private static Dictionary<string, List<Ohlcv>>? LoadCache(string path)
    {
        if (!File.Exists(path)) return null;
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var raw = new Dictionary<string, List<Ohlcv>>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in doc.RootElement.GetProperty("series").EnumerateArray())
        {
            var sym = s.GetProperty("symbol").GetString()!;
            var list = new List<Ohlcv>();
            foreach (var b in s.GetProperty("bars").EnumerateArray())
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(b.GetProperty("t").GetInt64()).UtcDateTime.Date;
                dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                if (dt > End) continue;
                var c = b.GetProperty("c").GetDecimal();
                list.Add(new Ohlcv { Symbol = sym, OpenDateTime = dt, CloseDateTime = dt.AddDays(1), Open = c, High = c, Low = c, Close = c, Volume = 1m });
            }
            raw[sym] = list.GroupBy(b => b.OpenDateTime).Select(g => g.First()).OrderBy(b => b.OpenDateTime).ToList();
        }

        // 只保留三个标的都有数据的交易日，避免某天缺腿 / keep only days on which all three symbols trade.
        var common = raw.Values.Select(l => l.Select(b => b.OpenDateTime).ToHashSet()).Aggregate((a, b) => { a.IntersectWith(b); return a; });
        var aligned = raw.ToDictionary(kv => kv.Key, kv => kv.Value.Where(b => common.Contains(b.OpenDateTime)).ToList(), StringComparer.OrdinalIgnoreCase);
        var first = aligned.Values.First();
        _firstYear = first[0].OpenDateTime.Year;
        _lastYear = first[^1].OpenDateTime.Year;
        return aligned;
    }

    private static string FindRepoRoot()
    {
        var envRoot = Environment.GetEnvironmentVariable("QUANT_INFRA_NET_ROOT");
        if (!string.IsNullOrEmpty(envRoot) && Directory.Exists(Path.Combine(envRoot, "docs"))) return envRoot;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "docs")) && Directory.Exists(Path.Combine(dir.FullName, "src"))) return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }
}

internal static class StatsExtensions
{
    public static double StdDev(this IReadOnlyCollection<double> xs)
    {
        if (xs.Count < 2) return 0;
        var mean = xs.Average();
        return Math.Sqrt(xs.Sum(x => (x - mean) * (x - mean)) / (xs.Count - 1));
    }
}
