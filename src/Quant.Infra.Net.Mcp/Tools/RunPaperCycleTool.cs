using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Runtime;
using Quant.Infra.Net.Runtime.Models;
using Quant.Infra.Net.Runtime.Strategies;

namespace Quant.Infra.Net.Mcp.Tools;

/// <summary>
/// MCP tool type: run_paper_cycle — runs one pipeline cycle under RunMode.Paper
/// (PaperBinanceUsdFutureService simulated bookkeeping, zero network, no real funds),
/// and returns the event stream + error count + optional portfolio snapshot.
/// </summary>
/// <remarks>
/// Deliberate product boundary: Paper mode uses an in-memory broker (PaperBinanceUsdFutureService)
/// that issues no network calls and no real orders. Testnet/Live are intentionally NOT exposed
/// by any MCP tool. See docs/manual/mcp-server-en.md for the full statement.
/// </remarks>
[McpServerToolType]
public static class RunPaperCycleTool
{
    /// <summary>
    /// Runs one Paper pipeline cycle and returns JSON (events + errors + runId + optional snapshot).
    /// </summary>
    [McpServerTool, Description(
        "Run ONE paper-trading pipeline cycle (RunMode.Paper) using Quant.Infra.Net's PaperBinanceUsdFutureService " +
        "for simulated bookkeeping. Zero network, no real funds, no real orders — safe to call. " +
        "Returns the event stream (Stage/Message/TimestampUtc) and any errors from the cycle. " +
        "Use this to see how a strategy behaves tick-by-tick in a simulated environment.")]
    public static async Task<string> RunPaperCycle(
        [Description("Strategy name, e.g. MaCross, MeanReversion, or PairTradingZScore (see list_strategies).")]
        string strategy,
        [Description("Symbol, e.g. AAPL. Required for single-symbol strategies.")]
        string? symbol = null,
        [Description("PairTradingZScore only: first leg symbol.")]
        string? symbolA = null,
        [Description("PairTradingZScore only: second leg symbol.")]
        string? symbolB = null,
        [Description("Optional: MaCross fast MA period.")]
        int? fastPeriod = null,
        [Description("Optional: MaCross slow MA period.")]
        int? slowPeriod = null,
        [Description("Optional: MeanReversion/PairTradingZScore lookback bars.")]
        int? lookbackBars = null,
        [Description("Optional: entry z threshold.")]
        double? entryZ = null,
        [Description("Optional: exit z threshold.")]
        double? exitZ = null)
    {
        if (string.IsNullOrWhiteSpace(strategy))
            throw new ArgumentException("strategy is required.", nameof(strategy));

        // 1) Resolve strategy (validate + get descriptor) — fail fast on unknown names.
        var catalog = new StrategyCatalog(new[] { typeof(QuantInfraNetMcpServer).Assembly });
        catalog.Resolve(strategy);

        // 2) Build parameter table.
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

        // 3) Build DI container (RunMode.Paper + Demo data source, zero network).
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddQuantInfraNet(
            rt =>
            {
                rt.RunMode = RunMode.Paper;
                rt.DataSource = DataSourceKind.Demo;
            },
            o =>
            {
                foreach (var (k, v) in parameters) o.Parameters[k] = v;
            },
            b =>
            {
                b.InitialEquityUsd = 10_000m;
                b.CommissionBps = 5m;
                b.SlippageBps = 2m;
            },
            strategyAssemblies: new[] { typeof(QuantInfraNetMcpServer).Assembly });

        using var provider = services.BuildServiceProvider();
        var runner = provider.GetRequiredService<PipelineRunner>();

        // 4) Run one cycle.
        var ctx = await runner.RunOnceAsync().ConfigureAwait(false);

        // 5) Shape output (camelCase JSON).
        var events = ctx.Events.Select(e => new
        {
            stage = e.Stage,
            message = e.Message,
            timestampUtc = e.TimestampUtc
        }).ToList();
        var errors = ctx.Errors.Count;
        var errorMessages = ctx.Errors.Select(e => e.Message).ToList();

        var snap = ctx.Get<PortfolioSnapshot>();
        var portfolio = snap is not null ? new
        {
            equityUsd = snap.AccountEquityUsd,
            snapshotUtc = snap.SnapshotUtc,
            actualWeights = snap.ActualWeights,
            targetWeights = snap.TargetWeights
        } : null;

        var payload = new
        {
            runId = ctx.RunId,
            strategy,
            mode = "Paper",
            events,
            errors,
            errorMessages,
            portfolio
        };

        return JsonSerializer.Serialize(payload, JsonOpts);
    }

    /// <summary>
    /// JSON serialization options (camelCase + ignore nulls).
    /// </summary>
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
}
