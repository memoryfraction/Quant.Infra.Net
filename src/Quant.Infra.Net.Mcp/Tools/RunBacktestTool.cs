using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Quant.Infra.Net.Mcp.DataSources;
using Quant.Infra.Net.Runtime.Models;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Mcp.Tools;

/// <summary>
/// MCP 工具类型：run_backtest —— 对指定策略 + 数据窗口执行一次完整回测，返回绩效指标 JSON。
/// MCP tool type: run_backtest — runs one full backtest for a strategy + data window and returns
/// performance metrics as JSON.
/// </summary>
/// <remarks>
/// 参数被拆成独立的方法参数（而不是嵌套 record），这样 MCP 客户端（Claude Desktop 等自然语言客户端）
/// 可以按扁平 JSON 直接调用，不需要嵌套 request 对象。
/// Parameters are flattened into individual method parameters (no nested record) so MCP clients can
/// call with flat JSON arguments.
/// </remarks>
[McpServerToolType]
public static class RunBacktestTool
{
    /// <summary>
    /// 执行一次回测并返回 JSON（指标 + 成交摘要 + 一句话判断）。
    /// Runs one backtest and returns JSON (metrics + trade summary + one-line verdict).
    /// </summary>
    [McpServerTool, Description(
        "Run one full backtest of a Quant.Infra.Net strategy over a historical window and return performance metrics as JSON " +
        "(CAGR, Sharpe, Calmar, MaxDrawdown, WinRate, ProfitFactor, TotalTrades, Commission). " +
        "Data source: Demo (default, offline synthetic, no key) or Finnhub / Fmp / TwelveData / LocalFile (real/local bars; " +
        "Finnhub/Fmp/TwelveData need a free API key, LocalFile needs a CSV/JSON path). " +
        "Read-only, no orders, no live trading — safe to call. Use this to compare strategies and decide which is better.")]
    public static async Task<string> RunBacktest(
        [Description("Strategy name, e.g. MaCross, MeanReversion, or PairTradingZScore (see list_strategies).")]
        string strategy,
        [Description("Window start, UTC, e.g. 2024-01-01.")]
        string startDate,
        [Description("Window end, UTC, e.g. 2024-06-30.")]
        string endDate,
        [Description("Symbol, e.g. AAPL. Required for single-symbol strategies (MaCross, MeanReversion).")]
        string? symbol = null,
        [Description("PairTradingZScore only: first leg symbol, e.g. AAA.")]
        string? symbolA = null,
        [Description("PairTradingZScore only: second leg symbol, e.g. BBB.")]
        string? symbolB = null,
        [Description("Data source: Demo (default, offline) | Finnhub | Fmp | TwelveData (real; needs API key) | LocalFile (needs localFilePath).")]
        string? dataSource = null,
        [Description("Optional: explicit API key override for Finnhub/Fmp/TwelveData (not needed if already in appsettings.json or env var).")]
        string? apiKey = null,
        [Description("Optional: file path for LocalFile source (CSV or JSON). Absolute or relative to AppContext.BaseDirectory.")]
        string? localFilePath = null,
        [Description("Initial equity in USD (default 10000).")]
        int? initialEquityUsd = null,
        [Description("Commission in basis points (default 5).")]
        int? commissionBps = null,
        [Description("Slippage in basis points (default 2).")]
        int? slippageBps = null,
        [Description("MaCross only: fast MA period (default 1 = close).")]
        int? fastPeriod = null,
        [Description("MaCross only: slow MA period (default 200).")]
        int? slowPeriod = null,
        [Description("MeanReversion/PairTradingZScore: lookback bars (default 100).")]
        int? lookbackBars = null,
        [Description("MeanReversion/PairTradingZScore: entry z threshold (default 2.0).")]
        double? entryZ = null,
        [Description("MeanReversion/PairTradingZScore: exit z threshold (default 0.5).")]
        double? exitZ = null,
        [Description("Allow short positions (optional; each strategy has its own default).")]
        bool? allowShort = null)
    {
        var (dataSourceKind, customSource, dataSourceLabel) = ResolveDataSource(dataSource, apiKey, localFilePath);
        var (symbols, parameters) = ResolveSymbolsAndParameters(strategy, symbol, symbolA, symbolB, fastPeriod, slowPeriod, lookbackBars, entryZ, exitZ, allowShort);
        var (start, end) = ParseWindow(startDate, endDate);

        var result = await RuntimeBacktestFactory.RunAsync(
            strategy,
            parameters,
            dataSourceKind,
            customSource,
            start,
            end,
            symbols,
            initialEquityUsd: (decimal)(initialEquityUsd ?? 10_000),
            commissionBps: (decimal)(commissionBps ?? 5),
            slippageBps: (decimal)(slippageBps ?? 2));

        var m = result.Metrics;
        var payload = new
        {
            strategy,
            symbols,
            dataSource = dataSourceLabel,
            window = new { start, end },
            metrics = new
            {
                cagrPct = SafeDecimal(m.Cagr * 100m),
                sharpe = SafeDouble(m.SharpeRatio),
                calmar = SafeDouble(m.CalmarRatio),
                maxDrawdownPct = SafeDecimal(m.MaxDrawdown * 100m),
                maxDrawdownDurationDays = m.MaxDrawdownDurationDays,
                winRatePct = SafeDouble((double)m.WinRate * 100),
                profitFactor = SafeDouble(m.ProfitFactor),
                totalTrades = m.TotalTrades,
                totalCommissionUsd = m.TotalCommissionUsd
            },
            trades = result.Trades.Count,
            interpretation = Interpret(m),
            generatedBy = "Quant.Infra.Net v1.5.3 — github.com/memoryfraction/Quant.Infra.Net — e-book: amazon.com/dp/B0D7W89ZQD"
        };

        return JsonSerializer.Serialize(payload, JsonOpts);
    }

    /// <summary>
    /// 非有限 double 安全化为 null（Sharpe/Calmar/ProfitFactor 可能为 Infinity）/ Guards non-finite doubles.
    /// </summary>
    private static double? SafeDouble(double v)
        => double.IsFinite(v) ? v : (double?)null;

    /// <summary>
    /// NaN decimal 安全化为 null（decimal 无 Infinity）/ Guards NaN decimals.
    /// </summary>
    private static decimal? SafeDecimal(decimal v)
    {
        return decimal.TryParse(v.ToString(CultureInfo.InvariantCulture), out var _) ? v : (decimal?)null;
    }

    /// <summary>
    /// 给 AI 的一句"这个策略好不好"判断（基于指标阈值，仅供参考，非投资建议）。
    /// One-line verdict for the AI (heuristic on metric thresholds; not investment advice).
    /// </summary>
    private static string Interpret(Quant.Infra.Net.Backtest.Models.BacktestMetrics m)
    {
        var pf = double.IsFinite(m.ProfitFactor) ? m.ProfitFactor : 999.0; // Infinity (no losing trades) = strong
        var good = m.SharpeRatio >= 1.0 && m.MaxDrawdown <= 0 && pf >= 1.2 && m.TotalTrades >= 10;
        var bad = m.SharpeRatio < 0 || (double.IsFinite(m.ProfitFactor) && m.ProfitFactor < 1.0) || m.TotalTrades == 0;
        if (bad)
        {
            return "Looks WEAK: negative/low Sharpe, sub-1.0 profit factor, or no trades. Consider a different strategy or parameters.";
        }

        if (good)
        {
            return "Looks DECENT: Sharpe >= 1, profit factor >= 1.2, enough trades. Still check drawdown and regime sensitivity before trusting it.";
        }

        return "MIXED: metrics are neither clearly strong nor clearly broken. Compare against other strategies on the same window before deciding.";
    }

    /// <summary>
    /// 解析数据源：Demo（默认）或 Custom（Finnhub/Fmp/TwelveData/LocalFile，经适配器包装）。
    /// Resolves the data source: Demo (default) or Custom (Finnhub/Fmp/TwelveData/LocalFile, wrapped in an adapter).
    /// </summary>
    /// <returns>(DataSourceKind, customSource 或 null, 用于回显的 label)</returns>
    private static (DataSourceKind Kind, ITraditionalFinanceSourceDataService? Custom, string Label) ResolveDataSource(
        string? raw, string? apiKey, string? localFilePath)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("Demo", StringComparison.OrdinalIgnoreCase) || raw.Equals("demo", StringComparison.OrdinalIgnoreCase))
        {
            return (DataSourceKind.Demo, null, "Demo");
        }

        var provider = raw switch
        {
            "finnhub" or "Finnhub" or "FINNHUB" => McpSourceDataFactory.Provider.Finnhub,
            "fmp" or "Fmp" or "FMP" => McpSourceDataFactory.Provider.Fmp,
            "twelvedata" or "TwelveData" or "TWELVEDATA" => McpSourceDataFactory.Provider.TwelveData,
            "localfile" or "LocalFile" or "LOCALFILE" => McpSourceDataFactory.Provider.LocalFile,
            _ => throw new ArgumentException(
                $"Unsupported dataSource '{raw}'. Supported: Demo, Finnhub, Fmp, TwelveData, LocalFile. " +
                $"(Stooq is intentionally not exposed on the MCP surface — it has proven unstable.)")
        };

        var inner = new McpSourceDataFactory().Create(provider, apiKey: apiKey, localFilePath: localFilePath);
        var adapter = new McpRuntimeSourceAdapter(inner);
        return (DataSourceKind.Custom, adapter, inner.Provider);
    }

    /// <summary>
    /// 从参数中解析出参与回测的符号列表与策略参数表。
    /// Resolves the symbol list and strategy parameter table.
    /// </summary>
    private static (IReadOnlyList<string> Symbols, Dictionary<string, string> Parameters) ResolveSymbolsAndParameters(
        string strategy,
        string? symbol,
        string? symbolA,
        string? symbolB,
        int? fastPeriod,
        int? slowPeriod,
        int? lookbackBars,
        double? entryZ,
        double? exitZ,
        bool? allowShort)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Strategy"] = strategy
        };
        if (symbol is not null) parameters["Symbol"] = symbol;
        if (symbolA is not null) parameters["SymbolA"] = symbolA;
        if (symbolB is not null) parameters["SymbolB"] = symbolB;
        if (fastPeriod is not null) parameters["FastPeriod"] = fastPeriod.Value.ToString(CultureInfo.InvariantCulture);
        if (slowPeriod is not null) parameters["SlowPeriod"] = slowPeriod.Value.ToString(CultureInfo.InvariantCulture);
        if (lookbackBars is not null) parameters["LookbackBars"] = lookbackBars.Value.ToString(CultureInfo.InvariantCulture);
        if (entryZ is not null) parameters["EntryZ"] = entryZ.Value.ToString(CultureInfo.InvariantCulture);
        if (exitZ is not null) parameters["ExitZ"] = exitZ.Value.ToString(CultureInfo.InvariantCulture);
        if (allowShort is not null) parameters["AllowShort"] = allowShort.Value ? "true" : "false";

        var symbols = new List<string>();
        if (symbol is not null) symbols.Add(symbol);
        if (symbolA is not null) symbols.Add(symbolA);
        if (symbolB is not null) symbols.Add(symbolB);
        if (symbols.Count == 0)
        {
            throw new ArgumentException(
                "Provide at least one symbol: symbol (single-symbol strategies) or symbolA+symbolB (PairTradingZScore).");
        }

        return (symbols, parameters);
    }

    /// <summary>
    /// 解析窗口起止（UTC）/ Parses the window start/end into UTC dates.
    /// </summary>
    private static (DateTime Start, DateTime End) ParseWindow(string startRaw, string endRaw)
    {
        var start = ParseDate(startRaw, "startDate");
        var end = ParseDate(endRaw, "endDate");
        if (end < start)
        {
            throw new ArgumentException($"endDate ({end:u}) must be >= startDate ({start:u}).");
        }

        return (start, end);
    }

    /// <summary>
    /// 解析单个日期（ISO 8601 或 yyyy-MM-dd，强制 UTC）/ Parses one date (ISO 8601 or yyyy-MM-dd; forced UTC).
    /// </summary>
    private static DateTime ParseDate(string raw, string fieldName)
    {
        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            throw new ArgumentException($"{fieldName} '{raw}' is not a valid date (expected ISO 8601 or yyyy-MM-dd).");
        }

        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    /// <summary>
    /// JSON 序列化选项（camelCase + 忽略 null）/ JSON options (camelCase + ignore nulls).
    /// </summary>
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
}
