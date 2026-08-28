using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Orchestration.Signals;

/// <summary>
/// 配对交易 Z-Score 信号生成器：OLS 回归残差（价差）+ 平稳性(ADF)过滤 + Z 值开平仓。
/// Pair-trading Z-Score signal generator: OLS residual (spread) + stationarity (ADF) filter + z-score entry/exit.
/// </summary>
/// <remarks>
/// 规则（§6 M2）：
/// - n = min(lenA, lenB)；n &lt; 50 → 空集 + "数据不足"事件；
/// - |corr(A,B)| &lt; MinCorrelation（参数，默认 0.8）→ 空集 + Reason/事件含相关性说明；
/// - OLS：B = Slope·A + Intercept；spread = B − Slope·A − Intercept（尾部 n 根对齐）；
/// - UseAdfFilter 为 true（默认）且价差非平稳（ADF 统计量 &gt; −2.86）→ 空集 + Reason/事件含 ADF 说明；
/// - lastZ = (spread_last − mean)/std；lastZ ≥ +1.5 → A=Short, B=Long；lastZ ≤ −1.5 → A=Long, B=Short；否则 Flat/Flat；
/// - Strength = |lastZ|，Reason 含 corr/slope/intercept/z 信息（InvariantCulture）。
/// Rules are per the design's M2 acceptance contract (direction mapping follows its test matrix: z ≥ +1.5 → A Short / B Long).
/// </remarks>
public sealed class PairTradingZScoreSignalGenerator : BaseSignalGenerator
{
    /// <summary>
    /// 生成器 ID（与 §5 契约一致）。
    /// Generator id (per the §5 contract).
    /// </summary>
    public const string GeneratorId = "PairTradingZScore";

    private const double AdfThreshold = -2.86;

    private const double EntryZ = 1.5;

    /// <summary>
    /// 初始化配对交易生成器。
    /// Initializes the pair-trading generator.
    /// </summary>
    /// <param name="analysisService">分析服务（不得为 null）/ Analysis service (must not be null).</param>
    /// <param name="yahooData">Yahoo 数据源（可选）/ Yahoo data source (optional).</param>
    /// <param name="binanceService">币安合约服务（可选）/ Binance futures service (optional).</param>
    /// <exception cref="ArgumentNullException">analysisService 为 null 时抛出 / Thrown when analysisService is null.</exception>
    public PairTradingZScoreSignalGenerator(
        IAnalysisService analysisService,
        ITraditionalFinanceSourceDataService? yahooData = null,
        IBinanceUsdFutureService? binanceService = null)
        : base(analysisService, yahooData, binanceService)
    {
    }

    /// <inheritdoc />
    public override string Id => GeneratorId;

    /// <summary>
    /// 生成配对交易信号（核心实现）。
    /// Generates pair-trading signals (core implementation).
    /// </summary>
    /// <param name="context">管道上下文（不得为 null）/ Pipeline context (must not be null).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>信号列表（A/B 各一条或空集）/ Signals (one per side or empty).</returns>
    protected override async Task<IReadOnlyList<Signal>> GenerateCoreAsync(IPipelineContext context, CancellationToken ct)
    {
        var symbolA = context.GetParameter("SymbolA");
        var symbolB = context.GetParameter("SymbolB");
        if (string.IsNullOrWhiteSpace(symbolA) || string.IsNullOrWhiteSpace(symbolB))
        {
            context.AddEvent(PipelineEvent.Create(context.RunId, Id, "rejected: missing SymbolA/SymbolB parameter"));
            return Array.Empty<Signal>();
        }

        var a = (await ResolveClosesAsync(context, symbolA, ct).ConfigureAwait(false)).ToList();
        var b = (await ResolveClosesAsync(context, symbolB, ct).ConfigureAwait(false)).ToList();

        var n = Math.Min(a.Count, b.Count);
        if (n < 50)
        {
            context.AddEvent(PipelineEvent.Create(context.RunId, Id, $"insufficient data for pair ({symbolA}/{symbolB}): n={n} < 50"));
            return Array.Empty<Signal>();
        }

        a = a.TakeLast(n).ToList();
        b = b.TakeLast(n).ToList();

        var corr = AnalysisService.CalculateCorrelation(a, b);
        var minCorr = GetDoubleParam(context, "MinCorrelation", 0.8);
        if (Math.Abs(corr) < minCorr)
        {
            context.AddEvent(PipelineEvent.Create(
                context.RunId, Id,
                string.Format(System.Globalization.CultureInfo.InvariantCulture, "rejected: |corr|={0:0.00} < MinCorrelation={1:0.00} ({2}/{3})",
                    new object[] { corr, minCorr, symbolA, symbolB })));
            return Array.Empty<Signal>();
        }

        var ols = AnalysisService.PerformOLSRegression(a, b);
        var spread = new List<double>(n);
        for (var i = 0; i < n; i++)
        {
            spread.Add(b[i] - (ols.Slope * a[i] + ols.Intercept));
        }

        var useAdf = GetBoolParam(context, "UseAdfFilter", true);
        var stationarityNote = "ADF=skipped";
        if (useAdf)
        {
            var adf = AnalysisService.AugmentedDickeyFullerTest(spread, AdfThreshold);
            if (!adf)
            {
                context.AddEvent(PipelineEvent.Create(
                    context.RunId, Id,
                    string.Format(System.Globalization.CultureInfo.InvariantCulture, "rejected: spread non-stationary by ADF (threshold={0:0.00}, {1}/{2})",
                        new object[] { AdfThreshold, symbolA, symbolB })));
                return Array.Empty<Signal>();
            }

            stationarityNote = "ADF=passed";
        }

        var mean = spread.Average();
        var std = OrchestrationNumerics.PopulationStdDev(spread);
        var lastZ = double.IsNaN(std) || Math.Abs(std) < 1e-12 ? 0.0 : (spread[spread.Count - 1] - mean) / std;

        var (dirA, dirB) = lastZ >= EntryZ
            ? (SignalDirection.Short, SignalDirection.Long)
            : lastZ <= -EntryZ
                ? (SignalDirection.Long, SignalDirection.Short)
                : (SignalDirection.Flat, SignalDirection.Flat);

        var strength = Math.Abs(lastZ);
        var reason = string.Format(System.Globalization.CultureInfo.InvariantCulture, "corr={0:0.00} slope={1:F} intercept={2:F} lastZ={3:F} {4}",
            new object[] { corr, ols.Slope, ols.Intercept, lastZ, stationarityNote });
        var now = DateTime.UtcNow;

        context.AddEvent(PipelineEvent.Create(context.RunId, Id, $"pair signal: {symbolA}={dirA} / {symbolB}={dirB}, {reason}"));

        return new[]
        {
            new Signal { Symbol = symbolA!, GeneratedUtc = now, Direction = dirA, Strength = strength, Reason = reason },
            new Signal { Symbol = symbolB!, GeneratedUtc = now, Direction = dirB, Strength = strength, Reason = reason }
        };
    }
}


