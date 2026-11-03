using System.Globalization;
using System.Text.Json;
using Quant.Infra.Net.Backtest.Metrics;
using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Mcp.Reporting;
using Quant.Infra.Net.Mcp.Strategies;

namespace Quant.Infra.Net.Mcp.Engine;

/// <summary>
/// 一次研究请求的公共输入（run_backtest / optimize_strategy / walk_forward 共用）。
/// Common inputs of a research request (shared by run_backtest / optimize_strategy / walk_forward).
/// </summary>
public sealed record ResearchRequest
{
    /// <summary>策略名 / Strategy.</summary>
    public required string Strategy { get; init; }
    /// <summary>窗口起点 / Window start.</summary>
    public required DateTime Start { get; init; }
    /// <summary>窗口终点 / Window end.</summary>
    public required DateTime End { get; init; }
    /// <summary>参数表 / Parameter table.</summary>
    public required Dictionary<string, string> Parameters { get; init; }
    /// <summary>交易标的 / Traded symbols.</summary>
    public required IReadOnlyList<string> Symbols { get; init; }
    /// <summary>基准标的 / Benchmark symbol.</summary>
    public string? Benchmark { get; init; }
    /// <summary>数据源 / Data source.</summary>
    public string? DataSource { get; init; }
    /// <summary>API key / API key.</summary>
    public string? ApiKey { get; init; }
    /// <summary>本地文件 / Local file.</summary>
    public string? LocalFilePath { get; init; }
    /// <summary>初始资金 / Initial equity.</summary>
    public decimal InitialEquityUsd { get; init; } = 10_000m;
    /// <summary>手续费 / Commission bps.</summary>
    public decimal CommissionBps { get; init; } = 5m;
    /// <summary>滑点 / Slippage bps.</summary>
    public decimal SlippageBps { get; init; } = 2m;
    /// <summary>单标的仓位 / Per-symbol weight.</summary>
    public double? WeightPerSymbol { get; init; }
    /// <summary>熔断 / Kill switch.</summary>
    public double? KillSwitchDrawdown { get; init; }
    /// <summary>下一根开盘成交 / Fill at next open.</summary>
    public bool FillAtNextOpen { get; init; } = true;

    /// <summary>转成某个窗口 + 参数的 BacktestSpec / Builds a BacktestSpec for a window + parameters.</summary>
    public BacktestSpec ToSpec(DateTime start, DateTime end, IReadOnlyDictionary<string, string>? overrides = null)
    {
        var p = new Dictionary<string, string>(Parameters, StringComparer.OrdinalIgnoreCase);
        if (overrides is not null) foreach (var (k, v) in overrides) p[k] = v;
        var symbols = Strategy.Equals("MultiFactor", StringComparison.OrdinalIgnoreCase)
            ? MultiFactorRotation.AllSymbols(k => p.TryGetValue(k, out var x) ? x : null)
            : Symbols;
        return new BacktestSpec
        {
            Strategy = Strategy,
            Parameters = p,
            Symbols = symbols,
            Start = start,
            End = end,
            InitialEquityUsd = InitialEquityUsd,
            CommissionBps = CommissionBps,
            SlippageBps = SlippageBps,
            WeightPerSymbol = WeightPerSymbol,
            KillSwitchDrawdown = KillSwitchDrawdown,
            FillAtNextOpen = FillAtNextOpen
        };
    }
}

/// <summary>
/// 研究服务：解析输入 → 加载数据 → 回测 → 分析 → 报告；以及参数网格 / 滚动前推检验所需的公共函数。
/// Research service: parse inputs → load data → backtest → analyze → report; plus helpers for parameter grids and
/// walk-forward analysis.
/// </summary>
public static class ResearchService
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// 根据策略与参数构建请求（符号解析、参数合并）/ Builds a request (symbol resolution, parameter merge).
    /// </summary>
    public static ResearchRequest BuildRequest(
        string strategy, string startDate, string endDate, string? symbol, string? symbolA, string? symbolB, string? universe,
        string? parameters, Dictionary<string, string?> named, string? benchmark, string? dataSource, string? apiKey, string? localFilePath,
        int? initialEquityUsd, int? commissionBps, int? slippageBps, double? positionSizePct, double? killSwitchDrawdownPct, bool? fillAtNextOpen)
    {
        if (string.IsNullOrWhiteSpace(strategy)) throw new ArgumentException("strategy is required (see list_strategies).", nameof(strategy));
        var (start, end) = ParseWindow(startDate, endDate);

        var p = ParseParameterString(parameters);
        foreach (var (k, v) in named)
        {
            if (!string.IsNullOrWhiteSpace(v)) p[k] = v!;
        }

        if (!string.IsNullOrWhiteSpace(symbol)) p["Symbol"] = symbol.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(symbolA)) p["SymbolA"] = symbolA.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(symbolB)) p["SymbolB"] = symbolB.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(universe)) p["Universe"] = universe;
        p["Strategy"] = strategy;

        List<string> symbols;
        if (strategy.Equals("MultiFactor", StringComparison.OrdinalIgnoreCase))
        {
            symbols = MultiFactorRotation.AllSymbols(k => p.TryGetValue(k, out var x) ? x : null).ToList();
            if (symbols.Count < 2)
                throw new ArgumentException("MultiFactor needs universe with 2+ symbols, e.g. universe=\"SPY,QQQ,IWM,EFA,EEM,TLT,GLD\".");
            _ = MultiFactorRotation.Options.From(k => p.TryGetValue(k, out var x) ? x : null); // validate factor names early
        }
        else
        {
            symbols = new[] { p.GetValueOrDefault("Symbol"), p.GetValueOrDefault("SymbolA"), p.GetValueOrDefault("SymbolB") }
                .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (symbols.Count == 0)
                throw new ArgumentException("Provide at least one symbol: symbol (single-symbol strategies), symbolA+symbolB (PairTradingZScore), or universe (MultiFactor).");
            if (strategy.Equals("PairTradingZScore", StringComparison.OrdinalIgnoreCase) && symbols.Count < 2)
                throw new ArgumentException("PairTradingZScore needs symbolA and symbolB.");
        }

        if (positionSizePct is <= 0 or > 100) throw new ArgumentException("positionSizePct must be in (0, 100].");
        return new ResearchRequest
        {
            Strategy = strategy,
            Start = start,
            End = end,
            Parameters = p,
            Symbols = symbols,
            Benchmark = string.IsNullOrWhiteSpace(benchmark) ? null : benchmark.Trim().ToUpperInvariant(),
            DataSource = dataSource,
            ApiKey = apiKey,
            LocalFilePath = localFilePath,
            InitialEquityUsd = initialEquityUsd ?? 10_000,
            CommissionBps = commissionBps ?? 5,
            SlippageBps = slippageBps ?? 2,
            WeightPerSymbol = positionSizePct / 100.0,
            KillSwitchDrawdown = killSwitchDrawdownPct is null ? null : -Math.Abs(killSwitchDrawdownPct.Value) / 100.0,
            FillAtNextOpen = fillAtNextOpen ?? true
        };
    }

    /// <summary>
    /// 为请求（及可选的参数网格）加载数据：自动预取预热历史（至少 200 根，供市场状态分析）。
    /// Loads data for a request (and an optional grid), pre-fetching warm-up history (≥ 200 bars for regime analysis).
    /// </summary>
    public static Task<MarketData> LoadAsync(ResearchRequest req, IReadOnlyList<Dictionary<string, string>>? grid = null)
    {
        var need = 200;
        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var combo in grid is { Count: > 0 } ? grid : new List<Dictionary<string, string>> { new() })
        {
            var spec = req.ToSpec(req.Start, req.End, combo);
            need = Math.Max(need, BacktestEngine.RequiredBars(req.Strategy, spec.Parameters));
            foreach (var s in spec.Symbols) symbols.Add(s);
        }

        if (req.Benchmark is not null) symbols.Add(req.Benchmark);
        var isDemo = DataSources.McpSourceDataFactory.Parse(req.DataSource) is null;
        var fetchStart = isDemo ? req.Start : req.Start.AddDays(-BacktestEngine.WarmupCalendarDays(need));
        return BacktestEngine.LoadAsync(req.DataSource, req.ApiKey, req.LocalFilePath, symbols.ToList(), fetchStart, req.End);
    }

    /// <summary>按 "Key=Value;Key=Value" 或 JSON 对象解析参数 / Parses "Key=Value;Key=Value" or a JSON object.</summary>
    public static Dictionary<string, string> ParseParameterString(string? raw)
    {
        var p = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return p;
        raw = raw.Trim();
        if (raw.StartsWith('{'))
        {
            using var doc = JsonDocument.Parse(raw);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                p[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString()! : prop.Value.GetRawText();
            }

            return p;
        }

        foreach (var part in raw.Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length == 2 && kv[0].Length > 0) p[kv[0]] = kv[1];
        }

        return p;
    }

    /// <summary>
    /// 解析参数网格："SlowPeriod=50,100,200; FastPeriod=1..21:10"（值列表或 起..止:步长）。
    /// Parses a parameter grid: "SlowPeriod=50,100,200; FastPeriod=1..21:10" (value lists or start..end:step).
    /// </summary>
    public static List<Dictionary<string, string>> ParseGrid(string grid, int maxCombinations)
    {
        if (string.IsNullOrWhiteSpace(grid)) throw new ArgumentException("grid is required, e.g. \"SlowPeriod=50,100,150,200\".", nameof(grid));
        var axes = new List<(string Key, List<string> Values)>();
        foreach (var part in grid.Split(new[] { ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2, StringSplitOptions.TrimEntries);
            if (kv.Length != 2) throw new ArgumentException($"Bad grid axis '{part}'. Use Key=v1,v2,v3 or Key=start..end:step.");
            var values = new List<string>();
            var range = System.Text.RegularExpressions.Regex.Match(kv[1], @"^\s*(-?[\d.]+)\s*\.\.\s*(-?[\d.]+)\s*(?::\s*([\d.]+))?\s*$");
            if (range.Success)
            {
                var a = double.Parse(range.Groups[1].Value, Inv);
                var b = double.Parse(range.Groups[2].Value, Inv);
                var step = range.Groups[3].Success ? double.Parse(range.Groups[3].Value, Inv) : 1;
                if (step <= 0) throw new ArgumentException("grid step must be > 0.");
                for (var v = a; v <= b + 1e-9 && values.Count < 1000; v += step) values.Add(Math.Round(v, 6).ToString(Inv));
            }
            else
            {
                // 用 | 分隔时允许值内含逗号（如 Factors=momentum=1,lowvol=1|momentum=1）/ '|' lets values contain commas
                var sep = kv[1].Contains('|') ? new[] { '|' } : new[] { ',' };
                values.AddRange(kv[1].Split(sep, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }

            if (values.Count == 0) throw new ArgumentException($"Grid axis '{kv[0]}' has no values.");
            axes.Add((kv[0], values));
        }

        var combos = new List<Dictionary<string, string>> { new(StringComparer.OrdinalIgnoreCase) };
        foreach (var (key, values) in axes)
        {
            combos = combos.SelectMany(c => values.Select(v => new Dictionary<string, string>(c, StringComparer.OrdinalIgnoreCase) { [key] = v })).ToList();
            if (combos.Count > maxCombinations)
                throw new ArgumentException($"Grid expands to more than {maxCombinations} combinations — narrow it (fewer values per axis) or raise maxCombinations. Large grids also overfit.");
        }

        return combos;
    }

    /// <summary>
    /// 并行跑多次回测（数据共享，每次独立 DI 容器）/ Runs many backtests in parallel (shared data, isolated DI containers).
    /// </summary>
    public static async Task<List<(T Key, BacktestResult? Result, string? Error)>> RunManyAsync<T>(
        IEnumerable<(T Key, BacktestSpec Spec)> jobs, MarketData data, CancellationToken ct = default)
    {
        using var gate = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount - 1));
        var tasks = jobs.Select(async job =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var r = await Task.Run(() => BacktestEngine.RunAsync(job.Spec, data, ct), ct).ConfigureAwait(false);
                return (job.Key, (BacktestResult?)r, (string?)null);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return (job.Key, (BacktestResult?)null, (string?)ex.Message);
            }
            finally
            {
                gate.Release();
            }
        }).ToList();
        return (await Task.WhenAll(tasks).ConfigureAwait(false)).ToList();
    }

    /// <summary>
    /// 目标函数取值（越大越好）/ Objective value (higher is better).
    /// </summary>
    public static double Objective(BacktestResult r, string objective)
    {
        var m = r.Metrics;
        double v = (objective ?? "sharpe").ToLowerInvariant() switch
        {
            "cagr" or "return" => (double)m.Cagr,
            "calmar" => m.CalmarRatio,
            "maxdrawdown" or "drawdown" => (double)m.MaxDrawdown,
            _ => m.SharpeRatio
        };
        return double.IsFinite(v) ? v : -999;
    }

    /// <summary>把若干段回测拼成一条连续权益曲线（按收益率链接）/ Chains several backtests into one continuous equity curve.</summary>
    public static BacktestResult Stitch(IReadOnlyList<BacktestResult> parts, decimal initialEquity)
    {
        var curve = new SortedDictionary<DateTime, decimal>();
        var trades = new List<BacktestTrade>();
        var level = initialEquity;
        foreach (var part in parts)
        {
            var pts = part.EquityCurve.OrderBy(kv => kv.Key).ToList();
            if (pts.Count == 0) continue;
            var first = pts[0].Value;
            foreach (var (t, v) in pts)
            {
                if (curve.ContainsKey(t)) continue;
                curve[t] = first > 0 ? level * v / first : level;
            }

            level = curve[pts[^1].Key];
            trades.AddRange(part.Trades);
        }

        var dict = curve.ToDictionary(kv => kv.Key, kv => kv.Value);
        return new BacktestResult { EquityCurve = dict, Trades = trades, Metrics = BacktestMetricsFactory.Assemble(dict, trades) };
    }

    /// <summary>
    /// 把权益曲线按月采样（≤ 120 点），便于客户端模型自己画图 / Samples the equity curve monthly (≤ 120 points) for client-side charts.
    /// </summary>
    public static object SampleCurve(BacktestAnalysis a)
    {
        var bench = a.BenchmarkEquity.ToDictionary(p => p.T, p => p.V);
        var monthly = a.Equity.GroupBy(p => (p.T.Year, p.T.Month)).Select(g => g.Last()).ToList();
        var step = Math.Max(1, (int)Math.Ceiling(monthly.Count / 120.0));
        return monthly.Where((_, i) => i % step == 0 || i == monthly.Count - 1)
            .Select(p => new { date = p.T.ToString("yyyy-MM-dd", Inv), strategy = Math.Round(p.V, 2), benchmark = Math.Round(bench.GetValueOrDefault(p.T), 2) })
            .ToList();
    }

    /// <summary>报告结果的 JSON 片段 / JSON fragment describing the report.</summary>
    public static object ReportInfo(string? path)
        => path is null ? new { written = false } : new { written = true, path, uri = new Uri(path).AbsoluteUri, hint = "Open this file in a browser to see charts (equity vs benchmark, drawdown, rolling Sharpe, regimes, monthly heatmap, fills)." };

    /// <summary>解析窗口 / Parses the window.</summary>
    public static (DateTime Start, DateTime End) ParseWindow(string startRaw, string endRaw)
    {
        var start = ParseDate(startRaw, "startDate");
        var end = string.IsNullOrWhiteSpace(endRaw) || endRaw.Trim().Equals("today", StringComparison.OrdinalIgnoreCase)
            ? DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc)
            : ParseDate(endRaw, "endDate");
        if (end < start) throw new ArgumentException($"endDate ({end:u}) must be >= startDate ({start:u}).");
        return (start, end);
    }

    private static DateTime ParseDate(string raw, string field)
    {
        if (!DateTime.TryParse(raw, Inv, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            throw new ArgumentException($"{field} '{raw}' is not a valid date (expected ISO 8601 or yyyy-MM-dd).");
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }
}
