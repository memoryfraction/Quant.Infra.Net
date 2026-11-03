using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Mcp.Engine;

namespace Quant.Infra.Net.Mcp.Reporting;

/// <summary>时间序列点 / A time-series point.</summary>
public readonly record struct Point(DateTime T, double V);

/// <summary>一组核心绩效统计 / A set of core performance statistics.</summary>
public sealed record Stats(
    double TotalReturnPct, double CagrPct, double AnnVolPct, double? Sharpe, double? Sortino,
    double MaxDrawdownPct, double? Calmar, double BestDayPct, double WorstDayPct, double PositiveMonthsPct);

/// <summary>某个市场状态下的表现 / Performance within one market regime.</summary>
public sealed record RegimeRow(string Regime, string Description, double ShareOfDaysPct, double StrategyAnnReturnPct,
    double? StrategySharpe, double BenchmarkAnnReturnPct, double? BenchmarkSharpe);

/// <summary>年度收益 / Calendar-year return.</summary>
public sealed record YearRow(int Year, double StrategyPct, double BenchmarkPct);

/// <summary>
/// 完整分析结果（报告与 JSON 共享）/ Full analysis (shared by the HTML report and the JSON payload).
/// </summary>
public sealed class BacktestAnalysis
{
    /// <summary>策略统计 / Strategy stats.</summary>
    public required Stats Strategy { get; init; }
    /// <summary>基准统计 / Benchmark stats.</summary>
    public required Stats Benchmark { get; init; }
    /// <summary>基准说明 / Benchmark label.</summary>
    public required string BenchmarkLabel { get; init; }
    /// <summary>Beta / Beta vs benchmark.</summary>
    public double? Beta { get; init; }
    /// <summary>年化 Alpha（%）/ Annualized alpha (%).</summary>
    public double? AlphaPct { get; init; }
    /// <summary>相关系数 / Correlation.</summary>
    public double? Correlation { get; init; }
    /// <summary>平均仓位暴露（%）/ Average exposure (%).</summary>
    public double ExposurePct { get; init; }
    /// <summary>年化换手（倍）/ Annual turnover (x).</summary>
    public double AnnualTurnover { get; init; }
    /// <summary>策略权益 / Strategy equity.</summary>
    public required IReadOnlyList<Point> Equity { get; init; }
    /// <summary>基准权益（同起点）/ Benchmark equity (same start).</summary>
    public required IReadOnlyList<Point> BenchmarkEquity { get; init; }
    /// <summary>回撤（%）/ Drawdown (%).</summary>
    public required IReadOnlyList<Point> Drawdown { get; init; }
    /// <summary>滚动 6 个月夏普 / Rolling 6-month Sharpe.</summary>
    public required IReadOnlyList<Point> RollingSharpe { get; init; }
    /// <summary>月度收益（年→月[1..12]→%）/ Monthly returns.</summary>
    public required SortedDictionary<int, double?[]> Monthly { get; init; }
    /// <summary>年度收益 / Yearly returns.</summary>
    public required IReadOnlyList<YearRow> Yearly { get; init; }
    /// <summary>市场状态拆解 / Regime breakdown.</summary>
    public required IReadOnlyList<RegimeRow> Regimes { get; init; }
    /// <summary>自动生成的诊断与警告 / Auto-generated diagnostics and warnings.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }
    /// <summary>一句话风格判断 / One-line regime verdict.</summary>
    public required string RegimeVerdict { get; init; }
}

/// <summary>
/// 绩效分析：基准对比、月/年度收益、滚动夏普、市场状态（牛/熊 × 平静/动荡）拆解、诊断警告。
/// Performance analytics: benchmark comparison, monthly/yearly returns, rolling Sharpe, regime breakdown
/// (bull/bear × calm/volatile), diagnostics.
/// </summary>
public static class PerformanceAnalytics
{
    /// <summary>
    /// 分析一次回测 / Analyzes one backtest.
    /// </summary>
    /// <param name="result">回测结果 / Backtest result.</param>
    /// <param name="data">已加载数据（含预热段，用于基准均线）/ Loaded data (incl. warm-up, for the benchmark SMA).</param>
    /// <param name="symbols">交易标的 / Traded symbols.</param>
    /// <param name="benchmarkSymbol">基准标的（null = 单标的买入持有 / 多标的等权买入持有）/ Benchmark symbol.</param>
    public static BacktestAnalysis Analyze(BacktestResult result, MarketData data, IReadOnlyList<string> symbols, string? benchmarkSymbol)
    {
        var equity = result.EquityCurve.OrderBy(kv => kv.Key).Select(kv => new Point(kv.Key, (double)kv.Value)).ToList();
        if (equity.Count < 2)
        {
            throw new InvalidOperationException("Backtest produced fewer than 2 equity points — window too short for analysis.");
        }

        var periodsPerYear = PeriodsPerYear(equity);
        var (benchRaw, benchLabel) = BuildBenchmarkPrices(data, symbols, benchmarkSymbol);
        var benchAt = ForwardFill(benchRaw, equity.Select(p => p.T).ToList());
        var e0 = equity[0].V;
        var b0 = benchAt.First(v => v > 0);
        var benchEquity = equity.Select((p, i) => new Point(p.T, benchAt[i] > 0 ? e0 * benchAt[i] / b0 : e0)).ToList();

        var sr = Returns(equity);
        var br = Returns(benchEquity);
        var sStats = ComputeStats(equity, sr, periodsPerYear);
        var bStats = ComputeStats(benchEquity, br, periodsPerYear);

        double? beta = null, alpha = null, corr = null;
        if (sr.Count > 10)
        {
            var mb = br.Average();
            var ms = sr.Average();
            var cov = sr.Zip(br, (a, b) => (a - ms) * (b - mb)).Sum() / (sr.Count - 1);
            var vb = br.Sum(b => (b - mb) * (b - mb)) / (br.Count - 1);
            var vs = sr.Sum(a => (a - ms) * (a - ms)) / (sr.Count - 1);
            if (vb > 0)
            {
                beta = cov / vb;
                alpha = (ms - beta.Value * mb) * periodsPerYear * 100.0;
            }

            if (vb > 0 && vs > 0) corr = cov / Math.Sqrt(vb * vs);
        }

        // 暴露度与换手 / exposure and turnover
        var years = Math.Max(1e-9, (equity[^1].T - equity[0].T).TotalDays / 365.25);
        var avgEq = equity.Average(p => p.V);
        var notional = result.Trades.Sum(t => (double)Math.Abs(t.NotionalUsd));
        var turnover = avgEq > 0 ? notional / avgEq / years : 0;
        var exposure = EstimateExposure(result, equity);

        var drawdown = Drawdowns(equity);
        var rolling = RollingSharpe(equity, sr, Math.Max(20, (int)Math.Round(periodsPerYear / 2)), periodsPerYear);
        var monthly = MonthlyReturns(equity);
        var yearly = YearlyRows(equity, benchEquity);
        var regimes = Regimes(equity, sr, benchEquity, br, benchRaw, periodsPerYear);
        var warnings = Diagnose(result, sStats, bStats, years, regimes, data);

        return new BacktestAnalysis
        {
            Strategy = sStats,
            Benchmark = bStats,
            BenchmarkLabel = benchLabel,
            Beta = Round(beta, 3),
            AlphaPct = Round(alpha, 2),
            Correlation = Round(corr, 3),
            ExposurePct = Math.Round(exposure, 1),
            AnnualTurnover = Math.Round(turnover, 2),
            Equity = equity,
            BenchmarkEquity = benchEquity,
            Drawdown = drawdown,
            RollingSharpe = rolling,
            Monthly = monthly,
            Yearly = yearly,
            Regimes = regimes,
            Warnings = warnings,
            RegimeVerdict = RegimeVerdictText(regimes)
        };
    }

    /// <summary>根据权益点密度估计每年 bar 数（股票≈252，加密≈365）/ Estimates bars per year (stocks≈252, crypto≈365).</summary>
    public static double PeriodsPerYear(IReadOnlyList<Point> eq)
    {
        var days = (eq[^1].T - eq[0].T).TotalDays;
        if (days < 20) return 252;
        var ppy = (eq.Count - 1) / (days / 365.25);
        return ppy > 300 ? 365 : ppy < 60 ? ppy : 252;
    }

    /// <summary>计算统计 / Computes stats.</summary>
    public static Stats ComputeStats(IReadOnlyList<Point> eq, IReadOnlyList<double> r, double ppy)
    {
        var total = eq[^1].V / eq[0].V - 1.0;
        var years = Math.Max(1e-9, (eq[^1].T - eq[0].T).TotalDays / 365.25);
        var cagr = eq[^1].V > 0 ? Math.Pow(eq[^1].V / eq[0].V, 1.0 / years) - 1.0 : -1.0;
        var mean = r.Count > 0 ? r.Average() : 0;
        var sd = r.Count > 1 ? Math.Sqrt(r.Sum(x => (x - mean) * (x - mean)) / (r.Count - 1)) : 0;
        var downside = r.Count > 1 ? Math.Sqrt(r.Sum(x => x < 0 ? x * x : 0) / (r.Count - 1)) : 0;
        var mdd = Drawdowns(eq).Select(p => p.V).DefaultIfEmpty(0).Min();
        var months = MonthlyReturns(eq).Values.SelectMany(a => a).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return new Stats(
            Math.Round(total * 100, 2),
            Math.Round(cagr * 100, 2),
            Math.Round(sd * Math.Sqrt(ppy) * 100, 2),
            sd > 1e-12 ? Math.Round(mean / sd * Math.Sqrt(ppy), 2) : null,
            downside > 1e-12 ? Math.Round(mean / downside * Math.Sqrt(ppy), 2) : null,
            Math.Round(mdd, 2),
            mdd < -1e-9 ? Math.Round(cagr * 100 / Math.Abs(mdd), 2) : null,
            r.Count > 0 ? Math.Round(r.Max() * 100, 2) : 0,
            r.Count > 0 ? Math.Round(r.Min() * 100, 2) : 0,
            months.Count > 0 ? Math.Round(100.0 * months.Count(m => m > 0) / months.Count, 1) : 0);
    }

    private static (List<Point> Prices, string Label) BuildBenchmarkPrices(MarketData data, IReadOnlyList<string> symbols, string? benchmark)
    {
        if (!string.IsNullOrWhiteSpace(benchmark) && data.Bars.TryGetValue(benchmark, out var bb) && bb.Count > 1)
        {
            return (bb.Select(b => new Point(b.OpenDateTime, (double)b.Close)).ToList(), $"{benchmark.ToUpperInvariant()} buy & hold");
        }

        var tradables = symbols.Where(s => data.Bars.ContainsKey(s) && data.Bars[s].Count > 1).ToList();
        if (tradables.Count == 1)
        {
            return (data.Bars[tradables[0]].Select(b => new Point(b.OpenDateTime, (double)b.Close)).ToList(), $"{tradables[0]} buy & hold");
        }

        // 等权（每日再平衡）指数 / equal-weight index, daily rebalanced
        var dates = tradables.SelectMany(s => data.Bars[s].Select(b => b.OpenDateTime)).Distinct().OrderBy(d => d).ToList();
        var maps = tradables.ToDictionary(s => s, s => data.Bars[s].ToDictionary(b => b.OpenDateTime, b => (double)b.Close));
        var last = tradables.ToDictionary(s => s, _ => double.NaN);
        var index = new List<Point>();
        var level = 100.0;
        foreach (var d in dates)
        {
            var rets = new List<double>();
            foreach (var s in tradables)
            {
                if (maps[s].TryGetValue(d, out var c))
                {
                    if (!double.IsNaN(last[s]) && last[s] > 0) rets.Add(c / last[s] - 1.0);
                    last[s] = c;
                }
            }

            if (rets.Count > 0) level *= 1.0 + rets.Average();
            index.Add(new Point(d, level));
        }

        return (index, $"equal-weight buy & hold of {string.Join("/", tradables)}");
    }

    private static List<double> ForwardFill(List<Point> series, IReadOnlyList<DateTime> dates)
    {
        var outp = new List<double>(dates.Count);
        var j = 0;
        var last = 0.0;
        foreach (var d in dates)
        {
            while (j < series.Count && series[j].T <= d)
            {
                last = series[j].V;
                j++;
            }

            outp.Add(last);
        }

        return outp;
    }

    private static List<double> Returns(IReadOnlyList<Point> eq)
    {
        var r = new List<double>(eq.Count);
        for (var i = 1; i < eq.Count; i++)
        {
            r.Add(eq[i - 1].V > 0 ? eq[i].V / eq[i - 1].V - 1.0 : 0.0);
        }

        return r;
    }

    private static List<Point> Drawdowns(IReadOnlyList<Point> eq)
    {
        var peak = double.MinValue;
        var dd = new List<Point>(eq.Count);
        foreach (var p in eq)
        {
            peak = Math.Max(peak, p.V);
            dd.Add(new Point(p.T, peak > 0 ? (p.V / peak - 1.0) * 100.0 : 0));
        }

        return dd;
    }

    private static List<Point> RollingSharpe(IReadOnlyList<Point> eq, IReadOnlyList<double> r, int window, double ppy)
    {
        var outp = new List<Point>();
        for (var i = window; i <= r.Count; i++)
        {
            var seg = r.Skip(i - window).Take(window).ToList();
            var m = seg.Average();
            var sd = Math.Sqrt(seg.Sum(x => (x - m) * (x - m)) / (seg.Count - 1));
            outp.Add(new Point(eq[i].T, sd > 1e-12 ? m / sd * Math.Sqrt(ppy) : 0));
        }

        return outp;
    }

    private static SortedDictionary<int, double?[]> MonthlyReturns(IReadOnlyList<Point> eq)
    {
        var res = new SortedDictionary<int, double?[]>();
        var groups = eq.GroupBy(p => (p.T.Year, p.T.Month)).OrderBy(g => g.Key).ToList();
        var prevClose = eq[0].V;
        foreach (var g in groups)
        {
            var close = g.Last().V;
            if (!res.TryGetValue(g.Key.Year, out var arr))
            {
                arr = new double?[12];
                res[g.Key.Year] = arr;
            }

            arr[g.Key.Month - 1] = prevClose > 0 ? Math.Round((close / prevClose - 1.0) * 100, 2) : null;
            prevClose = close;
        }

        return res;
    }

    private static List<YearRow> YearlyRows(IReadOnlyList<Point> s, IReadOnlyList<Point> b)
    {
        var rows = new List<YearRow>();
        var years = s.Select(p => p.T.Year).Distinct().OrderBy(y => y).ToList();
        double sPrev = s[0].V, bPrev = b[0].V;
        foreach (var y in years)
        {
            var sLast = s.Last(p => p.T.Year == y).V;
            var bLast = b.Last(p => p.T.Year == y).V;
            rows.Add(new YearRow(y, Math.Round((sLast / sPrev - 1) * 100, 2), Math.Round((bLast / bPrev - 1) * 100, 2)));
            sPrev = sLast;
            bPrev = bLast;
        }

        return rows;
    }

    /// <summary>
    /// 市场状态拆解：趋势（基准 vs 200 日均线）× 波动（20 日实现波动率 vs 区间中位数）。
    /// Regime breakdown: trend (benchmark vs its 200-bar SMA) × volatility (20-bar realized vol vs window median).
    /// </summary>
    private static List<RegimeRow> Regimes(IReadOnlyList<Point> eq, IReadOnlyList<double> sr, IReadOnlyList<Point> benchEq,
        IReadOnlyList<double> br, List<Point> benchRaw, double ppy)
    {
        const int smaN = 200, volN = 20;
        var rawIdx = benchRaw.Select((p, i) => (p.T, i)).ToDictionary(x => x.T, x => x.i);
        var labels = new string?[sr.Count];
        var vols = new double[sr.Count];
        for (var k = 0; k < sr.Count; k++)
        {
            var t = eq[k + 1].T;
            var vStart = Math.Max(0, k - volN + 1);
            var seg = br.Skip(vStart).Take(k - vStart + 1).ToList();
            var m = seg.Average();
            vols[k] = seg.Count > 1 ? Math.Sqrt(seg.Sum(x => (x - m) * (x - m)) / (seg.Count - 1)) : 0;

            // 用原始基准序列（含预热段）求 SMA，否则用基准权益 / SMA from raw benchmark incl. warm-up, else benchmark equity
            bool? bull = null;
            if (rawIdx.TryGetValue(t, out var ri) && ri >= smaN - 1)
            {
                var sma = 0.0;
                for (var q = ri - smaN + 1; q <= ri; q++) sma += benchRaw[q].V;
                bull = benchRaw[ri].V >= sma / smaN;
            }
            else if (k + 1 >= smaN)
            {
                var sma = benchEq.Skip(k + 2 - smaN).Take(smaN).Average(p => p.V);
                bull = benchEq[k + 1].V >= sma;
            }

            labels[k] = bull is null ? null : bull.Value ? "Bull" : "Bear";
        }

        var median = vols.Where((_, k) => labels[k] != null).OrderBy(v => v).ToList();
        var med = median.Count > 0 ? median[median.Count / 2] : 0;
        var buckets = new Dictionary<string, (List<double> S, List<double> B)>();
        var counted = 0;
        for (var k = 0; k < sr.Count; k++)
        {
            if (labels[k] is null) continue;
            var key = labels[k] + (vols[k] > med ? "-Volatile" : "-Calm");
            if (!buckets.TryGetValue(key, out var bk))
            {
                bk = (new List<double>(), new List<double>());
                buckets[key] = bk;
            }

            bk.S.Add(sr[k]);
            bk.B.Add(br[k]);
            counted++;
        }

        var desc = new Dictionary<string, string>
        {
            ["Bull-Calm"] = "uptrend, low volatility / 慢牛",
            ["Bull-Volatile"] = "uptrend, high volatility / 震荡上行",
            ["Bear-Calm"] = "downtrend, low volatility / 阴跌",
            ["Bear-Volatile"] = "downtrend, high volatility / 恐慌下跌"
        };

        // 少于 20 天的状态年化后毫无意义（会出现 -117%/年这种噪声），直接不展示。
        // Regimes with < 20 days annualize into noise (e.g. -117%/yr), so they are not shown.
        return desc.Keys.Where(k => buckets.ContainsKey(k) && buckets[k].S.Count >= 20).Select(k =>
        {
            var (s, b) = buckets[k];
            return new RegimeRow(k, desc[k], Math.Round(100.0 * s.Count / Math.Max(1, counted), 1),
                Math.Round(s.Average() * ppy * 100, 2), SharpeOf(s, ppy),
                Math.Round(b.Average() * ppy * 100, 2), SharpeOf(b, ppy));
        }).ToList();
    }

    private static double? SharpeOf(List<double> r, double ppy)
    {
        if (r.Count < 5) return null;
        var m = r.Average();
        var sd = Math.Sqrt(r.Sum(x => (x - m) * (x - m)) / (r.Count - 1));
        return sd > 1e-12 ? Math.Round(m / sd * Math.Sqrt(ppy), 2) : null;
    }

    private static string RegimeVerdictText(IReadOnlyList<RegimeRow> rows)
    {
        var usable = rows.Where(r => r.ShareOfDaysPct >= 5).ToList();
        if (usable.Count < 2) return "Not enough history to judge regime dependence (need 200+ bars of benchmark history).";
        var best = usable.OrderByDescending(r => r.StrategyAnnReturnPct).First();
        var worst = usable.OrderBy(r => r.StrategyAnnReturnPct).First();
        var spread = best.StrategyAnnReturnPct - worst.StrategyAnnReturnPct;
        var dependent = spread > 25 || (worst.StrategyAnnReturnPct < 0 && best.StrategyAnnReturnPct > 0);
        return $"Best in {best.Regime} ({best.StrategyAnnReturnPct:0.#}%/yr), worst in {worst.Regime} ({worst.StrategyAnnReturnPct:0.#}%/yr). " +
               (dependent
                   ? "Strongly regime-dependent: consider a regime filter (MultiFactor RegimeSymbol) or pairing it with a strategy that wins in the weak regime."
                   : "Fairly regime-robust in this sample.");
    }

    private static double EstimateExposure(BacktestResult result, IReadOnlyList<Point> eq)
    {
        if (result.Trades.Count == 0) return 0;
        // 由成交重建持仓名义（近似）/ rebuild approximate position notional from fills
        var pos = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var trades = result.Trades.OrderBy(t => t.TimestampUtc).ToList();
        var j = 0;
        var invested = 0;
        foreach (var p in eq)
        {
            while (j < trades.Count && trades[j].TimestampUtc <= p.T)
            {
                var t = trades[j];
                var signed = (double)t.NotionalUsd * (t.Side.ToString().Contains("Short", StringComparison.OrdinalIgnoreCase) || t.Side.ToString().Contains("Sell", StringComparison.OrdinalIgnoreCase) ? -1 : 1);
                pos[t.Symbol] = pos.GetValueOrDefault(t.Symbol) + signed;
                j++;
            }

            if (pos.Values.Any(v => Math.Abs(v) > 1e-6)) invested++;
        }

        return 100.0 * invested / eq.Count;
    }

    private static List<string> Diagnose(BacktestResult r, Stats s, Stats b, double years, IReadOnlyList<RegimeRow> regimes, MarketData data)
    {
        var w = new List<string>();
        if (data.IsDemo) w.Add("Demo data is synthetic — use dataSource=Yahoo (stocks/ETFs) or Binance (crypto) for real conclusions.");
        if (r.Trades.Count < 20) w.Add($"Only {r.Trades.Count} fills — too few for statistical confidence; lengthen the window or widen the universe.");
        if (years < 3) w.Add($"Window is {years:0.0} years — shorter than a full market cycle; test 5-10+ years including 2008/2020/2022.");
        if (s.Sharpe is double sh && b.Sharpe is double bs && sh < bs) w.Add("Risk-adjusted return is below simply holding the benchmark.");
        if (s.MaxDrawdownPct < -35) w.Add($"Max drawdown {s.MaxDrawdownPct:0.#}% — most people abandon a strategy before recovering from this.");
        if (regimes.Count(x => x.StrategyAnnReturnPct < 0 && x.ShareOfDaysPct >= 10) >= 2) w.Add("Loses money in 2+ market regimes — edge may be fragile; run walk_forward to check stability.");
        if (!data.IsDemo && data.Provider is "Yahoo" or "Tiingo") w.Add("Survivorship bias: today's tickers exclude delisted companies; prefer ETFs/indices for long tests.");
        return w;
    }

    private static double? Round(double? v, int digits) => v is null || double.IsNaN(v.Value) || double.IsInfinity(v.Value) ? null : Math.Round(v.Value, digits);
}
