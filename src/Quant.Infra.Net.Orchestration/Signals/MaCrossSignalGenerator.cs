using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Orchestration.Signals;

/// <summary>
/// 均线交叉信号生成器：Fast/SMA(慢) 双均线，close &gt;= SMA200 → Long，反之 Flat（AllowShort=true 时 Short）。
/// Moving-average cross signal generator: fast/slow MA pair; close &gt;= SMA200 → Long, else Flat (Short when AllowShort=true).
/// </summary>
/// <remarks>
/// 参数：Symbol（必填）、FastPeriod（默认 1，即 close）、SlowPeriod（默认 200，经典 200MA）、AllowShort（默认 false，"true"/"false"）。
/// 数据不足（closes.Count &lt; SlowPeriod + 1）→ 空集 + 事件。
/// Strength = |fast − slow| / slow；Reason 含 fast/slow 数值（InvariantCulture）。
/// Parameters per the design's default parameter table; deterministic and fully hand-testable without network.
/// </remarks>
public sealed class MaCrossSignalGenerator : BaseSignalGenerator
{
    /// <summary>
    /// 生成器 ID（与 §5 契约一致）。
    /// Generator id (per the §5 contract).
    /// </summary>
    public const string GeneratorId = "MaCross";

    /// <summary>
    /// 初始化均线交叉生成器。
    /// Initializes the MA-cross generator.
    /// </summary>
    /// <param name="analysisService">分析服务（不得为 null）/ Analysis service (must not be null).</param>
    /// <param name="yahooData">Yahoo 数据源（可选）/ Yahoo data source (optional).</param>
    /// <param name="binanceService">币安合约服务（可选）/ Binance futures service (optional).</param>
    /// <exception cref="ArgumentNullException">analysisService 为 null 时抛出 / Thrown when analysisService is null.</param>
    public MaCrossSignalGenerator(
        IAnalysisService analysisService,
        ITraditionalFinanceSourceDataService? yahooData = null,
        IBinanceUsdFutureService? binanceService = null)
        : base(analysisService, yahooData, binanceService)
    {
    }

    /// <inheritdoc />
    public override string Id => GeneratorId;

    /// <summary>
    /// 生成均线交叉信号（核心实现）。
    /// Generates MA-cross signals (core implementation).
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

        var fastPeriod = Math.Max(1, GetIntParam(context, "FastPeriod", 1));
        var slowPeriod = Math.Max(2, GetIntParam(context, "SlowPeriod", 200));
        var allowShort = GetBoolParam(context, "AllowShort", false);

        var closes = (await ResolveClosesAsync(context, symbol, ct).ConfigureAwait(false)).ToList();
        if (closes.Count < slowPeriod + 1)
        {
            context.AddEvent(PipelineEvent.Create(context.RunId, Id, $"insufficient data for '{symbol}': {closes.Count} < {slowPeriod + 1}"));
            return Array.Empty<Signal>();
        }

        var fastLine = closes.TakeLast(fastPeriod).Average();
        var slowLine = closes.TakeLast(slowPeriod).Average();

        var direction = fastLine >= slowLine
            ? SignalDirection.Long
            : allowShort ? SignalDirection.Short : SignalDirection.Flat;

        var strength = slowLine == 0.0 ? 0.0 : Math.Abs(fastLine - slowLine) / Math.Abs(slowLine);
        var reason = string.Format(System.Globalization.CultureInfo.InvariantCulture, "fast({0})={1:0.4} slow({2})={3:0.4}",
            new object[] { fastPeriod, fastLine, slowPeriod, slowLine });

        context.AddEvent(PipelineEvent.Create(context.RunId, Id, $"{symbol} → {direction}, {reason}"));

        return new[]
        {
            new Signal { Symbol = symbol, GeneratedUtc = DateTime.UtcNow, Direction = direction, Strength = strength, Reason = reason }
        };
    }
}



