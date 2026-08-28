using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Orchestration.Signals;

/// <summary>
/// 均值回归信号生成器：滚动窗口 Z 值开平仓（z ≤ −EntryZ → Long；z ≥ +EntryZ → Short/Flat；|z| ≤ ExitZ → Flat 平仓）。
/// Mean-reversion signal generator: rolling-window z-score entry/exit (z ≤ −EntryZ → Long; z ≥ +EntryZ → Short/Flat; |z| ≤ ExitZ → Flat exit).
/// </summary>
/// <remarks>
/// 参数：Symbol（必填）、LookbackBars（默认 100）、EntryZ（默认 2.0）、ExitZ（默认 0.5）、AllowShort（默认 true）。
/// 数据不足（序列长度 &lt; max(10, LookbackBars/10)）→ 空集 + 事件；std=0 → Flat（无可回归性）。
/// 中性区（ExitZ &lt; |z| &lt; EntryZ）→ 空集 + "中性区"事件。
/// Strength = |z|，Reason 含 mean/std/z（InvariantCulture）。
/// Parameters per the design's default table; neutral zone (ExitZ &lt; |z| &lt; EntryZ) yields an empty set with an explanatory event.
/// </remarks>
public sealed class MeanReversionSignalGenerator : BaseSignalGenerator
{
    /// <summary>
    /// 生成器 ID（与 §5 契约一致）。
    /// Generator id (per the §5 contract).
    /// </summary>
    public const string GeneratorId = "MeanReversion";

    /// <summary>
    /// 初始化均值回归生成器。
    /// Initializes the mean-reversion generator.
    /// </summary>
    /// <param name="analysisService">分析服务（不得为 null）/ Analysis service (must not be null).</param>
    /// <param name="yahooData">Yahoo 数据源（可选）/ Yahoo data source (optional).</param>
    /// <param name="binanceService">币安合约服务（可选）/ Binance futures service (optional).</param>
    /// <exception cref="ArgumentNullException">analysisService 为 null 时抛出 / Thrown when analysisService is null.</exception>
    public MeanReversionSignalGenerator(
        IAnalysisService analysisService,
        ITraditionalFinanceSourceDataService? yahooData = null,
        IBinanceUsdFutureService? binanceService = null)
        : base(analysisService, yahooData, binanceService)
    {
    }

    /// <inheritdoc />
    public override string Id => GeneratorId;

    /// <summary>
    /// 生成均值回归信号（核心实现）。
    /// Generates mean-reversion signals (core implementation).
    /// </summary>
    /// <param name="context">管道上下文（不得为 null）/ Pipeline context (must not be null).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>信号列表（1 条或空集）/ Signals (one or empty).</returns>
    protected override async Task<IReadOnlyList<Signal>> GenerateCoreAsync(IPipelineContext context, CancellationToken ct)
    {
        var symbol = context.GetParameter("Symbol");
        if (string.IsNullOrWhiteSpace(symbol))
        {
            context.AddEvent(PipelineEvent.Create(context.RunId, Id, "rejected: missing Symbol parameter"));
            return Array.Empty<Signal>();
        }

        var lookback = Math.Max(10, GetIntParam(context, "LookbackBars", 100));
        var entryZ = GetDoubleParam(context, "EntryZ", 2.0);
        var exitZ = Math.Max(0.0, GetDoubleParam(context, "ExitZ", 0.5));
        var allowShort = GetBoolParam(context, "AllowShort", true);

        var closes = (await ResolveClosesAsync(context, symbol, ct).ConfigureAwait(false)).ToList();
        var minBars = Math.Max(10, lookback / 10);
        if (closes.Count < minBars)
        {
            context.AddEvent(PipelineEvent.Create(context.RunId, Id, $"insufficient data for '{symbol}': {closes.Count} < {minBars}"));
            return Array.Empty<Signal>();
        }

        var window = closes.TakeLast(lookback).ToList();
        var last = window[window.Count - 1];
        var mean = window.Average();
        var std = OrchestrationNumerics.PopulationStdDev(window);

        var now = DateTime.UtcNow;

        if (Math.Abs(std) < 1e-12)
        {
            var flatReason = string.Format(System.Globalization.CultureInfo.InvariantCulture, "mean={0:F} std=0 (degenerate window) → Flat", mean);
            context.AddEvent(PipelineEvent.Create(context.RunId, Id, $"{symbol} → Flat, {flatReason}"));
            return new[]
            {
                new Signal { Symbol = symbol, GeneratedUtc = now, Direction = SignalDirection.Flat, Strength = 0, Reason = flatReason }
            };
        }

        var z = (last - mean) / std;

        SignalDirection direction;
        if (z <= -entryZ)
        {
            direction = SignalDirection.Long;
        }
        else if (z >= entryZ)
        {
            direction = allowShort ? SignalDirection.Short : SignalDirection.Flat;
        }
        else if (Math.Abs(z) <= exitZ)
        {
            direction = SignalDirection.Flat;
        }
        else
        {
            context.AddEvent(PipelineEvent.Create(
                context.RunId, Id,
                string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} neutral zone: z={1:0.00} not triggered", new object[] { symbol, z })));
            return Array.Empty<Signal>();
        }

        var reason = string.Format(System.Globalization.CultureInfo.InvariantCulture, 
            "mean={0:F} std={1:F} z={2:0.00} → {3}", new object[] { mean, std, z, direction });

        context.AddEvent(PipelineEvent.Create(context.RunId, Id, $"{symbol} → {direction}, {reason}"));

        return new[]
        {
            new Signal { Symbol = symbol, GeneratedUtc = now, Direction = direction, Strength = Math.Abs(z), Reason = reason }
        };
    }
}



