using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Orchestration.Execution;

/// <summary>
/// 纸上交易实现：编排层自建的 <see cref="IBinanceUsdFutureService"/> 纯内存实现，不发任何网络请求。
/// In-memory paper-trading implementation of <see cref="IBinanceUsdFutureService"/> — issues no network calls.
/// 默认在 Paper 环境下由 AddQuantInfraNetOrchestration() 注册为 IBinanceUsdFutureService 单例。
/// Default-registered as the IBinanceUsdFutureService singleton in Paper mode by AddQuantInfraNetOrchestration().
/// 记账模型：权益 = 初始权益 + 已实现盈亏 + 未实现盈亏（基于最新已知的最新收盘价/入场价）。
/// Accounting: equity = initial equity + realized PnL + unrealized PnL (mark price vs entry price; zero when either is unknown).
/// </summary>
public sealed class PaperBinanceUsdFutureService : IBinanceUsdFutureService
{
    private readonly object _gate = new();
    private readonly decimal _initialEquityUsd;
    private decimal _realizedPnlUsd;

    // 每个 symbol 的有符号名义持仓（USD；正=多头，负=空头）与入场价（可能未知）。
    // Per-symbol signed notional in USD (positive = long, negative = short) and possibly-unknown entry price.
    private readonly Dictionary<string, double> _notionalUsd = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double?> _entryPrice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _markPrice = new(StringComparer.OrdinalIgnoreCase);
    private bool _isHedgeMode = true;

    /// <summary>
    /// 创建纸上交易服务。
    /// Creates a paper-trading service.
    /// </summary>
    /// <param name="options">
    /// 编排配置（提供 InitialEquityUsd；为 null 时采用默认 10000）。
    /// Orchestration options (supply InitialEquityUsd; defaults to 10000 when null).
    /// </param>
    /// <exception cref="ArgumentException">InitialEquityUsd 为负时抛出 / Thrown when InitialEquityUsd is negative.</exception>
    public PaperBinanceUsdFutureService(OrchestrationOptions? options = null)
    {
        _initialEquityUsd = options?.InitialEquityUsd ?? 10000m;
        if (_initialEquityUsd < 0m)
        {
            throw new ArgumentException("InitialEquityUsd must not be negative.", nameof(options));
        }
    }

    /// <inheritdoc />
    public ExchangeEnvironment ExchangeEnvironment { get; set; } = ExchangeEnvironment.Paper;

    /// <summary>
    /// 只读访问内部状态（测试与诊断用）：当前权益（USD）。
    /// Read-only diagnostic accessor: current equity in USD.
    /// </summary>
    public decimal CurrentEquityUsd => ComputeEquityUsd();

    /// <inheritdoc />
    public Task<Ohlcvs> GetOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt, ResolutionLevel resolutionLevel = ResolutionLevel.Hourly)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol must not be blank.", nameof(symbol));
        }

        // Paper 环境不做任何行情拉取（零网络）：行情由 DataIngestStage 通过配置的数据源注入。
        // Paper mode fetches nothing (zero network); market data is injected by DataIngestStage.
        return Task.FromResult(new Ohlcvs
        {
            Symbol = symbol,
            ResolutionLevel = resolutionLevel,
            StartDateTimeUtc = startDt,
            EndDateTimeUtc = endDt,
            OhlcvSet = new HashSet<Ohlcv>()
        });
    }

    /// <inheritdoc />
    public Task<IEnumerable<string>> GetUsdFutureSymbolsAsync()
    {
        lock (_gate)
        {
            var symbols = _notionalUsd.Keys.Where(k => _notionalUsd[k] != 0d).ToList();
            foreach (var k in _markPrice.Keys)
            {
                if (!symbols.Contains(k))
                {
                    symbols.Add(k);
                }
            }

            return Task.FromResult<IEnumerable<string>>(symbols.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        }
    }

    /// <inheritdoc />
    public Task<IEnumerable<BinancePositionDetailsUsdt>> GetHoldingPositionAsync()
    {
        lock (_gate)
        {
            var result = new List<BinancePositionDetailsUsdt>();
            foreach (var kvp in _notionalUsd)
            {
                var notional = kvp.Value;
                if (Math.Abs(notional) < 1e-12)
                {
                    continue;
                }

                double? entry = _entryPrice.TryGetValue(kvp.Key, out var ep) ? ep : (double?)null;
                double? mark = _markPrice.TryGetValue(kvp.Key, out var mp) ? mp : (double?)null;
                var price = mark ?? entry;
                if (price is null || price <= 0d)
                {
                    continue; // 无可用价格：持仓存在但无法给出数量明细，跳过（HasUsdFuturePositionAsync 仍为 true）
                }

                var sign = notional >= 0d ? 1d : -1d;
                var basePrice = entry ?? price.Value; // 入场价未知时用标记价作数量基准 / fall back to mark for quantity basis
                var quantity = notional / basePrice; // 单位基准数量（base units）
                var markValue = quantity * price.Value;
                var unrealized = notional > 0d
                    ? (entry is null ? 0d : markValue - (Math.Abs(quantity) * entry.Value))
                    : (entry is null ? 0d : (Math.Abs(quantity) * entry.Value) - markValue);

                result.Add(new BinancePositionDetailsUsdt
                {
                    Symbol = kvp.Key,
                    Quantity = (decimal)(sign * Math.Abs(quantity)),
                    EntryPrice = (decimal)(entry ?? 0d),
                    MarkPrice = (decimal)price.Value,
                    UnrealizedPnl = (decimal)unrealized,
                    Notional = (decimal)Math.Abs(markValue),
                    PositionSide = notional > 0d ? PositionSide.Long : PositionSide.Short,
                    Leverage = 1
                });
            }

            return Task.FromResult<IEnumerable<BinancePositionDetailsUsdt>>(result);
        }
    }

    /// <inheritdoc />
    public Task<decimal> GetusdFutureAccountBalanceAsync()
        => Task.FromResult(ComputeEquityUsd());

    /// <inheritdoc />
    public Task<double> GetusdFutureUnrealizedProfitRateAsync()
    {
        double totalUnrealized;
        decimal baseEquity;
        lock (_gate)
        {
            totalUnrealized = ComputeUnrealizedUsd();
            baseEquity = _initialEquityUsd + _realizedPnlUsd;
        }

        if (baseEquity <= 0m)
        {
            return Task.FromResult(0d);
        }

        return Task.FromResult((double)(totalUnrealized / (double)baseEquity));
    }

    /// <inheritdoc />
    public Task LiquidateUsdFutureAsync(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol must not be blank.", nameof(symbol));
        }

        lock (_gate)
        {
            if (_notionalUsd.TryGetValue(symbol, out var notional) && Math.Abs(notional) > 1e-12)
            {
                _realizedPnlUsd += (decimal)UnrealizedOf(notional, symbol, closeAt: null);
                _notionalUsd[symbol] = 0d;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetUsdFutureHoldingsAsync(string symbol, double rate, PositionSide positionSide = PositionSide.Both)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol must not be blank.", nameof(symbol));
        }

        if (rate < 0d || rate > 1d)
        {
            throw new ArgumentException("rate must be within [0, 1].", nameof(rate));
        }

        lock (_gate)
        {
            var equity = (double)ComputeEquityUsd();
            var sign = positionSide == PositionSide.Short ? -1d : 1d; // Both 视为多头 / both-side treated as long
            var oldNotional = _notionalUsd.TryGetValue(symbol, out var old) ? old : 0d;
            if (Math.Abs(oldNotional) > 1e-12)
            {
                _realizedPnlUsd += (decimal)UnrealizedOf(oldNotional, symbol, closeAt: null);
            }

            var target = sign * rate * equity;
            if (Math.Abs(target) < 1e-12)
            {
                _notionalUsd[symbol] = 0d;
            }
            else
            {
                _notionalUsd[symbol] = target;
                // 入场价取当前标记价（若已知）；否则保持未知，直到首个标记价出现（避免伪盈亏）。
                // Entry price = current mark if known; otherwise unknown until the first mark appears (no fictitious PnL).
                if (_markPrice.TryGetValue(symbol, out var mark))
                {
                    _entryPrice[symbol] = mark;
                }
                else if (!_entryPrice.TryGetValue(symbol, out var prevEntry) || prevEntry is null)
                {
                    _entryPrice[symbol] = null;
                }
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> HasUsdFuturePositionAsync(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol must not be blank.", nameof(symbol));
        }

        lock (_gate)
        {
            return Task.FromResult(_notionalUsd.TryGetValue(symbol, out var notional) && Math.Abs(notional) > 1e-12);
        }
    }

    /// <inheritdoc />
    public Task ShowPositionModeAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public Task SetPositionModeAsync(bool isHedgeMode = true)
    {
        lock (_gate)
        {
            _isHedgeMode = isHedgeMode;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 当前是否双向持仓模式。
    /// Whether two-sided (hedge) mode is currently active.
    /// </summary>
    public bool IsHedgeMode
    {
        get { lock (_gate) { return _isHedgeMode; } }
    }

    /// <summary>
    /// 登记某个 symbol 的最新收盘价（标记价），用于估值与未实现盈亏计算。
    /// Registers the latest close (mark price) for a symbol, used for valuation and unrealized PnL.
    /// </summary>
    /// <param name="symbol">标的（不得为空白）/ Symbol (must not be blank).</param>
    /// <param name="closePrice">最新收盘价（&gt; 0）/ Latest close (must be positive).</param>
    /// <exception cref="ArgumentException">symbol 空白或价格非正时抛出 / Thrown for blank symbol or non-positive price.</exception>
    public void SetMarkPrice(string symbol, double closePrice)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol must not be blank.", nameof(symbol));
        }

        if (closePrice <= 0d)
        {
            throw new ArgumentException("closePrice must be positive.", nameof(closePrice));
        }

        lock (_gate)
        {
            _markPrice[symbol] = closePrice;
            // 首个标记价若早于入场价记录：补记入场价 = 首个标记价（从当刻起开始计盈亏）。
            // First mark also pins the entry price when unknown (PnL starts counting from now on).
            if (!_entryPrice.TryGetValue(symbol, out var ep) || ep is null)
            {
                _entryPrice[symbol] = closePrice;
            }
        }
    }

    /// <summary>
    /// 批量登记最新收盘价（键不得为空白）。
    /// Registers several (symbol, latest close) pairs (keys must not be blank).
    /// </summary>
    /// <param name="latestCloses">symbol 到最新收盘价 / Symbol to latest close.</param>
    /// <exception cref="ArgumentNullException">入参为 null 时抛出 / Thrown when the argument is null.</exception>
    public void SetMarkPrices(IReadOnlyDictionary<string, double> latestCloses)
    {
        if (latestCloses == null)
        {
            throw new ArgumentNullException(nameof(latestCloses));
        }

        foreach (var kvp in latestCloses)
        {
            SetMarkPrice(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// 基于当前标记价重估权益（测试/诊断用）。
    /// Recomputes equity against the current marks (for tests/diagnostics).
    /// </summary>
    /// <returns>权益（USD）/ Equity in USD.</returns>
    public decimal RecomputeEquityUsd() => ComputeEquityUsd();

    private decimal ComputeEquityUsd()
    {
        lock (_gate)
        {
            return _initialEquityUsd + _realizedPnlUsd + (decimal)ComputeUnrealizedUsd();
        }
    }

    private double ComputeUnrealizedUsd()
    {
        // 调用方必须已持有 _gate。
        // Callers must already hold _gate.
        var total = 0d;
        foreach (var kvp in _notionalUsd)
        {
            if (Math.Abs(kvp.Value) < 1e-12)
            {
                continue;
            }

            total += UnrealizedOf(kvp.Value, kvp.Key, closeAt: null);
        }

        return total;
    }

    private double UnrealizedOf(double notional, string symbol, double? closeAt)
    {
        // closeAt = 平仓价（null = 按标记价估值）。
        // closeAt = closing price (null = mark valuation).
        if (!_entryPrice.TryGetValue(symbol, out var entry) || entry is null)
        {
            return 0d; // 入场价未知：不计盈亏（保守）/ unknown entry: no PnL (conservative)
        }

        var e = entry!.Value;
        var p = closeAt ?? (_markPrice.TryGetValue(symbol, out var mp) ? mp : e);
        if (p <= 0d || e <= 0d)
        {
            return 0d;
        }

        var baseQuantity = Math.Abs(notional) / e;
        return notional > 0d
            ? baseQuantity * (p - e)          // 多头：(市价 − 入场) × 数量 / long: (price - entry) × qty
            : baseQuantity * (e - p);        // 空头：(入场 − 市价) × 数量 / short: (entry - price) × qty
    }
}
