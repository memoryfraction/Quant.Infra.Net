using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Quant.Infra.Net.Mcp.Engine;
using Quant.Infra.Net.Mcp.Reporting;

namespace Quant.Infra.Net.Mcp.Tools;

/// <summary>
/// MCP 工具类型：run_backtest —— 对指定策略 + 数据窗口执行一次完整回测，返回绩效 JSON + 生成图文 HTML 报告。
/// MCP tool type: run_backtest — runs one full backtest and returns performance JSON plus an HTML report with charts.
/// </summary>
/// <remarks>
/// 参数被拆成独立的方法参数（而不是嵌套 record），这样 MCP 客户端可以按扁平 JSON 直接调用。
/// Parameters are flattened into individual method parameters so MCP clients can call with flat JSON arguments.
/// </remarks>
[McpServerToolType]
public static class RunBacktestTool
{
    /// <summary>
    /// 执行一次回测并返回 JSON（指标 + 基准对比 + 市场状态拆解 + 年度收益 + 报告路径）。
    /// Runs one backtest and returns JSON (metrics + benchmark + regimes + yearly returns + report path).
    /// </summary>
    [McpServerTool(Name = "run_backtest"), Description(
        "Run one full backtest of a strategy over a historical window. Returns JSON with metrics (CAGR, Sharpe, Sortino, Calmar, " +
        "MaxDrawdown, win rate, profit factor), a buy-and-hold benchmark comparison (alpha/beta), calendar-year returns, a " +
        "market-regime breakdown (bull/bear × calm/volatile — shows WHEN the strategy works), automatic warnings, a monthly-sampled " +
        "equity curve you can chart, and the path of a self-contained HTML report with charts. " +
        "Strategies: MaCross, MeanReversion, PairTradingZScore, MultiFactor (cross-sectional rotation over a universe; see list_strategies). " +
        "Data: dataSource=Yahoo (FREE, no key: US/HK/China stocks, ETFs, indices like ^GSPC, FX, futures) | Binance (FREE, no key: crypto like BTCUSDT) | " +
        "Tiingo (free key) | LocalFile (your CSV) | Demo (offline synthetic, default). Warm-up history before startDate is fetched automatically. " +
        "Fills at next bar open by default (no look-ahead). Read-only, no orders, no live trading — safe to call repeatedly.")]
    public static async Task<string> RunBacktest(
        [Description("Strategy name: MaCross | MeanReversion | PairTradingZScore | MultiFactor (see list_strategies).")]
        string strategy,
        [Description("Window start (UTC), e.g. 2015-01-01.")]
        string startDate,
        [Description("Window end (UTC), e.g. 2024-12-31, or 'today'.")]
        string endDate,
        [Description("Single-symbol strategies (MaCross, MeanReversion): symbol, e.g. SPY, AAPL, BTCUSDT.")]
        string? symbol = null,
        [Description("PairTradingZScore only: first leg, e.g. KO.")]
        string? symbolA = null,
        [Description("PairTradingZScore only: second leg, e.g. PEP.")]
        string? symbolB = null,
        [Description("MultiFactor only: comma-separated universe, e.g. \"SPY,QQQ,IWM,EFA,EEM,TLT,GLD,DBC\".")]
        string? universe = null,
        [Description("MultiFactor only: factor weights, e.g. \"momentum=1,lowvol=0.5\". Factors: momentum, lowvol, trend, reversal, riskadjmom.")]
        string? factors = null,
        [Description("MultiFactor only: number of holdings (default: a third of the universe).")]
        int? topK = null,
        [Description("MultiFactor only: rebalance every N bars (default 21 ≈ monthly).")]
        int? rebalanceDays = null,
        [Description("MultiFactor only: regime filter symbol, e.g. SPY — when it trades below its SMA (RegimeSmaPeriod, default 200) go to cash / defensiveSymbol.")]
        string? regimeSymbol = null,
        [Description("MultiFactor only: defensive asset held when the regime filter is off or nothing has positive momentum, e.g. SHY or TLT.")]
        string? defensiveSymbol = null,
        [Description("Advanced: any other strategy parameters as \"Key=Value;Key=Value\" or a JSON object, e.g. \"MomentumLookback=252;SkipRecent=21\".")]
        string? parameters = null,
        [Description("Benchmark symbol for comparison (default: buy & hold of the traded symbol, or equal-weight of the universe). E.g. SPY.")]
        string? benchmark = null,
        [Description("Data source: Demo (default, offline) | Yahoo (free, no key) | Binance (free, no key, crypto) | Tiingo (free key) | LocalFile | Finnhub | Fmp | TwelveData. Call list_data_sources for details.")]
        string? dataSource = null,
        [Description("Optional API key override for Tiingo/Finnhub/Fmp/TwelveData (normally set via env var in the MCP client config).")]
        string? apiKey = null,
        [Description("LocalFile: CSV/JSON path; for several symbols a directory containing SYMBOL.csv files, or a template like C:/data/{symbol}.csv.")]
        string? localFilePath = null,
        [Description("Initial equity in USD (default 10000).")]
        int? initialEquityUsd = null,
        [Description("Commission in basis points per fill (default 5).")]
        int? commissionBps = null,
        [Description("Slippage in basis points per fill (default 2).")]
        int? slippageBps = null,
        [Description("Position size per symbol in % of equity (default: 100 single-symbol, 50 per pair leg, 100/TopK for MultiFactor).")]
        double? positionSizePct = null,
        [Description("Optional kill-switch: stop adding risk when drawdown exceeds this %, e.g. 20. Default off.")]
        double? killSwitchDrawdownPct = null,
        [Description("Fill at the next bar's open (default true, realistic). false = fill at the signal bar's close.")]
        bool? fillAtNextOpen = null,
        [Description("Write the HTML report with charts (default true).")]
        bool? report = null,
        [Description("MaCross: fast MA period (default 1 = close).")]
        int? fastPeriod = null,
        [Description("MaCross: slow MA period (default 200).")]
        int? slowPeriod = null,
        [Description("MeanReversion/PairTradingZScore: lookback bars (default 100).")]
        int? lookbackBars = null,
        [Description("MeanReversion/PairTradingZScore: entry z threshold (default 2.0).")]
        double? entryZ = null,
        [Description("MeanReversion/PairTradingZScore: exit z threshold (default 0.5).")]
        double? exitZ = null,
        [Description("Allow short positions (each strategy has its own default).")]
        bool? allowShort = null)
    {
        var req = ResearchService.BuildRequest(strategy, startDate, endDate, symbol, symbolA, symbolB, universe, parameters,
            NamedParameters(factors, topK, rebalanceDays, regimeSymbol, defensiveSymbol, fastPeriod, slowPeriod, lookbackBars, entryZ, exitZ, allowShort),
            benchmark, dataSource, apiKey, localFilePath, initialEquityUsd, commissionBps, slippageBps, positionSizePct, killSwitchDrawdownPct, fillAtNextOpen);

        var data = await ResearchService.LoadAsync(req).ConfigureAwait(false);
        var spec = req.ToSpec(req.Start, req.End);
        var result = await BacktestEngine.RunAsync(spec, data).ConfigureAwait(false);
        var m = result.Metrics;

        BacktestAnalysis? analysis = null;
        string? analysisError = null;
        try
        {
            analysis = PerformanceAnalytics.Analyze(result, data, spec.Symbols, req.Benchmark);
        }
        catch (Exception ex)
        {
            analysisError = ex.Message;
        }

        string? reportPath = null;
        if (analysis is not null && report != false)
        {
            var title = $"{req.Strategy} {string.Join(" ", spec.Symbols.Take(8))}";
            var subtitle = $"{req.Start:yyyy-MM-dd} → {req.End:yyyy-MM-dd} · data: {data.Provider} · costs {req.CommissionBps}+{req.SlippageBps} bps · " +
                           (req.FillAtNextOpen ? "fills at next open" : "fills at close");
            reportPath = HtmlReportWriter.Write(title, subtitle, spec.Parameters, analysis, result);
        }

        var payload = new
        {
            strategy = req.Strategy,
            symbols = spec.Symbols,
            dataSource = data.Provider,
            window = new { start = req.Start, end = req.End },
            parameters = spec.Parameters,
            metrics = new
            {
                cagrPct = Math.Round(m.Cagr * 100m, 4),
                sharpe = SafeDouble(m.SharpeRatio),
                calmar = SafeDouble(m.CalmarRatio),
                maxDrawdownPct = Math.Round(m.MaxDrawdown * 100m, 4),
                maxDrawdownDurationDays = m.MaxDrawdownDurationDays,
                winRatePct = SafeDouble((double)m.WinRate * 100),
                profitFactor = SafeDouble(m.ProfitFactor),
                totalTrades = m.TotalTrades,
                totalCommissionUsd = m.TotalCommissionUsd,
                sortino = analysis?.Strategy.Sortino,
                annualVolatilityPct = analysis?.Strategy.AnnVolPct,
                totalReturnPct = analysis?.Strategy.TotalReturnPct,
                exposurePct = analysis?.ExposurePct,
                annualTurnover = analysis?.AnnualTurnover
            },
            benchmark = analysis is null ? null : new
            {
                label = analysis.BenchmarkLabel,
                cagrPct = analysis.Benchmark.CagrPct,
                sharpe = analysis.Benchmark.Sharpe,
                maxDrawdownPct = analysis.Benchmark.MaxDrawdownPct,
                beta = analysis.Beta,
                alphaPct = analysis.AlphaPct,
                correlation = analysis.Correlation
            },
            regimes = analysis?.Regimes,
            regimeVerdict = analysis?.RegimeVerdict,
            yearly = analysis?.Yearly,
            warnings = analysis?.Warnings,
            analysisError,
            trades = result.Trades.Count,
            interpretation = Interpret(m),
            report = ResearchService.ReportInfo(reportPath),
            equityCurveMonthly = analysis is null ? null : ResearchService.SampleCurve(analysis),
            nextSteps = new[]
            {
                "optimize_strategy: grid-search parameters with an out-of-sample holdout (detects overfitting).",
                "walk_forward: re-optimize on rolling windows to see if the edge survives regime changes.",
                "Compare against the benchmark and other strategies on the SAME window before trusting a result."
            },
            generatedBy = "Quant.Infra.Net MCP v1.6 — github.com/memoryfraction/Quant.Infra.Net — not investment advice"
        };

        return JsonSerializer.Serialize(payload, JsonOpts);
    }

    /// <summary>
    /// 把常用的命名参数映射到策略参数键 / Maps common named arguments to strategy parameter keys.
    /// </summary>
    internal static Dictionary<string, string?> NamedParameters(string? factors, int? topK, int? rebalanceDays, string? regimeSymbol, string? defensiveSymbol,
        int? fastPeriod, int? slowPeriod, int? lookbackBars, double? entryZ, double? exitZ, bool? allowShort)
    {
        string? I(int? v) => v?.ToString(CultureInfo.InvariantCulture);
        string? D(double? v) => v?.ToString(CultureInfo.InvariantCulture);
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Factors"] = factors,
            ["TopK"] = I(topK),
            ["RebalanceDays"] = I(rebalanceDays),
            ["RegimeSymbol"] = regimeSymbol,
            ["DefensiveSymbol"] = defensiveSymbol,
            ["FastPeriod"] = I(fastPeriod),
            ["SlowPeriod"] = I(slowPeriod),
            ["LookbackBars"] = I(lookbackBars),
            ["EntryZ"] = D(entryZ),
            ["ExitZ"] = D(exitZ),
            ["AllowShort"] = allowShort is null ? null : allowShort.Value ? "true" : "false"
        };
    }

    private static double? SafeDouble(double v) => double.IsFinite(v) ? Math.Round(v, 4) : null;

    /// <summary>
    /// 给 AI 的一句"这个策略好不好"判断（基于指标阈值，仅供参考，非投资建议）。
    /// One-line verdict for the AI (heuristic on metric thresholds; not investment advice).
    /// </summary>
    internal static string Interpret(Quant.Infra.Net.Backtest.Models.BacktestMetrics m)
    {
        var pf = double.IsFinite(m.ProfitFactor) ? m.ProfitFactor : 999.0;
        var good = m.SharpeRatio >= 1.0 && pf >= 1.2 && m.TotalTrades >= 10;
        var bad = m.SharpeRatio < 0 || (double.IsFinite(m.ProfitFactor) && m.ProfitFactor < 1.0) || m.TotalTrades == 0;
        if (bad) return "Looks WEAK: negative/low Sharpe, sub-1.0 profit factor, or no trades. Consider a different strategy or parameters.";
        if (good) return "Looks DECENT: Sharpe >= 1, profit factor >= 1.2, enough trades. Still check the regime table, benchmark and walk_forward before trusting it.";
        return "MIXED: metrics are neither clearly strong nor clearly broken. Compare against the benchmark and other strategies on the same window.";
    }

    internal static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };
}
