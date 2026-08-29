using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Backtest.Data;

/// <summary>
/// 历史数据集：一次性预取全区间行情，按需切出"截至某时刻"的子集，供逐 bar 回放注入 PipelineContext。
/// Historical data set: pre-fetches the full range once; slices an "as-of" subset per bar for injection into the PipelineContext.
/// </summary>
/// <remarks>
/// 这是防未来函数（look-ahead bias）的关键：SliceUpTo(symbol, asOfUtc) 保证返回的 Ohlcvs
/// 只包含 OpenDateTime &lt;= asOfUtc 的 K 线；BacktestRunner 在每根模拟 bar 开始前调用它，
/// 把结果 Set 进新建的 PipelineContext，DataIngestStage/SignalDataLoader 会优先命中这个缓存
/// 而不再发起任何实时拉取（见 §4），因此策略代码天然不可能看到未来数据。
/// This is the look-ahead-bias guard: SliceUpTo(symbol, asOfUtc) only returns bars with
/// OpenDateTime &lt;= asOfUtc; DataIngestStage/SignalDataLoader hit this cache first and never
/// fetch live data, so strategy code cannot see future bars.
/// </remarks>
public sealed class HistoricalDataSet
{
    private readonly Dictionary<string, List<Ohlcv>> _seriesBySymbol = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 初始化历史数据集（内部对每个 symbol 排序去重，并构建全局时间轴）。
    /// Initializes the data set (internally sorts and de-duplicates per symbol, and builds the global timeline).
    /// </summary>
    /// <param name="seriesBySymbol">每个 symbol 的完整历史序列（无需预先排序，内部会排序去重）/ Full historical series per symbol (need not be pre-sorted; de-duplicated internally).</param>
    /// <exception cref="ArgumentNullException">入参为 null 时抛出 / Thrown when the argument is null.</exception>
    public HistoricalDataSet(IReadOnlyDictionary<string, IReadOnlyList<Ohlcv>> seriesBySymbol)
    {
        if (seriesBySymbol == null)
        {
            throw new ArgumentNullException(nameof(seriesBySymbol));
        }

        var timestamps = new SortedSet<DateTime>();
        foreach (var (symbol, bars) in seriesBySymbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new ArgumentException("Symbol keys must not be blank.", nameof(seriesBySymbol));
            }

            if (bars == null)
            {
                throw new ArgumentException($"Symbol '{symbol}' must not map to null.", nameof(seriesBySymbol));
            }

            // 去重（完全相同的 K 线折叠）后按开盘时间升序。
            // De-duplicate identical bars, then sort by OpenDateTime ascending.
            var sorted = bars
                .Where(static b => b != null)
                .Distinct()
                .OrderBy(static b => b!.OpenDateTime)
                .ToList();
            _seriesBySymbol[symbol] = sorted;
            timestamps.UnionWith(sorted.Select(b => b.OpenDateTime));
        }

        Timeline = timestamps.ToList();
    }

    /// <summary>
    /// 全部 symbol 时间戳的并集，升序。
    /// The union of all symbols' timestamps, ascending.
    /// </summary>
    public IReadOnlyList<DateTime> Timeline { get; }

    /// <summary>
    /// 截至 asOfUtc（含）的某 symbol 历史切片（只含 OpenDateTime &lt;= asOfUtc 的 K 线）。
    /// The as-of (inclusive) slice for one symbol (only bars with OpenDateTime &lt;= asOfUtc).
    /// </summary>
    /// <param name="symbol">标的代码（大小写不敏感）/ Trading symbol (case-insensitive).</param>
    /// <param name="asOfUtc">模拟时刻（UTC），切片含该时刻 / The simulated instant in UTC; the slice is inclusive of it.</param>
    /// <returns>切片（未知 symbol 返回空集合，不抛异常）/ The slice (an empty set for unknown symbols; no exception).</returns>
    /// <exception cref="ArgumentException">symbol 为空白时抛出 / Thrown when the symbol is blank.</exception>
    public Ohlcvs SliceUpTo(string symbol, DateTime asOfUtc)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol must not be blank.", nameof(symbol));
        }

        var result = new Ohlcvs { Symbol = symbol };
        if (!_seriesBySymbol.TryGetValue(symbol, out var series))
        {
            return result;
        }

        foreach (var bar in series)
        {
            if (bar.OpenDateTime > asOfUtc)
            {
                break; // 已按时间升序，后续全部是未来 bar / sorted ascending; the rest are future bars
            }

            result.OhlcvSet.Add(bar);
        }

        if (result.OhlcvSet.Count > 0)
        {
            result.StartDateTimeUtc = result.OhlcvSet.Min(b => b.OpenDateTime);
            result.EndDateTimeUtc = result.OhlcvSet.Max(b => b.OpenDateTime);
        }

        return result;
    }

    /// <summary>
    /// 某个模拟时刻的收盘价（取 &lt;= asOfUtc 的最近一根 bar；供 broker.SetMarkPrice 使用；无数据返回 null）。
    /// The close price at a simulated instant (last bar at or before asOfUtc; feeds broker.SetMarkPrice; null when absent).
    /// </summary>
    /// <param name="symbol">标的代码（大小写不敏感）/ Trading symbol (case-insensitive).</param>
    /// <param name="asOfUtc">模拟时刻（UTC）/ The simulated instant in UTC.</param>
    /// <returns>收盘价；无数据时 null / The close price, or null when no qualifying bar exists.</returns>
    /// <exception cref="ArgumentException">symbol 为空白时抛出 / Thrown when the symbol is blank.</exception>
    public double? CloseAt(string symbol, DateTime asOfUtc)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol must not be blank.", nameof(symbol));
        }

        if (!_seriesBySymbol.TryGetValue(symbol, out var series))
        {
            return null;
        }

        double? lastClose = null;
        foreach (var bar in series)
        {
            if (bar.OpenDateTime > asOfUtc)
            {
                break;
            }

            lastClose = (double)bar.Close;
        }

        return lastClose;
    }

    /// <summary>
    /// 该 symbol 在 asOfUtc 之后第一根 bar（严格 &gt; asOfUtc）的开盘价（NextBarOpen 成交锚点；无后续 bar 返回 null）。
    /// The open price of the first bar strictly after asOfUtc for one symbol (the NextBarOpen fill anchor; null when there is no later bar).
    /// </summary>
    /// <param name="symbol">标的代码（大小写不敏感）/ Trading symbol (case-insensitive).</param>
    /// <param name="afterUtc">模拟时刻（UTC），严格之后 / The simulated instant in UTC; strictly after it.</param>
    /// <returns>开盘价；无后续 bar 时 null / The open price, or null when no later bar exists.</returns>
    /// <exception cref="ArgumentException">symbol 为空白时抛出 / Thrown when the symbol is blank.</exception>
    public double? OpenAtNextAfter(string symbol, DateTime afterUtc)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol must not be blank.", nameof(symbol));
        }

        if (!_seriesBySymbol.TryGetValue(symbol, out var series))
        {
            return null;
        }

        foreach (var bar in series)
        {
            if (bar.OpenDateTime > afterUtc)
            {
                return (double)bar.Open;
            }
        }

        return null;
    }
}
