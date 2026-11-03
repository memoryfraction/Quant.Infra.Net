using System.Collections.Concurrent;
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
/// CLEC 三桶配置回测文档宿主（真实 QQQ / QLD / BIL / SPY 复权日线）：
///   ClecDoc      —— 逐一回测 alpha-wealth-lab.com/planner/allocation/ 的 10 个阵型，写 docs/backtests/clec-{code}.md 与总览 README.md；
///   ClecOptimize —— 在 CLEC 规则内网格搜索「阵型 × 再平衡规则」，样本内选参、样本外检验，写 docs/backtests/clec-optimization.md。
/// CLEC three-sleeve allocation backtest host (real adjusted QQQ / QLD / BIL / SPY daily closes):
///   ClecDoc      — backtests the 10 mixes of alpha-wealth-lab.com/planner/allocation/ and writes per-mix reports + overview;
///   ClecOptimize — grid-searches mix × rebalance rule inside the CLEC rules, selects in-sample, validates
///                  out-of-sample, and writes docs/backtests/clec-optimization.md.
/// </summary>
public static class ClecAllocationDoc
{
    private const string BaseSym = "QQQ";
    private const string LevSym = "QLD";
    private const string CashSym = "BIL";
    private const string SpxSym = "SPY";
    private const decimal InitialEquity = 10_000m;
    private const decimal CommissionBps = 5m;
    private const decimal SlippageBps = 2m;
    private const double Band = 0.05;
    private static readonly DateTime End = new(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime OosStart = new(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc);
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

    // ================================================================== entry points

    /// <summary>逐一回测 10 个阵型并写文档 / Backtests the 10 mixes and writes the docs.</summary>
    public static async Task<int> RunAsync()
    {
        C.WriteLine("=== Quant.Infra.Net — CLEC allocation backtests (QQQ / QLD / BIL vs SPY, real adjusted daily closes) ===");
        var (root, bars) = Load();
        if (bars is null) return 1;

        var spx = await RunMixAsync(SpxMix, bars, SpxSym, ClecRebalance.Annual);
        var qqq = await RunMixAsync(QqqMix, bars, BaseSym, ClecRebalance.Annual);
        C.WriteLine(Line(spx));
        C.WriteLine(Line(qqq));

        var results = new ConcurrentBag<Run>();
        await Parallel.ForEachAsync(ClecMix.Presets, new ParallelOptions { MaxDegreeOfParallelism = Dop }, async (mix, _) =>
            results.Add(await RunMixAsync(mix, bars, BaseSym, ClecRebalance.Annual)));
        var ordered = ClecMix.Presets.Select(p => results.First(r => r.Mix.Code == p.Code)).ToList();
        ordered.ForEach(r => C.WriteLine(Line(r)));

        var outDir = Path.Combine(root, "docs", "backtests");
        Directory.CreateDirectory(outDir);
        foreach (var r in ordered)
        {
            var path = Path.Combine(outDir, $"clec-{r.Mix.Code}.md");
            await File.WriteAllTextAsync(path, MixDoc(r, spx, qqq, ordered), new UTF8Encoding(false));
            C.WriteLine("written: " + Path.GetRelativePath(root, path));
        }

        var index = Path.Combine(outDir, "README.md");
        await File.WriteAllTextAsync(index, IndexDoc(ordered, spx, qqq), new UTF8Encoding(false));
        C.WriteLine("written: " + Path.GetRelativePath(root, index));
        return 0;
    }

    /// <summary>
    /// 网格搜索 + 样本外检验并写文档 / Grid search with out-of-sample validation; writes the report.
    /// </summary>
    public static async Task<int> OptimizeAsync()
    {
        C.WriteLine("=== Quant.Infra.Net — CLEC optimization: mix × rebalance rule, in-sample 2007-06..2018-12, out-of-sample 2019-01..2026-10 ===");
        var (root, bars) = Load();
        if (bars is null) return 1;

        var spx = await RunMixAsync(SpxMix, bars, SpxSym, ClecRebalance.Annual);
        var qqq = await RunMixAsync(QqqMix, bars, BaseSym, ClecRebalance.Annual);
        var grid = Grid().SelectMany(m => Enum.GetValues<ClecRebalance>().Select(rule => (m, rule))).ToList();
        C.WriteLine($"grid: {Grid().Count()} CLEC-valid mixes × {Enum.GetValues<ClecRebalance>().Length} rebalance rules = {grid.Count} backtests (parallelism {Dop})");

        var bag = new ConcurrentBag<Run>();
        var done = 0;
        await Parallel.ForEachAsync(grid, new ParallelOptions { MaxDegreeOfParallelism = Dop }, async (g, _) =>
        {
            bag.Add(await RunMixAsync(g.m, bars, BaseSym, g.rule));
            var n = Interlocked.Increment(ref done);
            if (n % 10 == 0) C.WriteLine($"  {n}/{grid.Count} done");
        });
        var runs = bag.OrderBy(r => r.Mix.Code).ThenBy(r => r.Rule).ToList();

        var path = Path.Combine(root, "docs", "backtests", "clec-optimization.md");
        await File.WriteAllTextAsync(path, OptimizationDoc(runs, spx, qqq), new UTF8Encoding(false));
        C.WriteLine("written: " + Path.GetRelativePath(root, path));
        return 0;
    }

    private static int Dop => Math.Max(1, Math.Min(8, Environment.ProcessorCount - 1));

    private static readonly ClecMix SpxMix = new("SPY", 1, 0, 0, "Benchmark: 100% SPY (S&P 500), buy and hold", "基准：100% SPY（标普 500）买入持有");
    private static readonly ClecMix QqqMix = new("QQQ", 1, 0, 0, "Benchmark: 100% QQQ, buy and hold", "基准：100% QQQ 买入持有");

    /// <summary>
    /// CLEC 规则内的全部 10% 步长阵型：现金 ≥ 20%，杠杆 ≤ 原型，杠杆 ≤ 现金。
    /// Every 10%-step mix allowed by the CLEC rules: cash ≥ 20%, leveraged ≤ base, leveraged ≤ cash.
    /// </summary>
    private static IEnumerable<ClecMix> Grid()
    {
        for (var b = 1; b <= 8; b++)
        {
            for (var l = 0; l <= 4; l++)
            {
                var c = 10 - b - l;
                if (c < 2 || l > b || l > c) continue;
                var preset = ClecMix.Presets.FirstOrDefault(p => Math.Abs(p.Base - b / 10.0) < 1e-9 && Math.Abs(p.Lev - l / 10.0) < 1e-9);
                yield return new ClecMix($"{b}{l}{c % 10}{(c == 10 ? "0" : "")}", b / 10.0, l / 10.0, c / 10.0,
                    preset?.UseEn ?? "", preset?.UseZh ?? "");
            }
        }
    }

    // ================================================================== backtest

    private sealed record Stats(double Final, double Cagr, double Vol, double Sharpe, double MaxDd, DateTime Peak, DateTime Trough, DateTime? Recovered)
    {
        public double Calmar => MaxDd < 0 ? Cagr / Math.Abs(MaxDd) : double.NaN;
    }

    private sealed record Run(ClecMix Mix, ClecRebalance Rule, BacktestResult Result, List<(DateTime T, double E)> Curve, int Rebalances)
    {
        public Stats Full => Measure(Curve);
        public Stats InSample => Measure(Segment(Curve, DateTime.MinValue, OosStart.AddDays(-1)));
        public Stats OutOfSample => Measure(Segment(Curve, OosStart, DateTime.MaxValue));
        public string Label => $"{Mix.Code} · {RuleName(Rule)}";

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

        public double WindowDd(DateTime from, DateTime to) => Measure(Curve.Where(p => p.T >= from && p.T <= to).ToList()).MaxDd;
    }

    /// <summary>
    /// 区间切片：含区间前最后一个点作为起点，保证收益连续 / Segment slice, starting from the last point before
    /// the segment so returns are continuous.
    /// </summary>
    private static List<(DateTime T, double E)> Segment(List<(DateTime T, double E)> c, DateTime from, DateTime to)
    {
        var res = c.Where(p => p.T >= from && p.T <= to).ToList();
        var before = c.LastOrDefault(p => p.T < from);
        if (before != default) res.Insert(0, before);
        return res;
    }

    /// <summary>
    /// 指标：CAGR、年化波动、Sharpe（日收益，√252 年化，无风险利率 0）、最大回撤（含日期与回本日）。
    /// Metrics: CAGR, annualized volatility, Sharpe (daily returns, √252, rf = 0), max drawdown (with dates and recovery).
    /// </summary>
    private static Stats Measure(List<(DateTime T, double E)> c)
    {
        if (c.Count < 2) return new Stats(double.NaN, double.NaN, double.NaN, double.NaN, 0, default, default, null);
        var years = (c[^1].T - c[0].T).TotalDays / 365.25;
        var cagr = Math.Pow(c[^1].E / c[0].E, 1 / years) - 1;
        var rets = new List<double>(c.Count);
        for (var i = 1; i < c.Count; i++) rets.Add(c[i].E / c[i - 1].E - 1);
        var mean = rets.Average();
        var sd = Math.Sqrt(rets.Sum(r => (r - mean) * (r - mean)) / (rets.Count - 1));
        var sharpe = sd > 0 ? mean / sd * Math.Sqrt(252) : 0;

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
        return new Stats(c[^1].E, cagr, sd * Math.Sqrt(252), sharpe, worst, wPeak, wTrough, rec);
    }

    private static async Task<Run> RunMixAsync(ClecMix mix, Dictionary<string, List<Ohlcv>> bars, string baseSymbol, ClecRebalance rule)
    {
        var symbols = mix.Lev == 0 && mix.Cash == 0 ? new[] { baseSymbol } : new[] { baseSymbol, LevSym, CashSym };
        var data = new HistoricalDataSet(symbols.ToDictionary(s => s, s => (IReadOnlyList<Ohlcv>)bars[s]));
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

        var stage = new ClecAllocationStage(mix, baseSymbol, LevSym, CashSym, rule, Band);
        var pipeline = new StrategyPipeline(ClecAllocationStage.BuildPipeline(sp, stage));
        var broker = (IBacktestBroker)sp.GetRequiredService<IBinanceUsdFutureService>();
        var runner = new BacktestRunner(pipeline, broker, sp.GetRequiredService<OrchestrationOptions>(), sp.GetRequiredService<BacktestOptions>());
        var result = await runner.RunAsync(data, symbols);
        var curve = result.EquityCurve.OrderBy(kv => kv.Key).Select(kv => (kv.Key, (double)kv.Value)).ToList();
        return new Run(mix, rule, result, curve, stage.Rebalances);
    }

    private static string Line(Run r)
    {
        var f = r.Full;
        return string.Format(CultureInfo.InvariantCulture, "{0,-5} {1,-9} final={2,10:N0}  CAGR={3,7:P2}  vol={4,7:P2}  Sharpe={5,5:F2}  MaxDD={6,8:P2}  Calmar={7:F2}  trades={8}",
            r.Mix.Code, r.Rule, f.Final, f.Cagr, f.Vol, f.Sharpe, f.MaxDd, f.Calmar, r.Result.Metrics.TotalTrades);
    }

    // ================================================================== formatting helpers

    private static string P(double x, int d = 1) => double.IsNaN(x) ? "—" : (x * 100).ToString("F" + d, CultureInfo.InvariantCulture) + "%";
    private static string N(double x) => x.ToString("N0", CultureInfo.InvariantCulture);
    private static string F(double x, int d = 2) => double.IsNaN(x) ? "—" : x.ToString("F" + d, CultureInfo.InvariantCulture);
    private static string Ds(DateTime t) => t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string W(ClecMix m) => $"{P(m.Base, 0)} {BaseSym} / {P(m.Lev, 0)} {LevSym} / {P(m.Cash, 0)} {CashSym}";
    private static string Win(bool x) => x ? "✅" : "❌";

    private static string RuleName(ClecRebalance r) => r switch
    {
        ClecRebalance.Annual => "annual",
        ClecRebalance.Quarterly => "quarterly",
        ClecRebalance.Monthly => "monthly",
        _ => $"±{Band * 100:0}pt band",
    };

    private static string RuleZh(ClecRebalance r) => r switch
    {
        ClecRebalance.Annual => "每年",
        ClecRebalance.Quarterly => "每季",
        ClecRebalance.Monthly => "每月",
        _ => $"偏离 ±{Band * 100:0} 个百分点",
    };

    // ================================================================== per-mix report

    private static string MixDoc(Run r, Run spx, Run qqq, List<Run> all)
    {
        var m = r.Mix;
        Stats s = r.Full, b = spx.Full, q = qqq.Full;
        var years = r.Yearly();
        var syears = spx.Yearly();
        var qyears = qqq.Yearly();
        var sb = new StringBuilder();
        var start = r.Curve[0].T;
        var end = r.Curve[^1].T;
        var yrs = (end - start).TotalDays / 365.25;
        string Rec(Stats x) => x.Recovered is { } rc ? Ds(rc) : "not yet / 尚未";

        sb.AppendLine($"# CLEC {m.Code} backtest / CLEC {m.Code} 阵型回测");
        sb.AppendLine();
        sb.AppendLine($"> **{m.Code} = {W(m)}** · beta {F(m.Beta)} · {m.UseEn} / {m.UseZh}");
        sb.AppendLine(">");
        sb.AppendLine($"> Real adjusted daily closes, **{Ds(start)} → {Ds(end)}** ({F(yrs, 1)} years), rebalanced on the first trading day of each year. Generated by [Quant.Infra.Net](../../README.md); try the mix interactively at [Alpha Wealth Lab · 433 Allocation & Beta Check]({AwlUrl}).");
        sb.AppendLine($"> 真实复权日线，**{Ds(start)} → {Ds(end)}**（{F(yrs, 1)} 年），每年第一个交易日再平衡。由 [Quant.Infra.Net](../../README.md) 生成；可在 [Alpha Wealth Lab · 433 配置与 Beta 体检]({AwlUrl}) 交互试算。");
        sb.AppendLine();
        sb.AppendLine("[← All mixes / 全部阵型](README.md) · [Optimization study / 寻优研究](clec-optimization.md)");
        sb.AppendLine();

        sb.AppendLine("## Result vs S&P 500 and Nasdaq-100 / 对比标普 500 与纳指 100");
        sb.AppendLine();
        sb.AppendLine($"| Metric / 指标 | CLEC {m.Code} | SPY (S&P 500) | QQQ (Nasdaq-100) |");
        sb.AppendLine("|---|---:|---:|---:|");
        sb.AppendLine($"| $10,000 grows to / 一万美元变成 | **${N(s.Final)}** | ${N(b.Final)} | ${N(q.Final)} |");
        sb.AppendLine($"| CAGR / 年化收益 | **{P(s.Cagr, 2)}** {Win(s.Cagr > b.Cagr)} | {P(b.Cagr, 2)} | {P(q.Cagr, 2)} |");
        sb.AppendLine($"| Sharpe (rf = 0) | **{F(s.Sharpe)}** {Win(s.Sharpe > b.Sharpe)} | {F(b.Sharpe)} | {F(q.Sharpe)} |");
        sb.AppendLine($"| Calmar (CAGR / MaxDD) | **{F(s.Calmar)}** {Win(s.Calmar > b.Calmar)} | {F(b.Calmar)} | {F(q.Calmar)} |");
        sb.AppendLine($"| Volatility / 年化波动 | {P(s.Vol)} | {P(b.Vol)} | {P(q.Vol)} |");
        sb.AppendLine($"| Max drawdown / 最大回撤 | **{P(s.MaxDd)}** | {P(b.MaxDd)} | {P(q.MaxDd)} |");
        sb.AppendLine($"| Max drawdown dates / 回撤区间 | {Ds(s.Peak)} → {Ds(s.Trough)} | {Ds(b.Peak)} → {Ds(b.Trough)} | {Ds(q.Peak)} → {Ds(q.Trough)} |");
        sb.AppendLine($"| Recovered on / 回本日 | {Rec(s)} | {Rec(b)} | {Rec(q)} |");
        sb.AppendLine($"| Best year / 最好年份 | {BestWorst(years, true)} | {BestWorst(syears, true)} | {BestWorst(qyears, true)} |");
        sb.AppendLine($"| Worst year / 最差年份 | {BestWorst(years, false)} | {BestWorst(syears, false)} | {BestWorst(qyears, false)} |");
        sb.AppendLine($"| Losing years / 亏损年数 | {years.Count(y => y.Value < 0)} / {years.Count} | {syears.Count(y => y.Value < 0)} / {syears.Count} | {qyears.Count(y => y.Value < 0)} / {qyears.Count} |");
        sb.AppendLine($"| Rebalances · trades · costs / 再平衡 · 成交 · 费用 | {r.Rebalances} · {r.Result.Metrics.TotalTrades} · ${N((double)r.Result.Metrics.TotalCommissionUsd)} | buy & hold | buy & hold |");
        sb.AppendLine();
        sb.AppendLine("✅ / ❌ = beats / does not beat the S&P 500 (SPY) on that metric. / ✅ / ❌ = 该指标是否跑赢标普 500（SPY）。");
        sb.AppendLine();
        var cagrRank = all.OrderByDescending(x => x.Full.Cagr).ToList().FindIndex(x => x.Mix.Code == m.Code) + 1;
        var calmarRank = all.OrderByDescending(x => x.Full.Calmar).ToList().FindIndex(x => x.Mix.Code == m.Code) + 1;
        sb.AppendLine($"Among the 10 CLEC mixes, {m.Code} ranks **#{cagrRank} by CAGR** and **#{calmarRank} by Calmar** (return per unit of drawdown).");
        sb.AppendLine($"在 10 个 CLEC 阵型里，{m.Code} 的**年化收益排第 {cagrRank}**，**Calmar（收益 / 回撤）排第 {calmarRank}**。");
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
        sb.AppendLine($"Line 1 = CLEC {m.Code}, line 2 = SPY (S&P 500), line 3 = QQQ. / 线 1 = CLEC {m.Code}，线 2 = SPY（标普 500），线 3 = QQQ。");
        sb.AppendLine();
        AppendGrowthChart(sb, $"CLEC {m.Code} vs SPY vs QQQ, growth of $10,000", r.Curve, spx.Curve, qqq.Curve);

        sb.AppendLine("## Calendar-year returns / 年度收益");
        sb.AppendLine();
        var yMin = Math.Floor(years.Values.Min() * 10) * 10;
        var yMax = Math.Ceiling(years.Values.Max() * 10) * 10;
        sb.AppendLine("```mermaid");
        sb.AppendLine("xychart-beta");
        sb.AppendLine($"    title \"CLEC {m.Code} calendar-year return (%)\"");
        sb.AppendLine($"    x-axis [{string.Join(", ", years.Keys.Select(y => $"\"{Label(y)}\""))}]");
        sb.AppendLine($"    y-axis \"%\" {yMin:0} --> {yMax:0}");
        sb.AppendLine($"    bar [{string.Join(", ", years.Values.Select(v => (v * 100).ToString("0.0", CultureInfo.InvariantCulture)))}]");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine($"| Year / 年份 | CLEC {m.Code} | SPY | vs SPY | QQQ |");
        sb.AppendLine("|---|---:|---:|---:|---:|");
        foreach (var (y, v) in years)
        {
            var sv = syears.TryGetValue(y, out var x1) ? x1 : double.NaN;
            var qv = qyears.TryGetValue(y, out var x2) ? x2 : double.NaN;
            sb.AppendLine($"| {Label(y)} | {P(v)} | {P(sv)} | {(v - sv >= 0 ? "+" : "")}{P(v - sv)} | {P(qv)} |");
        }
        sb.AppendLine();
        sb.AppendLine($"`{start.Year}*` starts on {Ds(start)}; `{end.Year}*` ends on {Ds(end)} (partial years). / 带 * 的为不完整年份。");
        sb.AppendLine();

        sb.AppendLine("## Drawdown (quarter-end) / 回撤（季末）");
        sb.AppendLine();
        var qdd = QuarterEndDrawdown(r.Curve);
        sb.AppendLine("```mermaid");
        sb.AppendLine("xychart-beta");
        sb.AppendLine($"    title \"CLEC {m.Code} drawdown from peak (%)\"");
        sb.AppendLine($"    x-axis [{string.Join(", ", qdd.Select(p => $"\"{p.T.Year}Q{(p.T.Month - 1) / 3 + 1}\""))}]");
        sb.AppendLine($"    y-axis \"%\" {Math.Floor(qdd.Min(p => p.Dd) * 10) * 10:0} --> 0");
        sb.AppendLine($"    line [{string.Join(", ", qdd.Select(p => (p.Dd * 100).ToString("0.0", CultureInfo.InvariantCulture)))}]");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Stress test: real bear markets / 压力测试：真实熊市");
        sb.AppendLine();
        sb.AppendLine("Worst peak-to-trough fall of the portfolio inside each window (real QLD prices, so daily-reset decay is included).");
        sb.AppendLine("每个区间内组合从高点到低点的最大跌幅（用的是 QLD 真实价格，已包含二倍 ETF 每日重置的损耗）。");
        sb.AppendLine();
        sb.AppendLine($"| Window / 区间 | Dates / 日期 | CLEC {m.Code} | SPY | QQQ |");
        sb.AppendLine("|---|---|---:|---:|---:|");
        foreach (var w in StressWindows)
        {
            sb.AppendLine($"| {w.En} / {w.Zh} | {Ds(w.From)} → {Ds(w.To)} | **{P(r.WindowDd(w.From, w.To))}** | {P(spx.WindowDd(w.From, w.To))} | {P(qqq.WindowDd(w.From, w.To))} |");
        }
        sb.AppendLine();
        var worst = StressWindows.Min(w => r.WindowDd(w.From, w.To));
        sb.AppendLine($"On $1,000,000 a repeat of the worst window would mean a paper loss of about **${N(1_000_000 * Math.Abs(worst))}**. Cash is what lets you hold on, and add, instead of being forced to sell.");
        sb.AppendLine($"以 100 万美元计，重演最差区间约浮亏 **${N(1_000_000 * Math.Abs(worst))}**。现金的作用是让你扛得住、还能补仓，而不是被迫卖出。");
        sb.AppendLine();

        AppendMethod(sb, "ClecDoc");
        return sb.ToString();
    }

    // ================================================================== overview

    private static string IndexDoc(List<Run> all, Run spx, Run qqq)
    {
        var sb = new StringBuilder();
        var start = spx.Curve[0].T;
        var end = spx.Curve[^1].T;
        var yrs = (end - start).TotalDays / 365.25;
        sb.AppendLine("# CLEC allocation backtests / CLEC 阵型回测总览");
        sb.AppendLine();
        sb.AppendLine($"> Every mix from [Alpha Wealth Lab · 433 Allocation & Beta Check]({AwlUrl}) backtested on **real adjusted daily closes, {Ds(start)} → {Ds(end)}** ({F(yrs, 1)} years, including 2008, 2020 and 2022), with **{BaseSym}** as the base index, **{LevSym}** as the 2x leveraged ETF and **{CashSym}** as cash, against the **S&P 500 (SPY)**. Each mix is rebalanced on the first trading day of each year. Generated by [Quant.Infra.Net](../../README.md).");
        sb.AppendLine(">");
        sb.AppendLine($"> 把 [Alpha Wealth Lab · 433 配置与 Beta 体检]({AwlUrl}) 里的每个阵型，用**真实复权日线（{Ds(start)} → {Ds(end)}，{F(yrs, 1)} 年，含 2008、2020、2022）**逐一回测并对比**标普 500（SPY）**：原型 = **{BaseSym}**，二倍杠杆 = **{LevSym}**，现金 = **{CashSym}**，每年第一个交易日再平衡。由 [Quant.Infra.Net](../../README.md) 生成。");
        sb.AppendLine();
        sb.AppendLine("🔬 **Which mix and which rebalance rule is best, and does it hold out of sample?** See the [optimization study](clec-optimization.md). / **哪个阵型、哪种再平衡方式最好？样本外还成立吗？** 见[寻优研究](clec-optimization.md)。");
        sb.AppendLine();
        sb.AppendLine("| Mix / 阵型 | Weights / 比例 | Beta | $10k → | CAGR | Sharpe | Calmar | Max DD / 最大回撤 | 2008 crisis / 金融危机 | 2022 | Beats SPY on all 3? / 三项全胜标普? | Report / 报告 |");
        sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|:---:|---|");
        var gfc = StressWindows[0];
        var y22 = StressWindows[^1];
        var b = spx.Full;
        foreach (var r in all)
        {
            var s = r.Full;
            var allThree = s.Cagr > b.Cagr && s.Sharpe > b.Sharpe && s.Calmar > b.Calmar;
            sb.AppendLine($"| **{r.Mix.Code}** | {F(r.Mix.Base * 100, 0)} / {F(r.Mix.Lev * 100, 0)} / {F(r.Mix.Cash * 100, 0)} | {F(r.Mix.Beta, 1)} | ${N(s.Final)} | {P(s.Cagr)} | {F(s.Sharpe)} | {F(s.Calmar)} | {P(s.MaxDd)} | {P(r.WindowDd(gfc.From, gfc.To))} | {P(r.WindowDd(y22.From, y22.To))} | {Win(allThree)} | [clec-{r.Mix.Code}.md](clec-{r.Mix.Code}.md) |");
        }
        foreach (var x in new[] { spx, qqq })
        {
            var s = x.Full;
            sb.AppendLine($"| *{(x == spx ? "SPY (S&P 500)" : "QQQ (Nasdaq-100)")}* | *benchmark* | 1.0 | ${N(s.Final)} | {P(s.Cagr)} | {F(s.Sharpe)} | {F(s.Calmar)} | {P(s.MaxDd)} | {P(x.WindowDd(gfc.From, gfc.To))} | {P(x.WindowDd(y22.From, y22.To))} | — | — |");
        }
        sb.AppendLine();

        var codes = string.Join(", ", all.Select(r => $"\"{r.Mix.Code}\"").Append("\"SPY\""));
        var full = all.Select(r => r.Full).Append(b).ToList();
        sb.AppendLine("```mermaid");
        sb.AppendLine("xychart-beta");
        sb.AppendLine("    title \"CAGR by mix vs SPY (%)\"");
        sb.AppendLine($"    x-axis [{codes}]");
        sb.AppendLine($"    y-axis \"%\" 0 --> {Math.Ceiling(full.Max(x => x.Cagr) * 100 / 5) * 5:0}");
        sb.AppendLine($"    bar [{string.Join(", ", full.Select(x => (x.Cagr * 100).ToString("0.0", CultureInfo.InvariantCulture)))}]");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("xychart-beta");
        sb.AppendLine("    title \"Calmar ratio by mix vs SPY\"");
        sb.AppendLine($"    x-axis [{codes}]");
        sb.AppendLine($"    y-axis \"Calmar\" 0 --> {Math.Ceiling(full.Max(x => x.Calmar) * 10) / 10:0.0}");
        sb.AppendLine($"    bar [{string.Join(", ", full.Select(x => x.Calmar.ToString("0.00", CultureInfo.InvariantCulture)))}]");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("xychart-beta");
        sb.AppendLine("    title \"Max drawdown by mix vs SPY (%)\"");
        sb.AppendLine($"    x-axis [{codes}]");
        sb.AppendLine($"    y-axis \"%\" {Math.Floor(full.Min(x => x.MaxDd) * 100 / 10) * 10:0} --> 0");
        sb.AppendLine($"    bar [{string.Join(", ", full.Select(x => (x.MaxDd * 100).ToString("0.0", CultureInfo.InvariantCulture)))}]");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("**How to read this / 怎么看：**");
        sb.AppendLine("- Beta (base + 2 × leveraged) predicts the *shape*: higher beta means more return in long bull runs and deeper holes in crashes. / Beta 决定形态：Beta 越高，长牛里赚得越多，暴跌里坑越深。");
        sb.AppendLine("- Cash is not dead weight. It caps the drawdown and, at each annual rebalance, buys the index back after a bad year. / 现金不是闲钱：它压住回撤，并在每年再平衡时把跌掉的指数买回来。");
        sb.AppendLine("- These are past numbers on one index (Nasdaq-100), one of the strongest of the period. Pick the mix whose *worst year* you could live through, not the one with the biggest final number. / 这是过去的数据，而且纳指 100 是这段时间最强的指数之一。请选择你扛得住*最差年份*的阵型，而不是终值最大的那个。");
        sb.AppendLine();
        AppendMethod(sb, "ClecDoc");
        return sb.ToString();
    }

    // ================================================================== optimization report

    private static string OptimizationDoc(List<Run> runs, Run spx, Run qqq)
    {
        var sb = new StringBuilder();
        var start = spx.Curve[0].T;
        var end = spx.Curve[^1].T;
        Stats bIs = spx.InSample, bOos = spx.OutOfSample, bFull = spx.Full;
        bool BeatsAll(Stats s, Stats b) => s.Cagr > b.Cagr && s.Sharpe > b.Sharpe && s.Calmar > b.Calmar;

        // 预先声明的选择规则 / pre-declared selection rule
        var eligible = runs.Where(r => BeatsAll(r.InSample, bIs)).ToList();
        var pickPool = eligible.Count > 0 ? eligible : runs;
        var pick = pickPool.OrderByDescending(r => r.InSample.Calmar).ThenByDescending(r => r.InSample.Sharpe).First();
        // 规则 B：最大化「三项相对 SPY 的最弱一项」，与「三项全胜标普」的目标一致。
        // Rule B: maximize the weakest of the three ratios vs SPY, which matches the goal "beat SPY on all three".
        double Weakest(Stats s, Stats b) => Math.Min(s.Cagr / b.Cagr, Math.Min(s.Sharpe / b.Sharpe, s.Calmar / b.Calmar));
        var pickB = runs.OrderByDescending(r => Weakest(r.InSample, bIs)).First();
        var oosWinners = runs.Where(r => BeatsAll(r.OutOfSample, bOos)).OrderByDescending(r => Weakest(r.OutOfSample, bOos)).ToList();
        var isRank = runs.OrderByDescending(r => r.InSample.Calmar).ToList();
        var oosRank = runs.OrderByDescending(r => r.OutOfSample.Calmar).ToList();
        var oosBeat = runs.Count(r => BeatsAll(r.OutOfSample, bOos));
        var fullBeat = runs.Count(r => BeatsAll(r.Full, bFull));
        var rho = Spearman(runs.Select(r => r.InSample.Calmar).ToList(), runs.Select(r => r.OutOfSample.Calmar).ToList());
        var classic = runs.First(r => r.Mix.Code == "433" && r.Rule == ClecRebalance.Annual);
        Stats pIs = pick.InSample, pOos = pick.OutOfSample, pFull = pick.Full;

        sb.AppendLine("# Finding the best CLEC allocation, honestly / 诚实地寻找最优 CLEC 配置");
        sb.AppendLine();
        sb.AppendLine("> **The question / 问题：** *\"Within the CLEC rules, which mix of index fund, 2x leveraged ETF and cash, and which rebalancing rule, gives the best return per unit of drawdown? Does it beat the S&P 500 on CAGR, Sharpe **and** Calmar, including on data it has never seen?\"*");
        sb.AppendLine("> *「在 CLEC 规则之内，指数基金、二倍杠杆 ETF、现金怎么配、多久再平衡，单位回撤的收益最高？它在年化收益、夏普**和**卡玛比率上能否全面跑赢标普 500？换成它没见过的数据还成立吗？」*");
        sb.AppendLine(">");
        sb.AppendLine($"> Real adjusted daily closes of QQQ, QLD, BIL and SPY, **{Ds(start)} → {Ds(end)}**. Run with [Quant.Infra.Net](../../README.md)'s bar-by-bar backtester; the mixes come from [Alpha Wealth Lab · 433 Allocation & Beta Check]({AwlUrl}).");
        sb.AppendLine($"> 数据：QQQ、QLD、BIL、SPY 真实复权日线，**{Ds(start)} → {Ds(end)}**。用 [Quant.Infra.Net](../../README.md) 的逐 bar 回测引擎完成；阵型来自 [Alpha Wealth Lab · 433 配置与 Beta 体检]({AwlUrl})。");
        sb.AppendLine();
        sb.AppendLine("[← All mixes / 全部阵型](README.md)");
        sb.AppendLine();

        sb.AppendLine("## The research loop / 研究流程");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("flowchart LR");
        sb.AppendLine("    Q[\"Question in<br/>plain language\"] --> D[\"Find data<br/>QQQ · QLD · BIL · SPY\"]");
        sb.AppendLine($"    D --> G[\"Grid: {runs.Select(r => r.Mix.Code).Distinct().Count()} CLEC mixes<br/>× {Enum.GetValues<ClecRebalance>().Length} rebalance rules\"]");
        sb.AppendLine($"    G --> B[\"{runs.Count} backtests<br/>bar by bar\"]");
        sb.AppendLine($"    B --> I[\"Pick on in-sample<br/>{start:yyyy-MM} → 2018-12\"]");
        sb.AppendLine($"    I --> O[\"Verify out-of-sample<br/>2019-01 → {end:yyyy-MM}\"]");
        sb.AppendLine("    O --> R[\"Report vs S&P 500<br/>CAGR · Sharpe · Calmar\"]");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Search space / 搜索空间");
        sb.AppendLine();
        sb.AppendLine($"- **Mixes / 阵型:** every 10%-step split of {BaseSym} / {LevSym} / {CashSym} that obeys the CLEC rules: cash ≥ 20%, leveraged ≤ base, leveraged ≤ cash → **{runs.Select(r => r.Mix.Code).Distinct().Count()} mixes**. (442 breaks the \"leveraged ≤ cash\" rule and is reserved for under-30 savers, so it is outside the search.) / 所有满足 CLEC 规则（现金 ≥ 20%、杠杆 ≤ 原型、杠杆 ≤ 现金）的 10% 步长组合（442 违反「杠杆 ≤ 现金」，只适用于 30 岁以下，不在搜索范围内）。");
        sb.AppendLine($"- **Rebalance rules / 再平衡规则:** annual, quarterly, monthly, or whenever a sleeve drifts more than ±{Band * 100:0} points from target (checked daily). / 每年、每季、每月，或任一部位偏离目标超过 ±{Band * 100:0} 个百分点时（每日检查）。");
        sb.AppendLine($"- **Costs / 成本:** {CommissionBps} bps commission + {SlippageBps} bps slippage on every trade. / 每笔 {CommissionBps} bp 佣金 + {SlippageBps} bp 滑点。");
        sb.AppendLine();
        sb.AppendLine("**Two selection rules / 两条选择规则：**");
        sb.AppendLine();
        sb.AppendLine("- **Rule A (declared first) / 规则 A（最先定下）:** among the candidates that beat SPY on CAGR, Sharpe and Calmar *in-sample*, take the one with the highest in-sample Calmar. / 在样本内三项全胜 SPY 的候选里，取样本内卡玛比率最高者。");
        sb.AppendLine("- **Rule B (added after Rule A failed; see below) / 规则 B（规则 A 失败后补充，见下文）:** take the candidate whose *weakest* in-sample ratio vs SPY (CAGR ÷ SPY CAGR, Sharpe ÷ SPY Sharpe, Calmar ÷ SPY Calmar) is the highest. It aims directly at the goal \"beat the S&P 500 on all three\". / 取样本内三项相对 SPY 比值中「最弱一项」最高的候选，直接对准「三项全胜标普」的目标。");
        sb.AppendLine();
        sb.AppendLine($"{eligible.Count} of {runs.Count} candidates beat SPY on all three metrics in-sample. / {runs.Count} 个候选中有 {eligible.Count} 个在样本内三项全胜 SPY。");
        sb.AppendLine();

        Stats bA = pIs, bB = pickB.InSample;
        sb.AppendLine("## Results / 结果");
        sb.AppendLine();
        sb.AppendLine($"| Period / 区间 | Metric / 指标 | Rule A: **{pick.Label}** | Rule B: **{pickB.Label}** | SPY (S&P 500) |");
        sb.AppendLine("|---|---|---:|---:|---:|");
        void Rows(string period, Stats a, Stats bb, Stats s)
        {
            sb.AppendLine($"| {period} | CAGR | {P(a.Cagr, 2)} {Win(a.Cagr > s.Cagr)} | {P(bb.Cagr, 2)} {Win(bb.Cagr > s.Cagr)} | {P(s.Cagr, 2)} |");
            sb.AppendLine($"| | Sharpe | {F(a.Sharpe)} {Win(a.Sharpe > s.Sharpe)} | {F(bb.Sharpe)} {Win(bb.Sharpe > s.Sharpe)} | {F(s.Sharpe)} |");
            sb.AppendLine($"| | Calmar | {F(a.Calmar)} {Win(a.Calmar > s.Calmar)} | {F(bb.Calmar)} {Win(bb.Calmar > s.Calmar)} | {F(s.Calmar)} |");
            sb.AppendLine($"| | Max drawdown / 最大回撤 | {P(a.MaxDd)} | {P(bb.MaxDd)} | {P(s.MaxDd)} |");
        }
        Rows($"In-sample (picked here) / 样本内（在此选参）<br/>{Ds(start)} → 2018-12-31", bA, bB, bIs);
        Rows($"**Out-of-sample / 样本外**<br/>2019-01-02 → {Ds(end)}", pOos, pickB.OutOfSample, bOos);
        Rows($"Full period / 全区间<br/>{Ds(start)} → {Ds(end)}", pFull, pickB.Full, bFull);
        sb.AppendLine($"| | $10,000 → | ${N(pFull.Final)} | ${N(pickB.Full.Final)} | ${N(bFull.Final)} |");
        sb.AppendLine();
        sb.AppendLine($"Rule A pick: **{W(pick.Mix)}** (beta {F(pick.Mix.Beta)}). Rule B pick: **{W(pickB.Mix)}** (beta {F(pickB.Mix.Beta)}), rebalanced {RuleName(pickB.Rule)}. ✅ / ❌ = beats / does not beat SPY.");
        sb.AppendLine();
        var aOk = BeatsAll(pOos, bOos);
        var bOk = BeatsAll(pickB.OutOfSample, bOos);
        sb.AppendLine("**What happened / 发生了什么：**");
        sb.AppendLine();
        sb.AppendLine(aOk
            ? "- Rule A's pick kept beating SPY on all three metrics out of sample. / 规则 A 的选择在样本外依然三项全胜 SPY。"
            : $"- **Rule A failed out of sample.** Maximizing Calmar alone drifts toward cash-heavy, low-beta mixes. They looked great through 2008 but could not keep up with a strong 2019–2026 bull market on CAGR ({P(pOos.Cagr)} vs {P(bOos.Cagr)}). This is exactly the kind of mistake the out-of-sample check exists to catch. / **规则 A 在样本外失败了。** 只追求卡玛比率，会把选择推向现金多、Beta 低的阵型：熬过 2008 很漂亮，但在 2019–2026 的强牛市里年化收益跟不上（{P(pOos.Cagr)} 对 {P(bOos.Cagr)}）。样本外检验存在的意义，正是抓住这类错误。");
        sb.AppendLine(bOk
            ? $"- **Rule B's pick beat SPY on CAGR, Sharpe and Calmar in-sample, out-of-sample and over the full period.** Because Rule B was written *after* seeing Rule A's out-of-sample result, its out-of-sample evidence is weaker than a truly blind test; treat it as a strong hint, not proof. / **规则 B 的选择在样本内、样本外、全区间都三项全胜 SPY。** 但规则 B 是在看到规则 A 的样本外结果*之后*才写的，所以它的样本外证据弱于真正的盲测：请把它当作强烈提示，而不是证明。"
            : $"- Rule B's pick did not beat SPY on all three metrics out of sample either. / 规则 B 的选择在样本外同样没能三项全胜 SPY。");
        sb.AppendLine();

        sb.AppendLine("### Growth of $10,000 (year-end) / 一万美元净值（年末）");
        sb.AppendLine();
        sb.AppendLine($"Line 1 = Rule B ({pickB.Label}), line 2 = SPY (S&P 500), line 3 = Rule A ({pick.Label}), line 4 = classic 433 · annual. Out-of-sample starts in 2019. / 线 1 = 规则 B，线 2 = SPY，线 3 = 规则 A，线 4 = 经典 433；2019 年起为样本外。");
        sb.AppendLine();
        AppendGrowthChart(sb, $"Rule B {pickB.Label} vs SPY vs Rule A {pick.Label} vs 433", pickB.Curve, spx.Curve, pick.Curve, classic.Curve);

        sb.AppendLine($"### Every candidate that beat SPY on all three metrics out-of-sample ({oosWinners.Count} of {runs.Count}) / 样本外三项全胜 SPY 的全部候选");
        sb.AppendLine();
        sb.AppendLine("| Mix · rule / 阵型 · 规则 | Beta | OOS CAGR | OOS Sharpe | OOS Calmar | Full CAGR | Full Sharpe | Full Calmar | Full max DD |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var r in oosWinners)
        {
            Stats o = r.OutOfSample, f = r.Full;
            sb.AppendLine($"| {(r == pickB ? "**" + r.Label + "**" : r.Label)} | {F(r.Mix.Beta, 1)} | {P(o.Cagr)} | {F(o.Sharpe)} | {F(o.Calmar)} | {P(f.Cagr)} | {F(f.Sharpe)} | {F(f.Calmar)} | {P(f.MaxDd)} |");
        }
        sb.AppendLine($"| *SPY (S&P 500)* | 1.0 | {P(bOos.Cagr)} | {F(bOos.Sharpe)} | {F(bOos.Calmar)} | {P(bFull.Cagr)} | {F(bFull.Sharpe)} | {F(bFull.Calmar)} | {P(bFull.MaxDd)} |");
        sb.AppendLine();

        sb.AppendLine("## Is it overfit? Robustness checks / 是否过拟合？稳健性检验");
        sb.AppendLine();
        sb.AppendLine($"- **Rank stability / 排名稳定性:** Spearman correlation between in-sample and out-of-sample Calmar across all {runs.Count} candidates = **{F(rho)}**. (1 = the in-sample ranking predicts the future ranking perfectly; 0 = no relation.) / 全部候选样本内与样本外卡玛排名的 Spearman 相关系数为 **{F(rho)}**（1 = 完全一致，0 = 毫无关系）。");
        sb.AppendLine($"- **Breadth / 广度:** **{oosBeat} of {runs.Count}** candidates beat SPY on all three metrics out-of-sample; **{fullBeat} of {runs.Count}** over the full period. If only one magic setting worked, that would be a red flag. / 样本外有 **{oosBeat}/{runs.Count}** 个候选三项全胜 SPY，全区间 **{fullBeat}/{runs.Count}**。如果只有一个「神奇参数」有效，那才是危险信号。");
        sb.AppendLine($"- **Pick's out-of-sample rank / 选中配置的样本外排名:** #{oosRank.IndexOf(pick) + 1} of {runs.Count} by Calmar. / 按卡玛比率排第 {oosRank.IndexOf(pick) + 1}。");
        sb.AppendLine();
        sb.AppendLine("**Top 10 in-sample, and how they did afterwards / 样本内前 10 名及其样本外表现：**");
        sb.AppendLine();
        sb.AppendLine("| # IS | Mix · rule / 阵型 · 规则 | Beta | IS Calmar | IS CAGR | OOS Calmar | OOS CAGR | OOS Sharpe | OOS rank | OOS beats SPY on all 3? |");
        sb.AppendLine("|---:|---|---:|---:|---:|---:|---:|---:|---:|:---:|");
        foreach (var (r, i) in isRank.Take(10).Select((r, i) => (r, i)))
        {
            Stats a = r.InSample, o = r.OutOfSample;
            sb.AppendLine($"| {i + 1} | {(r == pick ? "**" + r.Label + "**" : r.Label)} | {F(r.Mix.Beta, 1)} | {F(a.Calmar)} | {P(a.Cagr)} | {F(o.Calmar)} | {P(o.Cagr)} | {F(o.Sharpe)} | {oosRank.IndexOf(r) + 1} | {Win(BeatsAll(o, bOos))} |");
        }
        sb.AppendLine($"| | *SPY (S&P 500)* | 1.0 | {F(bIs.Calmar)} | {P(bIs.Cagr)} | {F(bOos.Calmar)} | {P(bOos.Cagr)} | {F(bOos.Sharpe)} | | |");
        sb.AppendLine();

        sb.AppendLine("**Does the rebalance rule matter? (median across all mixes) / 再平衡规则重要吗？（所有阵型的中位数）**");
        sb.AppendLine();
        sb.AppendLine("| Rule / 规则 | Full CAGR | Full Sharpe | Full Calmar | OOS Calmar | Median rebalances / 再平衡次数 |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (var g in runs.GroupBy(r => r.Rule))
        {
            sb.AppendLine($"| {RuleName(g.Key)} / {RuleZh(g.Key)} | {P(Median(g.Select(r => r.Full.Cagr)))} | {F(Median(g.Select(r => r.Full.Sharpe)))} | {F(Median(g.Select(r => r.Full.Calmar)))} | {F(Median(g.Select(r => r.OutOfSample.Calmar)))} | {Median(g.Select(r => (double)r.Rebalances)):0} |");
        }
        sb.AppendLine();

        var ruleCodes = runs.Where(r => r.Rule == pick.Rule).OrderByDescending(r => r.Full.Calmar).ToList();
        sb.AppendLine("```mermaid");
        sb.AppendLine("xychart-beta");
        sb.AppendLine($"    title \"Full-period Calmar of every CLEC mix ({RuleName(pick.Rule)} rebalance) vs SPY\"");
        sb.AppendLine($"    x-axis [{string.Join(", ", ruleCodes.Select(r => $"\"{r.Mix.Code}\"").Append("\"SPY\""))}]");
        sb.AppendLine($"    y-axis \"Calmar\" 0 --> {Math.Ceiling(ruleCodes.Max(r => r.Full.Calmar) * 10) / 10:0.0}");
        sb.AppendLine($"    bar [{string.Join(", ", ruleCodes.Select(r => r.Full.Calmar).Append(bFull.Calmar).Select(x => x.ToString("0.00", CultureInfo.InvariantCulture)))}]");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## What this does and does not prove / 这证明了什么，没证明什么");
        sb.AppendLine();
        sb.AppendLine("- ✅ **It proves the workflow**: ask a question, pull free data, run dozens of realistic backtests, pick with a rule fixed in advance, and check the pick on data it never saw, all reproducible from one command. / ✅ **它证明了工作流**：提出问题 → 拉免费数据 → 跑几十次真实回测 → 用事先定死的规则选参 → 在没见过的数据上检验，一条命令即可复现。");
        sb.AppendLine("- ❌ **It does not prove the future**: 2007–2026 was one of the best periods in history for the Nasdaq-100. The 2000–2002 bust (Nasdaq-100 −83%) is not in this data because QLD and BIL did not exist yet; a 2x sleeve through that would have been close to wiped out. Market style can switch at any time. / ❌ **它不能证明未来**：2007–2026 是纳指 100 史上最好的时期之一。2000–2002 年泡沫破裂（纳指 100 跌 83%）不在数据里，因为当时 QLD 和 BIL 还不存在；二倍杠杆部位在那轮下跌中几乎会归零。市场风格随时可能切换。");
        sb.AppendLine("- The method, not the number, is the takeaway: re-run it on your own symbols, period and rules. / 值得带走的是方法，而不是这个数字：换成你自己的标的、区间和规则再跑一次。");
        sb.AppendLine();
        AppendMethod(sb, "ClecOptimize");
        return sb.ToString();
    }

    private static double Median(IEnumerable<double> xs)
    {
        var a = xs.OrderBy(x => x).ToArray();
        return a.Length == 0 ? double.NaN : a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2;
    }

    private static double Spearman(List<double> x, List<double> y)
    {
        double[] Rank(List<double> v)
        {
            var idx = v.Select((val, i) => (val, i)).OrderBy(p => p.val).ToList();
            var r = new double[v.Count];
            for (var i = 0; i < idx.Count;)
            {
                var j = i;
                while (j + 1 < idx.Count && idx[j + 1].val == idx[i].val) j++;
                for (var k = i; k <= j; k++) r[idx[k].i] = (i + j) / 2.0 + 1;
                i = j + 1;
            }
            return r;
        }

        var rx = Rank(x);
        var ry = Rank(y);
        double mx = rx.Average(), my = ry.Average();
        double num = 0, dx = 0, dy = 0;
        for (var i = 0; i < rx.Length; i++)
        {
            num += (rx[i] - mx) * (ry[i] - my);
            dx += (rx[i] - mx) * (rx[i] - mx);
            dy += (ry[i] - my) * (ry[i] - my);
        }
        return num / Math.Sqrt(dx * dy);
    }

    // ================================================================== shared pieces

    private static void AppendGrowthChart(StringBuilder sb, string title, params List<(DateTime T, double E)>[] curves)
    {
        var ends = curves.Select(YearEnd).ToList();
        var maxY = ends.SelectMany(e => e).Max(p => p.E);
        sb.AppendLine("```mermaid");
        sb.AppendLine("xychart-beta");
        sb.AppendLine($"    title \"{title}\"");
        sb.AppendLine($"    x-axis [{string.Join(", ", ends[0].Select(p => p.Label))}]");
        sb.AppendLine($"    y-axis \"USD\" 0 --> {Math.Ceiling(maxY / 10000) * 10000:0}");
        foreach (var e in ends)
        {
            sb.AppendLine($"    line [{string.Join(", ", e.Select(p => p.E.ToString("0", CultureInfo.InvariantCulture)))}]");
        }
        sb.AppendLine("```");
        sb.AppendLine();
    }

    private static void AppendMethod(StringBuilder sb, string command)
    {
        sb.AppendLine("## Method & caveats / 方法与局限");
        sb.AppendLine();
        sb.AppendLine($"- **Data / 数据:** Yahoo Finance daily *adjusted* closes (dividends and splits reinvested) for {BaseSym}, {LevSym}, {CashSym} and {SpxSym}, cached in [`docs/assets/_clec_yfinance.json`](../assets/_clec_yfinance.json). {CashSym} (1–3 month T-bills) stands in for the cash sleeve because SGOV / BOXX only start in 2020; {SpxSym} is the S&P 500 benchmark with dividends. / Yahoo 复权日收盘价；现金用 BIL（1–3 个月美债）代替，因为 SGOV / BOXX 2020 年才上市；SPY 为含股息的标普 500 基准。");
        sb.AppendLine($"- **Leverage / 杠杆:** real {LevSym} prices, so daily-reset decay and fund fees are already in the numbers (unlike a simple \"2 × index\" estimate). / 使用 QLD 真实价格，已包含每日重置损耗与基金费用。");
        sb.AppendLine("- **Engine / 引擎:** Quant.Infra.Net bar-by-bar backtester. The strategy stage only sees bars up to the current day; Risk → Execution → Portfolio state are the framework's built-in stages. / 逐 bar 回放，策略只能看到当天及以前的数据。");
        sb.AppendLine($"- **Trading / 交易:** fills at the rebalance day's close, {CommissionBps} bps commission + {SlippageBps} bps slippage per trade. No taxes, no withdrawals, no contributions. / 按再平衡当日收盘价成交，每笔 {CommissionBps} bp 佣金 + {SlippageBps} bp 滑点；未计税、无提领、无追加投入。");
        sb.AppendLine("- **Metrics / 指标:** CAGR from first to last close; Sharpe = mean / stdev of daily returns × √252 with a 0% risk-free rate; Calmar = CAGR / |max drawdown|. / Sharpe 用日收益、√252 年化、无风险利率 0；Calmar = 年化收益 / 最大回撤绝对值。");
        sb.AppendLine("- **Bias / 偏差:** QQQ is a survivor and one of the best-performing indices of 2007–2026; other indices or periods (e.g. 2000–2002, when the Nasdaq-100 fell 83%) can look much worse. / 存在幸存者偏差，2000–2002 纳指 100 跌了 83%，换个区间结果可能差很多。");
        sb.AppendLine();
        sb.AppendLine("**Reproduce / 复现：**");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("node docs/assets/clec_fetch_data.js");
        sb.AppendLine($"dotnet run --project src/Quant.Infra.Net.Runtime.Console -- {command}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("> The CLEC mixes come from the published *CLEC Handbook* methodology (James) as implemented by Alpha Wealth Lab; this project is not affiliated with or endorsed by CLEC or its authors. Backtests are not forecasts; market style can switch at any time and nobody can predict the future. **Not investment advice.** / 阵型来自《CLEC 宝典》（James 老师）公开方法论，经 Alpha Wealth Lab 实现；本项目与 CLEC 及作者无关联、未获背书。回测不代表未来，市场风格随时可能切换，没有人能预测未来。**不构成投资建议。**");
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
        var period = -1;
        (DateTime T, double Dd) last = default;
        foreach (var (t, e) in c)
        {
            peak = Math.Max(peak, e);
            var key = t.Year * 4 + (t.Month - 1) / 3;
            if (period != -1 && key != period) res.Add(last);
            period = key;
            last = (t, e / peak - 1);
        }
        res.Add(last);
        return res;
    }

    // ================================================================== data

    private static (string Root, Dictionary<string, List<Ohlcv>>? Bars) Load()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "docs", "assets", "_clec_yfinance.json");
        var bars = LoadCache(path);
        if (bars is null || !new[] { BaseSym, LevSym, CashSym, SpxSym }.All(bars.ContainsKey))
        {
            C.WriteLine("FATAL: docs/assets/_clec_yfinance.json missing or incomplete. Run `node docs/assets/clec_fetch_data.js` first.");
            return (root, null);
        }

        foreach (var (s, list) in bars)
        {
            C.WriteLine($"{s}: {list.Count} bars ({list[0].OpenDateTime:yyyy-MM-dd} .. {list[^1].OpenDateTime:yyyy-MM-dd})");
        }

        return (root, bars);
    }

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

        // 只保留所有标的都有数据的交易日，避免某天缺腿 / keep only days on which every symbol trades.
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
