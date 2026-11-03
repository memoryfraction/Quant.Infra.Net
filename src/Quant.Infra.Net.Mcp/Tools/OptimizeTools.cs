using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Mcp.Engine;
using Quant.Infra.Net.Mcp.Reporting;

namespace Quant.Infra.Net.Mcp.Tools;

/// <summary>
/// MCP 工具类型：optimize_strategy（网格搜索 + 样本外留出）与 walk_forward（滚动前推再优化）。
/// 两者都是为了回答同一个问题：这个参数/策略的优势是真的，还是只是拟合了某一段行情？
/// MCP tool type: optimize_strategy (grid search + out-of-sample holdout) and walk_forward (rolling re-optimization).
/// Both answer one question: is the edge real, or was it fitted to one market regime?
/// </summary>
[McpServerToolType]
public static class OptimizeTools
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// 网格搜索 + 样本外检验 / Grid search with an out-of-sample holdout.
    /// </summary>
    [McpServerTool(Name = "optimize_strategy"), Description(
        "Grid-search strategy parameters on the in-sample part of the window and validate every combination on a held-out " +
        "out-of-sample tail (default last 30%). Returns the top combinations with BOTH in-sample and out-of-sample metrics, an " +
        "overfitting verdict, the most ROBUST combination (best worst-of-IS/OOS, not just best in-sample) and an HTML report for it. " +
        "Grid syntax: \"SlowPeriod=50,100,150,200; FastPeriod=1..41:20\" (lists or start..end:step; use | to separate values that contain commas, " +
        "e.g. \"Factors=momentum=1|momentum=1,lowvol=1\"). Accepts the same strategy/data arguments as run_backtest. Read-only, safe.")]
    public static async Task<string> OptimizeStrategy(
        [Description("Strategy name (see list_strategies).")] string strategy,
        [Description("Window start (UTC), e.g. 2012-01-01.")] string startDate,
        [Description("Window end (UTC), e.g. 2024-12-31 or 'today'.")] string endDate,
        [Description("Parameter grid, e.g. \"SlowPeriod=50,100,150,200,250\" or \"TopK=1,2,3; MomentumLookback=63,126,252\".")] string grid,
        [Description("Objective to maximize in-sample: sharpe (default) | cagr | calmar | maxdrawdown.")] string? objective = null,
        [Description("Out-of-sample fraction at the end of the window (default 0.3).")] double? testFraction = null,
        [Description("Maximum grid size (default 60; bigger grids overfit more).")] int? maxCombinations = null,
        [Description("Single-symbol strategies: symbol, e.g. SPY.")] string? symbol = null,
        [Description("PairTradingZScore: first leg.")] string? symbolA = null,
        [Description("PairTradingZScore: second leg.")] string? symbolB = null,
        [Description("MultiFactor: comma-separated universe.")] string? universe = null,
        [Description("Fixed (not optimized) strategy parameters, \"Key=Value;Key=Value\" (e.g. \"Factors=momentum=1,lowvol=0.5;RegimeSymbol=SPY\").")] string? parameters = null,
        [Description("Benchmark symbol (optional).")] string? benchmark = null,
        [Description("Data source: Demo | Yahoo | Binance | Tiingo | LocalFile | … (see list_data_sources).")] string? dataSource = null,
        [Description("Optional API key override.")] string? apiKey = null,
        [Description("LocalFile path / directory / {symbol} template.")] string? localFilePath = null,
        [Description("Commission bps (default 5).")] int? commissionBps = null,
        [Description("Slippage bps (default 2).")] int? slippageBps = null,
        [Description("Position size per symbol in % (default per strategy).")] double? positionSizePct = null)
    {
        var req = ResearchService.BuildRequest(strategy, startDate, endDate, symbol, symbolA, symbolB, universe, parameters,
            new Dictionary<string, string?>(), benchmark, dataSource, apiKey, localFilePath, null, commissionBps, slippageBps, positionSizePct, null, null);
        var combos = ResearchService.ParseGrid(grid, maxCombinations ?? 60);
        var frac = Math.Clamp(testFraction ?? 0.3, 0.1, 0.6);
        var split = req.Start.AddDays((req.End - req.Start).TotalDays * (1 - frac)).Date;
        var obj = (objective ?? "sharpe").ToLowerInvariant();

        var data = await ResearchService.LoadAsync(req, combos).ConfigureAwait(false);
        var jobs = combos.SelectMany((c, i) => new[]
        {
            ((i, true), req.ToSpec(req.Start, split, c)),
            ((i, false), req.ToSpec(split.AddDays(1), req.End, c))
        });
        var runs = await ResearchService.RunManyAsync(jobs, data).ConfigureAwait(false);
        var byKey = runs.ToDictionary(r => r.Key, r => r);

        var rows = combos.Select((c, i) =>
        {
            var isr = byKey[(i, true)];
            var oos = byKey[(i, false)];
            return new
            {
                Params = c,
                IS = isr.Result,
                OOS = oos.Result,
                Error = isr.Error ?? oos.Error,
                IsObj = isr.Result is null ? double.NaN : ResearchService.Objective(isr.Result, obj),
                OosObj = oos.Result is null ? double.NaN : ResearchService.Objective(oos.Result, obj)
            };
        }).ToList();

        var ok = rows.Where(r => r.Error is null).ToList();
        if (ok.Count == 0)
        {
            return JsonSerializer.Serialize(new { error = "every combination failed", details = rows.Select(r => new { r.Params, r.Error }).Take(5) }, RunBacktestTool.JsonOpts);
        }

        var rankedIs = ok.OrderByDescending(r => r.IsObj).ToList();
        var best = rankedIs[0];
        var robust = ok.OrderByDescending(r => Math.Min(r.IsObj, r.OosObj)).First();
        var oosRankOfBest = ok.OrderByDescending(r => r.OosObj).ToList().IndexOf(best) + 1;
        var medianIs = Median(ok.Select(r => r.IsObj));
        var medianOos = Median(ok.Select(r => r.OosObj));
        var degradation = best.IsObj > 0 ? best.OosObj / best.IsObj : double.NaN;

        var verdict = new List<string>();
        if (best.OosObj < 0) verdict.Add("The in-sample winner LOSES out-of-sample → classic overfitting / regime change. Do not trust the best-in-sample parameters.");
        else if (degradation > 1.2) verdict.Add($"Out-of-sample {obj} is HIGHER than in-sample → the test period was simply favorable; do not extrapolate it.");
        else if (degradation < 0.5) verdict.Add($"Out-of-sample {obj} is only {degradation:P0} of in-sample → heavy decay; edge is weaker than it looks.");
        else verdict.Add($"Out-of-sample {obj} keeps {degradation:P0} of in-sample → parameters generalize reasonably on this split.");
        if (oosRankOfBest > Math.Max(3, ok.Count / 3)) verdict.Add($"The in-sample #1 ranks #{oosRankOfBest}/{ok.Count} out-of-sample → rankings are unstable; prefer a parameter plateau over a peak.");
        if (ok.Count >= 6 && rankedIs.Take(3).Select(r => r.IsObj).Min() - medianIs < 0.15 * Math.Abs(medianIs) + 0.05)
            verdict.Add("Top results sit on a broad plateau (many combos score similarly) → good sign of robustness.");
        verdict.Add("Next: run walk_forward with the same grid to test across several regimes, not just one split.");

        // 报告：用"最稳健"参数在全窗口上跑一遍 / report: robust parameters over the full window
        string? reportPath = null;
        try
        {
            var fullSpec = req.ToSpec(req.Start, req.End, robust.Params);
            var full = await BacktestEngine.RunAsync(fullSpec, data).ConfigureAwait(false);
            var analysis = PerformanceAnalytics.Analyze(full, data, fullSpec.Symbols, req.Benchmark);
            var extra = GridTableHtml(rankedIs.Take(20).Select(r => (Label(r.Params), r.IsObj, r.OosObj, Metric(r.IS), Metric(r.OOS))).ToList(), obj, split);
            reportPath = HtmlReportWriter.Write($"Optimize {req.Strategy} {string.Join(" ", fullSpec.Symbols.Take(6))}",
                $"Robust params {Label(robust.Params)} · IS {req.Start:yyyy-MM-dd}→{split:yyyy-MM-dd} · OOS →{req.End:yyyy-MM-dd} · data {data.Provider}",
                fullSpec.Parameters, analysis, full, extra);
        }
        catch
        {
            // 报告失败不影响优化结果 / report failure never hides optimization results
        }

        var payload = new
        {
            strategy = req.Strategy,
            dataSource = data.Provider,
            objective = obj,
            inSample = new { start = req.Start, end = split },
            outOfSample = new { start = split.AddDays(1), end = req.End },
            combinationsTested = combos.Count,
            failed = rows.Count - ok.Count,
            bestInSample = Describe(best.Params, best.IS, best.OOS, best.IsObj, best.OosObj),
            mostRobust = Describe(robust.Params, robust.IS, robust.OOS, robust.IsObj, robust.OosObj),
            oosRankOfInSampleBest = oosRankOfBest,
            medianObjective = new { inSample = Math.Round(medianIs, 3), outOfSample = Math.Round(medianOos, 3) },
            verdict,
            top = rankedIs.Take(10).Select(r => Describe(r.Params, r.IS, r.OOS, r.IsObj, r.OosObj)).ToList(),
            report = ResearchService.ReportInfo(reportPath)
        };
        return JsonSerializer.Serialize(payload, RunBacktestTool.JsonOpts);
    }

    /// <summary>
    /// 滚动前推检验 / Walk-forward analysis.
    /// </summary>
    [McpServerTool(Name = "walk_forward"), Description(
        "Walk-forward analysis — the standard answer to 'a strategy that worked in one period may fail in another'. " +
        "Splits history into rolling windows: optimize the grid on each TRAIN window, then trade the chosen parameters on the next unseen " +
        "TEST window; repeat and stitch all test windows into one out-of-sample equity curve. Returns per-window chosen parameters and " +
        "OOS results, stitched OOS metrics vs benchmark, % of profitable windows, parameter stability and walk-forward efficiency " +
        "(OOS/IS), plus an HTML report. Without a grid it simply evaluates fixed parameters window by window (stability check). " +
        "Accepts the same strategy/data arguments as run_backtest. Read-only, safe.")]
    public static async Task<string> WalkForward(
        [Description("Strategy name (see list_strategies).")] string strategy,
        [Description("Window start (UTC), e.g. 2008-01-01. Longer is better (several regimes).")] string startDate,
        [Description("Window end (UTC), e.g. 'today'.")] string endDate,
        [Description("Optional parameter grid re-optimized on each train window, e.g. \"SlowPeriod=50,100,150,200\".")] string? grid = null,
        [Description("Train window length in months (default 36).")] int? trainMonths = null,
        [Description("Test window length in months (default 12).")] int? testMonths = null,
        [Description("Objective: sharpe (default) | cagr | calmar | maxdrawdown.")] string? objective = null,
        [Description("Maximum grid size (default 40).")] int? maxCombinations = null,
        [Description("Single-symbol strategies: symbol.")] string? symbol = null,
        [Description("PairTradingZScore: first leg.")] string? symbolA = null,
        [Description("PairTradingZScore: second leg.")] string? symbolB = null,
        [Description("MultiFactor: comma-separated universe.")] string? universe = null,
        [Description("Fixed strategy parameters, \"Key=Value;Key=Value\".")] string? parameters = null,
        [Description("Benchmark symbol (optional).")] string? benchmark = null,
        [Description("Data source: Demo | Yahoo | Binance | Tiingo | LocalFile | …")] string? dataSource = null,
        [Description("Optional API key override.")] string? apiKey = null,
        [Description("LocalFile path / directory / {symbol} template.")] string? localFilePath = null,
        [Description("Commission bps (default 5).")] int? commissionBps = null,
        [Description("Slippage bps (default 2).")] int? slippageBps = null,
        [Description("Position size per symbol in % (default per strategy).")] double? positionSizePct = null)
    {
        var req = ResearchService.BuildRequest(strategy, startDate, endDate, symbol, symbolA, symbolB, universe, parameters,
            new Dictionary<string, string?>(), benchmark, dataSource, apiKey, localFilePath, null, commissionBps, slippageBps, positionSizePct, null, null);
        var combos = string.IsNullOrWhiteSpace(grid)
            ? new List<Dictionary<string, string>> { new(StringComparer.OrdinalIgnoreCase) }
            : ResearchService.ParseGrid(grid, maxCombinations ?? 40);
        var train = Math.Max(3, trainMonths ?? 36);
        var test = Math.Max(1, testMonths ?? 12);
        var obj = (objective ?? "sharpe").ToLowerInvariant();

        var windows = new List<(DateTime TrainStart, DateTime TrainEnd, DateTime TestStart, DateTime TestEnd)>();
        for (var s = req.Start; ; s = s.AddMonths(test))
        {
            var trEnd = s.AddMonths(train).AddDays(-1);
            var teStart = trEnd.AddDays(1);
            if (teStart >= req.End) break;
            var teEnd = teStart.AddMonths(test).AddDays(-1);
            if (teEnd > req.End) teEnd = req.End;
            if ((teEnd - teStart).TotalDays < 20) break;
            windows.Add((s, trEnd, teStart, teEnd));
            if (windows.Count >= 40) break;
        }

        if (windows.Count == 0)
        {
            throw new ArgumentException($"Window too short: need at least trainMonths ({train}) + 1 month. Extend startDate/endDate or shorten trainMonths.");
        }

        var data = await ResearchService.LoadAsync(req, combos).ConfigureAwait(false);

        // 1) 每个训练窗口跑全部组合 / 1) every combination on every train window
        var trainJobs = windows.SelectMany((w, wi) => combos.Select((c, ci) => ((wi, ci), req.ToSpec(w.TrainStart, w.TrainEnd, c))));
        var trainRuns = combos.Count == 1 ? new List<((int, int) Key, BacktestResult? Result, string? Error)>() : await ResearchService.RunManyAsync(trainJobs, data).ConfigureAwait(false);
        var trainByKey = trainRuns.ToDictionary(r => r.Key, r => r);

        var chosen = windows.Select((w, wi) =>
        {
            if (combos.Count == 1) return (Index: 0, IsObj: double.NaN);
            var best = Enumerable.Range(0, combos.Count)
                .Where(ci => trainByKey[(wi, ci)].Result is not null)
                .Select(ci => (Index: ci, IsObj: ResearchService.Objective(trainByKey[(wi, ci)].Result!, obj)))
                .DefaultIfEmpty((Index: 0, IsObj: double.NaN))
                .OrderByDescending(x => x.IsObj).First();
            return best;
        }).ToList();

        // 2) 选中参数在下一段测试窗口交易 / 2) trade the chosen parameters on the next test window
        var testJobs = windows.Select((w, wi) => (wi, req.ToSpec(w.TestStart, w.TestEnd, combos[chosen[wi].Index])));
        var testRuns = (await ResearchService.RunManyAsync(testJobs, data).ConfigureAwait(false)).OrderBy(r => r.Key).ToList();

        var perWindow = windows.Select((w, wi) =>
        {
            var r = testRuns[wi].Result;
            return new
            {
                train = $"{w.TrainStart:yyyy-MM-dd} → {w.TrainEnd:yyyy-MM-dd}",
                test = $"{w.TestStart:yyyy-MM-dd} → {w.TestEnd:yyyy-MM-dd}",
                chosenParameters = combos[chosen[wi].Index],
                inSampleObjective = double.IsNaN(chosen[wi].IsObj) ? (double?)null : Math.Round(chosen[wi].IsObj, 3),
                oosReturnPct = r is null ? (double?)null : Math.Round(TotalReturn(r) * 100, 2),
                oosSharpe = r is null || !double.IsFinite(r.Metrics.SharpeRatio) ? (double?)null : Math.Round(r.Metrics.SharpeRatio, 2),
                oosMaxDrawdownPct = r is null ? (double?)null : Math.Round((double)r.Metrics.MaxDrawdown * 100, 2),
                error = testRuns[wi].Error
            };
        }).ToList();

        var okTests = testRuns.Where(t => t.Result is not null).Select(t => t.Result!).ToList();
        if (okTests.Count == 0)
        {
            return JsonSerializer.Serialize(new { error = "every test window failed", perWindow }, RunBacktestTool.JsonOpts);
        }

        var stitched = ResearchService.Stitch(okTests, req.InitialEquityUsd);
        BacktestAnalysis? analysis = null;
        try
        {
            analysis = PerformanceAnalytics.Analyze(stitched, data, req.ToSpec(req.Start, req.End).Symbols, req.Benchmark);
        }
        catch
        {
            // 窗口太短无法分析 / too short to analyze
        }

        var profitable = okTests.Count(r => TotalReturn(r) > 0);
        var distinct = chosen.Select(c => c.Index).Distinct().Count();
        var isMean = chosen.Where(c => double.IsFinite(c.IsObj)).Select(c => c.IsObj).DefaultIfEmpty(double.NaN).Average();
        var oosMean = okTests.Select(r => ResearchService.Objective(r, obj)).Average();
        var efficiency = double.IsFinite(isMean) && Math.Abs(isMean) > 1e-9 ? oosMean / isMean : double.NaN;

        var verdict = new List<string>
        {
            $"{profitable}/{okTests.Count} out-of-sample windows were profitable ({100.0 * profitable / okTests.Count:0}%)."
        };
        if (combos.Count > 1)
        {
            verdict.Add(distinct <= Math.Max(1, windows.Count / 4)
                ? $"Parameter choice is STABLE ({distinct} distinct choice(s) across {windows.Count} windows)."
                : $"Parameter choice is UNSTABLE ({distinct} distinct choices across {windows.Count} windows) → the 'best' parameters keep changing with the regime.");
            if (double.IsFinite(efficiency))
                verdict.Add(efficiency >= 0.5
                    ? $"Walk-forward efficiency {efficiency:0.00} (OOS/IS {obj}) ≥ 0.5 → optimization adds real value."
                    : $"Walk-forward efficiency {efficiency:0.00} < 0.5 → most in-sample performance does not carry forward.");
        }

        if (analysis is not null)
        {
            verdict.Add((analysis.Strategy.Sharpe ?? -9) >= (analysis.Benchmark.Sharpe ?? -9)
                ? "Stitched out-of-sample Sharpe beats buy & hold of the benchmark."
                : "Stitched out-of-sample Sharpe is BELOW buy & hold of the benchmark — a passive position would have been better.");
            verdict.Add(analysis.RegimeVerdict);
        }

        string? reportPath = null;
        if (analysis is not null)
        {
            var extra = WalkTableHtml(perWindow.Select(p => (p.train, p.test, Label(p.chosenParameters), p.oosReturnPct, p.oosSharpe, p.oosMaxDrawdownPct)).ToList(), verdict);
            reportPath = HtmlReportWriter.Write($"WalkForward {req.Strategy} {string.Join(" ", req.ToSpec(req.Start, req.End).Symbols.Take(6))}",
                $"{windows.Count} windows · train {train}m / test {test}m · objective {obj} · stitched out-of-sample · data {data.Provider}",
                req.Parameters, analysis, stitched, extra);
        }

        var payload = new
        {
            strategy = req.Strategy,
            dataSource = data.Provider,
            trainMonths = train,
            testMonths = test,
            objective = obj,
            gridSize = combos.Count,
            windows = perWindow,
            stitchedOutOfSample = analysis is null ? null : new
            {
                cagrPct = analysis.Strategy.CagrPct,
                sharpe = analysis.Strategy.Sharpe,
                maxDrawdownPct = analysis.Strategy.MaxDrawdownPct,
                benchmark = analysis.BenchmarkLabel,
                benchmarkCagrPct = analysis.Benchmark.CagrPct,
                benchmarkSharpe = analysis.Benchmark.Sharpe,
                regimes = analysis.Regimes
            },
            profitableWindowsPct = Math.Round(100.0 * profitable / okTests.Count, 1),
            distinctParameterChoices = distinct,
            walkForwardEfficiency = double.IsFinite(efficiency) ? Math.Round(efficiency, 3) : (double?)null,
            verdict,
            report = ResearchService.ReportInfo(reportPath)
        };
        return JsonSerializer.Serialize(payload, RunBacktestTool.JsonOpts);
    }

    private static double TotalReturn(BacktestResult r)
    {
        var pts = r.EquityCurve.OrderBy(kv => kv.Key).ToList();
        return pts.Count < 2 || pts[0].Value == 0 ? 0 : (double)(pts[^1].Value / pts[0].Value) - 1.0;
    }

    private static object Describe(Dictionary<string, string> p, BacktestResult? isr, BacktestResult? oos, double isObj, double oosObj) => new
    {
        parameters = p,
        inSample = Metric(isr),
        outOfSample = Metric(oos),
        objectiveInSample = Math.Round(isObj, 3),
        objectiveOutOfSample = Math.Round(oosObj, 3)
    };

    private static object? Metric(BacktestResult? r) => r is null ? null : new
    {
        cagrPct = Math.Round((double)r.Metrics.Cagr * 100, 2),
        sharpe = double.IsFinite(r.Metrics.SharpeRatio) ? Math.Round(r.Metrics.SharpeRatio, 2) : (double?)null,
        maxDrawdownPct = Math.Round((double)r.Metrics.MaxDrawdown * 100, 2),
        trades = r.Metrics.TotalTrades
    };

    private static string Label(Dictionary<string, string> p) => p.Count == 0 ? "(fixed)" : string.Join("; ", p.Select(kv => $"{kv.Key}={kv.Value}"));

    private static double Median(IEnumerable<double> xs)
    {
        var l = xs.Where(double.IsFinite).OrderBy(x => x).ToList();
        return l.Count == 0 ? double.NaN : l[l.Count / 2];
    }

    private static string GridTableHtml(List<(string Label, double IsObj, double OosObj, object? IsM, object? OosM)> rows, string obj, DateTime split)
    {
        var sb = new StringBuilder("<section><h2>Parameter grid — in-sample vs out-of-sample</h2><p class=\"note\">Sorted by in-sample ")
            .Append(WebUtility.HtmlEncode(obj)).Append(". Out-of-sample starts ").Append(split.AddDays(1).ToString("yyyy-MM-dd", Inv))
            .Append(". A good parameter keeps its rank out-of-sample.</p><table><thead><tr><th>Parameters</th><th>IS ").Append(WebUtility.HtmlEncode(obj))
            .Append("</th><th>OOS ").Append(WebUtility.HtmlEncode(obj)).Append("</th></tr></thead><tbody>");
        foreach (var r in rows)
        {
            sb.Append("<tr><td>").Append(WebUtility.HtmlEncode(r.Label)).Append("</td><td>").Append(r.IsObj.ToString("0.000", Inv))
              .Append("</td><td class=\"").Append(r.OosObj >= 0 ? "pos" : "neg").Append("\">").Append(r.OosObj.ToString("0.000", Inv)).Append("</td></tr>");
        }

        return sb.Append("</tbody></table></section>").ToString();
    }

    private static string WalkTableHtml(List<(string Train, string Test, string Params, double? Ret, double? Sharpe, double? Dd)> rows, List<string> verdict)
    {
        var sb = new StringBuilder("<section><h2>Walk-forward windows</h2><ul>");
        foreach (var v in verdict) sb.Append("<li>").Append(WebUtility.HtmlEncode(v)).Append("</li>");
        sb.Append("</ul><div class=\"scroll\"><table><thead><tr><th>Train</th><th>Test (out-of-sample)</th><th>Chosen parameters</th><th>OOS return</th><th>OOS Sharpe</th><th>OOS max DD</th></tr></thead><tbody>");
        foreach (var r in rows)
        {
            sb.Append("<tr><td>").Append(WebUtility.HtmlEncode(r.Train)).Append("</td><td>").Append(WebUtility.HtmlEncode(r.Test)).Append("</td><td>")
              .Append(WebUtility.HtmlEncode(r.Params)).Append("</td><td class=\"").Append((r.Ret ?? 0) >= 0 ? "pos" : "neg").Append("\">")
              .Append(r.Ret is null ? "—" : r.Ret.Value.ToString("0.##", Inv) + "%").Append("</td><td>")
              .Append(r.Sharpe is null ? "—" : r.Sharpe.Value.ToString("0.00", Inv)).Append("</td><td>")
              .Append(r.Dd is null ? "—" : r.Dd.Value.ToString("0.##", Inv) + "%").Append("</td></tr>");
        }

        return sb.Append("</tbody></table></div></section>").ToString();
    }
}
