using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.Mcp.DataSources;
using Quant.Infra.Net.Mcp.Strategies;
using Quant.Infra.Net.Runtime;
using Quant.Infra.Net.Runtime.DataSources;
using Quant.Infra.Net.Runtime.Models;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Mcp.Engine;

/// <summary>
/// 已加载的行情（按符号）——一次下载，供多次回测（调参 / 滚动检验）复用。
/// Loaded market data by symbol — downloaded once, reused across many backtests (optimization / walk-forward).
/// </summary>
public sealed class MarketData
{
    /// <summary>每个符号的日线（升序）/ Daily bars per symbol (ascending).</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<Ohlcv>> Bars { get; init; }

    /// <summary>数据源名 / Provider label.</summary>
    public required string Provider { get; init; }

    /// <summary>是否离线合成数据 / Whether this is offline synthetic data.</summary>
    public bool IsDemo { get; init; }
}

/// <summary>
/// 一次回测的完整设定 / Full specification of one backtest.
/// </summary>
public sealed record BacktestSpec
{
    /// <summary>策略名 / Strategy name.</summary>
    public required string Strategy { get; init; }
    /// <summary>策略参数 / Strategy parameters.</summary>
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }
    /// <summary>交易标的 / Traded symbols.</summary>
    public required IReadOnlyList<string> Symbols { get; init; }
    /// <summary>评估起点 / Evaluation start.</summary>
    public required DateTime Start { get; init; }
    /// <summary>评估终点 / Evaluation end.</summary>
    public required DateTime End { get; init; }
    /// <summary>初始资金 / Initial equity.</summary>
    public decimal InitialEquityUsd { get; init; } = 10_000m;
    /// <summary>手续费 bps / Commission bps.</summary>
    public decimal CommissionBps { get; init; } = 5m;
    /// <summary>滑点 bps / Slippage bps.</summary>
    public decimal SlippageBps { get; init; } = 2m;
    /// <summary>单标的仓位（0-1），null = 按策略默认 / Per-symbol weight (0-1); null = strategy default.</summary>
    public double? WeightPerSymbol { get; init; }
    /// <summary>回撤熔断（负数，如 -0.2）；null = 关闭 / Kill-switch drawdown (negative); null = off.</summary>
    public double? KillSwitchDrawdown { get; init; }
    /// <summary>下一根开盘成交（默认 true，防止"用收盘价信号又按收盘价成交"）/ Fill at next bar open (default true).</summary>
    public bool FillAtNextOpen { get; init; } = true;
}

/// <summary>
/// MCP 层回测引擎：加载数据（含自动预热）→ 组装统一运行时 → 跑 BacktestRunner。只消费公共 API，不改受保护项目。
/// MCP-layer backtest engine: load data (with automatic warm-up) → assemble the unified runtime → run BacktestRunner.
/// Consumes public APIs only; never edits the protected projects.
/// </summary>
public static class BacktestEngine
{
    /// <summary>
    /// 策略需要的历史 bar 数（用于自动预取预热数据）/ History bars a strategy needs (drives automatic warm-up fetch).
    /// </summary>
    public static int RequiredBars(string strategy, IReadOnlyDictionary<string, string> p)
    {
        string? Get(string k) => p.TryGetValue(k, out var v) ? v : null;
        int I(string k, int d) => int.TryParse(Get(k), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : d;
        return strategy.ToLowerInvariant() switch
        {
            "macross" => Math.Max(I("SlowPeriod", 200), I("FastPeriod", 1)) + 1,
            "meanreversion" or "pairtradingzscore" => I("LookbackBars", 100) + 1,
            "multifactor" => MultiFactorRotation.RequiredBars(Get),
            _ => p.Where(kv => kv.Key.Contains("Period", StringComparison.OrdinalIgnoreCase) || kv.Key.Contains("Lookback", StringComparison.OrdinalIgnoreCase))
                  .Select(kv => int.TryParse(kv.Value, out var v) ? v : 0).DefaultIfEmpty(0).Max() + 1
        };
    }

    /// <summary>
    /// 预热所需的日历天数（交易日 × 1.45 + 10，覆盖周末节假日）/ Calendar days of warm-up (trading bars × 1.45 + 10).
    /// </summary>
    public static int WarmupCalendarDays(int bars) => (int)Math.Ceiling(bars * 1.45) + 10;

    /// <summary>
    /// 加载数据。网络源自动带缓存；Demo 为离线合成数据（固定 260 根，从 start 起）。
    /// Loads data. Network sources are cached automatically; Demo is offline synthetic (fixed 260 bars from start).
    /// </summary>
    /// <param name="dataSource">数据源名（Demo/Yahoo/Binance/Tiingo/LocalFile/…）/ Data source name.</param>
    /// <param name="apiKey">可选 API key / Optional API key.</param>
    /// <param name="localFilePath">LocalFile：文件路径；多标的时可为目录（{dir}/{SYMBOL}.csv）或含 {symbol} 的模板。
    /// LocalFile: a file; for multiple symbols a directory ({dir}/{SYMBOL}.csv) or a template containing {symbol}.</param>
    /// <param name="symbols">符号 / Symbols.</param>
    /// <param name="fetchStart">下载起点（含预热）/ Download start (including warm-up).</param>
    /// <param name="end">终点 / End.</param>
    public static async Task<MarketData> LoadAsync(string? dataSource, string? apiKey, string? localFilePath,
        IReadOnlyList<string> symbols, DateTime fetchStart, DateTime end)
    {
        var provider = McpSourceDataFactory.Parse(dataSource);
        var bars = new Dictionary<string, IReadOnlyList<Ohlcv>>(StringComparer.OrdinalIgnoreCase);

        if (provider is null)
        {
            var demo = new DemoSyntheticSourceDataService();
            foreach (var s in symbols)
            {
                var o = await demo.DownloadOhlcvListAsync(s, fetchStart, end).ConfigureAwait(false);
                bars[s] = Normalize(s, o.OhlcvSet);
            }

            return new MarketData { Bars = bars, Provider = "Demo", IsDemo = true };
        }

        var factory = new McpSourceDataFactory();
        IMcpSourceDataService? shared = provider == McpSourceDataFactory.Provider.LocalFile ? null : factory.Create(provider.Value, apiKey, localFilePath);
        var failures = new List<string>();
        foreach (var s in symbols)
        {
            var src = shared ?? factory.Create(provider.Value, apiKey, ResolveLocalPath(localFilePath, s, symbols.Count));
            try
            {
                var o = await src.DownloadDailyAsync(s, fetchStart, end).ConfigureAwait(false);
                var list = Normalize(s, o.OhlcvSet);
                if (list.Count == 0) failures.Add($"{s}: no bars in window");
                bars[s] = list;
            }
            catch (Exception ex) when (ex is not ArgumentException)
            {
                failures.Add($"{s}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException($"Data load from {provider} failed — " + string.Join("; ", failures));
        }

        return new MarketData { Bars = bars, Provider = provider.Value.ToString() };
    }

    private static string? ResolveLocalPath(string? path, string symbol, int count)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        if (path.Contains("{symbol}", StringComparison.OrdinalIgnoreCase))
        {
            return path.Replace("{symbol}", symbol, StringComparison.OrdinalIgnoreCase);
        }

        if (Directory.Exists(path))
        {
            var csv = Path.Combine(path, symbol.ToUpperInvariant() + ".csv");
            return File.Exists(csv) ? csv : Path.Combine(path, symbol.ToUpperInvariant() + ".json");
        }

        return path; // single file (only meaningful for a single symbol)
    }

    private static IReadOnlyList<Ohlcv> Normalize(string symbol, IEnumerable<Ohlcv> bars)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        return bars
            .Select(b => { b.Symbol = sym; return b; })
            .GroupBy(b => b.OpenDateTime)
            .Select(g => g.First())
            .OrderBy(b => b.OpenDateTime)
            .ToList();
    }

    /// <summary>
    /// 在已加载数据上跑一次回测：自动把评估起点之前的 bar 作为预热（不计入绩效）。
    /// Runs one backtest on loaded data; bars before the evaluation start are warm-up (excluded from performance).
    /// </summary>
    public static async Task<BacktestResult> RunAsync(BacktestSpec spec, MarketData data, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(data);

        var symbols = spec.Symbols.Select(s => s.ToUpperInvariant()).ToList();
        var need = RequiredBars(spec.Strategy, spec.Parameters);
        var perSymbol = new Dictionary<string, IReadOnlyList<Ohlcv>>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in symbols)
        {
            if (!data.Bars.TryGetValue(s, out var all))
            {
                throw new ArgumentException($"No data loaded for symbol '{s}'.");
            }

            if (data.IsDemo)
            {
                perSymbol[s] = all;
                continue;
            }

            // 只保留"预热所需 + 评估窗口"的 bar：回测器每根 bar 都会切片全部历史，多余历史会让耗时成平方增长。
            // Keep only warm-up + evaluation bars: the runner slices all history on every bar, so surplus history
            // makes run time grow quadratically (walk-forward / grids).
            var firstEval = 0;
            while (firstEval < all.Count && all[firstEval].OpenDateTime < spec.Start.Date) firstEval++;
            var from = Math.Max(0, firstEval - need - 5);
            var to = all.Count;
            while (to > from && all[to - 1].OpenDateTime > spec.End.Date.AddDays(1)) to--;
            perSymbol[s] = all.Skip(from).Take(to - from).ToList();
        }

        var dataset = new Quant.Infra.Net.Backtest.Data.HistoricalDataSet(perSymbol);
        var warmupBars = data.IsDemo ? 0 : dataset.Timeline.Count(t => t < spec.Start.Date);
        if (!data.IsDemo && warmupBars >= dataset.Timeline.Count)
        {
            throw new ArgumentException($"No bars between {spec.Start:yyyy-MM-dd} and {spec.End:yyyy-MM-dd} — check symbols / dates.");
        }

        var weight = spec.WeightPerSymbol ?? DefaultWeight(spec.Strategy, spec.Parameters, symbols.Count);
        var services = new ServiceCollection();
        services.AddQuantInfraNet(
            rt =>
            {
                rt.RunMode = RunMode.Backtest;
                rt.DataSource = DataSourceKind.Demo; // 数据已预加载并按 as-of 切片注入；DI 只需一个占位源 / data is preloaded and injected as-of
            },
            o =>
            {
                foreach (var (key, value) in spec.Parameters)
                {
                    o.Parameters[key] = value;
                }

                o.Parameters["Strategy"] = spec.Strategy;
                o.Parameters["WeightPerSymbol"] = weight.ToString("0.########", CultureInfo.InvariantCulture);
                o.MaxWeightPerSymbol = Math.Max(weight, 1e-9);
                o.MaxGrossExposure = 1.0000001;
                o.KillSwitchDrawdownRate = spec.KillSwitchDrawdown ?? -10.0;
                o.InitialEquityUsd = spec.InitialEquityUsd;
            },
            b =>
            {
                b.InitialEquityUsd = spec.InitialEquityUsd;
                b.CommissionBps = spec.CommissionBps;
                b.SlippageBps = spec.SlippageBps;
                b.WarmupBars = warmupBars;
                b.FillTiming = spec.FillAtNextOpen ? FillTiming.NextBarOpen : FillTiming.SameBarClose;
            },
            strategyAssemblies: new[] { typeof(BacktestEngine).Assembly });

        using var provider = services.BuildServiceProvider();
        return await provider.GetRequiredService<BacktestRunner>().RunAsync(dataset, symbols, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 默认仓位：单标的 100%；配对每腿 50%；多因子 1/TopK。
    /// Default sizing: single-symbol 100%; pairs 50% per leg; multi-factor 1/TopK.
    /// </summary>
    public static double DefaultWeight(string strategy, IReadOnlyDictionary<string, string> p, int symbolCount)
    {
        string? Get(string k) => p.TryGetValue(k, out var v) ? v : null;
        return strategy.ToLowerInvariant() switch
        {
            "multifactor" => Math.Floor(1_000_000.0 / MultiFactorRotation.EffectiveTopK(Get)) / 1_000_000.0,
            "pairtradingzscore" => 0.5,
            _ => symbolCount <= 1 ? 1.0 : Math.Floor(1_000_000.0 / symbolCount) / 1_000_000.0
        };
    }
}
