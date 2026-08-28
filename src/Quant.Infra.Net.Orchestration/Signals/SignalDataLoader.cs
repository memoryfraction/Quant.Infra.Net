using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Orchestration.Signals;

/// <summary>
/// 信号数据装载器（静态）：context 缓存优先 → 数据源回退 → 无数据时返回空序列（不抛业务异常）。
/// Signal data loader (static): context cache first → data-source fallback → empty series with no business exception when no data exists.
/// </summary>
/// <remarks>
/// 供 BaseSignalGenerator 与 Stages.DataIngestStage 共用，保证装载规则单一实现（SRP）。
/// Shared by BaseSignalGenerator and Stages.DataIngestStage so the loading rule has a single implementation (SRP).
/// </remarks>
public static class SignalDataLoader
{
    /// <summary>
    /// 上下文是否已存在该标的的行情（单槽或合并槽）。
    /// Whether the context already holds market data for the symbol (single slot or merged slot).
    /// </summary>
    /// <param name="context">管道上下文 / Pipeline context.</param>
    /// <param name="symbol">标的代码 / Trading symbol.</param>
    /// <returns>有缓存返回 true / True when cached.</returns>
    public static bool HasCachedSeries(IPipelineContext context, string symbol)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol must not be blank.", nameof(symbol));
        }

        var single = context.Get<Ohlcvs>();
        if (single != null && string.Equals(single.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var union = context.Get<HashSet<Ohlcv>>();
        return union != null && union.Any(o => string.Equals(o?.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 读取标的升序收盘价序列（缓存优先，回退数据源；无数据返回空集并不抛业务异常）。
    /// Reads the ascending close series for a symbol (cache first, source fallback; returns an empty set without business exceptions when no data exists).
    /// </summary>
    /// <param name="context">管道上下文（不得为 null）/ Pipeline context (must not be null).</param>
    /// <param name="symbol">标的代码（不得为空白）/ Trading symbol (must not be blank).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <param name="yahooData">Yahoo 数据源（可选）/ Yahoo data source (optional).</param>
    /// <param name="binanceService">币安合约服务（可选）/ Binance futures service (optional).</param>
    /// <returns>升序收盘价（无数据为空）/ Ascending closes (empty when absent).</returns>
    /// <exception cref="ArgumentNullException">context 为 null 时抛出 / Thrown when context is null.</exception>
    /// <exception cref="ArgumentException">symbol 为空白时抛出 / Thrown when symbol is blank.</exception>
    public static async Task<IReadOnlyList<double>> LoadClosesAsync(
        IPipelineContext context,
        string symbol,
        CancellationToken ct,
        ITraditionalFinanceSourceDataService? yahooData,
        IBinanceUsdFutureService? binanceService)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol must not be blank.", nameof(symbol));
        }

        var single = context.Get<Ohlcvs>();
        if (single != null && string.Equals(single.Symbol, symbol, StringComparison.OrdinalIgnoreCase) && single.OhlcvSet.Count > 0)
        {
            return CloseSeries(single);
        }

        var union = context.Get<HashSet<Ohlcv>>();
        if (union != null && union.Any(o => string.Equals(o?.Symbol, symbol, StringComparison.OrdinalIgnoreCase)))
        {
            return union
                .Where(o => string.Equals(o?.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                .OrderBy(o => o.OpenDateTime)
                .Select(o => (double)o.Close)
                .ToList();
        }

        return await FetchAsync(context, symbol, ct, yahooData, binanceService).ConfigureAwait(false);
    }

    private static IReadOnlyList<double> CloseSeries(Ohlcvs ohlcvs)
    {
        return ohlcvs.OhlcvSet
            .OrderBy(o => o.OpenDateTime)
            .Select(o => (double)o.Close)
            .ToList();
    }

    private static async Task<IReadOnlyList<double>> FetchAsync(
        IPipelineContext context,
        string symbol,
        CancellationToken ct,
        ITraditionalFinanceSourceDataService? yahooData,
        IBinanceUsdFutureService? binanceService)
    {
        var source = SignalParams.GetDataSource(context);
        var bars = Math.Max(1, SignalParams.GetInt(context, "LookbackBars", 240));
        var resolution = SignalParams.ParseResolution(context);
        var endDt = DateTime.UtcNow;
        var span = resolution switch
        {
            ResolutionLevel.Daily => TimeSpan.FromDays(1),
            ResolutionLevel.Hourly => TimeSpan.FromHours(1),
            ResolutionLevel.Weekly => TimeSpan.FromDays(7),
            ResolutionLevel.Monthly => TimeSpan.FromDays(30),
            _ => TimeSpan.FromMinutes(1)
        };
        var startDt = endDt - span * (int)(bars * 1.3) - span; // 1.3x 缓冲覆盖缺 bar / 1.3x buffer to cover missing bars

        try
        {
            if (source == "yahoo" && yahooData != null)
            {
                var ohlcvs = await yahooData.DownloadOhlcvListAsync(
                    symbol, startDt, endDt, resolution, DataSource.YahooFinance).ConfigureAwait(false);
                if (ohlcvs?.OhlcvSet.Count > 0)
                {
                    context.Set(ohlcvs);
                    return CloseSeries(ohlcvs);
                }
            }
            else if (source == "binance" && binanceService != null)
            {
                var ohlcvs = await binanceService.GetOhlcvListAsync(symbol, startDt, endDt, resolution).ConfigureAwait(false);
                if (ohlcvs?.OhlcvSet.Count > 0)
                {
                    context.Set(ohlcvs);
                    return CloseSeries(ohlcvs);
                }
            }

            context.AddEvent(PipelineEvent.Create(context.RunId, "DataLoad", $"no data available for '{symbol}' (source={source})"));
        }
        catch (Exception ex)
        {
            // 网络/数据源故障：记录事件并降级为无数据（不抛业务异常）。
            // Network / data-source failure: record an event and degrade to no-data (no business exception).
            context.AddEvent(PipelineEvent.Create(context.RunId, "DataLoad", $"data fetch failed for '{symbol}' (source={source}): {ex.Message}"));
        }

        return Array.Empty<double>();
    }
}


