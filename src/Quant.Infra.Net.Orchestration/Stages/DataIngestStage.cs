using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Signals;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Orchestration.Stages;

/// <summary>
/// 数据装载阶段：按参数装载所需标的的 OHLCV 并写入 context（单槽 Ohlcvs 或合并槽 HashSet&lt;Ohlcv&gt;）。
/// Data ingest stage: loads OHLCV for the required symbols per parameters and stores them in the context (single Ohlcvs slot or merged HashSet slot).
/// </summary>
/// <remarks>
/// 容错：任一带数据源故障只记录 DataLoad 事件并继续（不得终止管道）；装载完成后 context.Get&lt;Ohlcvs&gt;()/Get&lt;HashSet&lt;Ohlcv&gt;&gt; 供后续阶段读取。
/// Fault-tolerant: per-symbol data-source failures only record DataLoad events and continue; later stages read context.Get&lt;Ohlcvs&gt;()/Get&lt;HashSet&lt;Ohlcv&gt;&gt;.
/// </remarks>
public sealed class DataIngestStage : IPipelineStage
{
    /// <summary>
    /// 初始化数据装载阶段。
    /// Initializes the data ingest stage.
    /// </summary>
    /// <param name="yahooData">Yahoo 数据源（可选）/ Yahoo data source (optional).</param>
    /// <param name="binanceService">币安合约服务（可选）/ Binance futures service (optional).</param>
    public DataIngestStage(ITraditionalFinanceSourceDataService? yahooData = null, IBinanceUsdFutureService? binanceService = null)
    {
        YahooData = yahooData;
        BinanceService = binanceService;
    }

    private ITraditionalFinanceSourceDataService? YahooData { get; }

    private IBinanceUsdFutureService? BinanceService { get; }

    /// <summary>
    /// 阶段名（固定 "DataIngest"）。
    /// Stage name (fixed "DataIngest").
    /// </summary>
    public string Name => "DataIngest";

    /// <summary>
    /// 执行数据装载（容错，不抛业务异常）。
    /// Executes data ingestion (fault-tolerant, no business exceptions).
    /// </summary>
    /// <param name="context">管道上下文（不得为 null）/ Pipeline context (must not be null).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>表示操作完成的任务 / Task representing completion.</returns>
    public async Task ExecuteAsync(IPipelineContext context, CancellationToken ct)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var symbols = new List<string>();
        AddSymbol(symbols, context.GetParameter("SymbolA"), context);
        AddSymbol(symbols, context.GetParameter("SymbolB"), context);
        if (symbols.Count == 0)
        {
            AddSymbol(symbols, context.GetParameter("Symbol"), context);
        }

        if (symbols.Count == 0)
        {
            context.AddEvent(PipelineEvent.Create(context.RunId, Name, "no symbols configured; nothing to ingest"));
            return;
        }

        var merged = new HashSet<Ohlcv>();
        var loaded = 0;

        foreach (var symbol in symbols)
        {
            if (SignalDataLoader.HasCachedSeries(context, symbol))
            {
                continue;
            }

            try
            {
                var closes = await SignalDataLoader.LoadClosesAsync(context, symbol, ct, YahooData, BinanceService).ConfigureAwait(false);
                if (closes.Count > 0)
                {
                    loaded++;
                    CollectCached(context, symbol, merged);
                }
            }
            catch (Exception ex)
            {
                context.AddEvent(PipelineEvent.Create(context.RunId, Name, $"ingest failed for '{symbol}': {ex.Message}"));
            }
        }

        if (merged.Count > 0)
        {
            context.Set(merged);
        }

        context.AddEvent(PipelineEvent.Create(context.RunId, Name, $"ingest done: {loaded}/{symbols.Count} symbol(s) loaded, merged={merged.Count} bars"));
    }

    private static void AddSymbol(List<string> symbols, string? candidate, IPipelineContext context)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        if (!symbols.Any(s => string.Equals(s, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            symbols.Add(candidate);
        }
    }

    private static void CollectCached(IPipelineContext context, string symbol, HashSet<Ohlcv> sink)
    {
        var single = context.Get<Ohlcvs>();
        if (single != null && string.Equals(single.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
        {
            sink.UnionWith(single.OhlcvSet);
            return;
        }

        var union = context.Get<HashSet<Ohlcv>>();
        if (union != null)
        {
            sink.UnionWith(union.Where(o => string.Equals(o?.Symbol, symbol, StringComparison.OrdinalIgnoreCase)));
        }
    }
}


