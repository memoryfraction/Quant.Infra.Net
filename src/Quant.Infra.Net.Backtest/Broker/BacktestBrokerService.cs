using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures;
using Quant.Infra.Net.Backtest.Models;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Backtest.Broker;

/// <summary>
/// 回测经纪商：零网络、按模拟时间记账的 <see cref="IBinanceUsdFutureService"/> 内存实现。
/// Backtest broker: a zero-network, simulated-time in-memory <see cref="IBinanceUsdFutureService"/>.
/// </summary>
/// <remarks>
/// 记账模型与 PaperBinanceUsdFutureService 逐语句对齐（D4），零手续费零滑点时对同一操作序列
/// 产生完全一致的权益序列（B2 测试锚点）。在此之上按 BacktestOptions 叠加：
/// CommissionBps（按成交名义价值扣减权益）、SlippageBps（成交价在标记价上做不利方向偏移）、
/// 并在每次开/平仓追加一条 <see cref="BacktestTrade"/> 记录。
/// Notional = signed USD notional (positive long / negative short), mirroring Paper's
/// statements exactly so that CommissionBps = SlippageBps = 0 reproduces Paper's equity
/// sequences verbatim (B2 parity tests). On top of that it layers commission deduction,
/// adverse-direction slippage, and one BacktestTrade record per open/close.
/// </remarks>
public sealed class BacktestBrokerService : IBinanceUsdFutureService
{
    private const double PositionEpsilon = 1e-12; // 与 Paper 相同的持仓判零阈值 / same zero-position epsilon as Paper

    private readonly object _gate = new();
    private readonly decimal _initialEquityUsd;
    private readonly decimal _commissionBps;
    private readonly double _slippage;
    private decimal _realizedPnlUsd;
    private decimal _commissionUsdTotal;
    private bool _isHedgeMode = true;
    private DateTime _simulatedNowUtc = DateTime.UtcNow;

    // 与 Paper 相同结构：每 symbol 的有符号名义持仓（USD，正=多头，负=空头）与入场价（可能未知）。
    // Same shape as Paper: signed USD notional per symbol (positive long / negative short) and possibly-unknown entry price.
    private readonly Dictionary<string, double> _notionalUsd = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double?> _entryPrice = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _markPrice = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<BacktestTrade> _trades = new();
    private readonly List<Action> _pendingOrders = new();
    private bool _deferFills;

    /// <summary>
    /// 创建回测经纪商。
    /// Creates a backtest broker.
    /// </summary>
    /// <param name="options">回测参数（提供 InitialEquityUsd/CommissionBps/SlippageBps；不得为 null）/ Backtest options (supply InitialEquityUsd/CommissionBps/SlippageBps; must not be null).</param>
    /// <exception cref="ArgumentNullException">入参为 null 时抛出 / Thrown when the argument is null.</exception>
    /// <exception cref="ArgumentException">负初始权益/负费率时抛出 / Thrown for negative initial equity or rates.</exception>
    public BacktestBrokerService(BacktestOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _initialEquityUsd = options.InitialEquityUsd;
        if (_initialEquityUsd < 0m)
        {
            throw new ArgumentException("InitialEquityUsd must not be negative.", nameof(options));
        }

        _commissionBps = options.CommissionBps;
        if (_commissionBps < 0m)
        {
            throw new ArgumentException("CommissionBps must not be negative.", nameof(options));
        }

        var slippageBps = options.SlippageBps;
        if (slippageBps < 0m)
        {
            throw new ArgumentException("SlippageBps must not be negative.", nameof(options));
        }

        _slippage = (double)slippageBps / 10000d;
    }

    /// <summary>
    /// 回测环境恒非实盘；借用 Paper 枚举成员表达"非实盘"（不扩展 ExchangeEnvironment 枚举，§11.5）。
    /// Backtest is never a live venue; the Paper enum member expresses "non-live" (the ExchangeEnvironment enum is not extended, §11.5).
    /// </summary>
    public ExchangeEnvironment ExchangeEnvironment { get; set; } = ExchangeEnvironment.Paper;

    /// <summary>
    /// 模拟当前时刻（用于给成交记录打时间戳；BacktestRunner 每根 bar 前设置）。
    /// The simulated now instant (stamps trade records; BacktestRunner sets it before each bar).
    /// </summary>
    public DateTime SimulatedNowUtc
    {
        get { lock (_gate) { return _simulatedNowUtc; } }
        set
        {
            lock (_gate)
            {
                _simulatedNowUtc = value;
            }
        }
    }

    /// <summary>
    /// 只读成交记录快照（按成交顺序追加）。
    /// Read-only snapshot of the trade log (append-ordered).
    /// </summary>
    public IReadOnlyList<BacktestTrade> Trades
    {
        get { lock (_gate) { return _trades.ToList(); } }
    }

    /// <summary>
    /// 当前权益（USD）= 初始权益 + 已实现盈亏 + 未实现盈亏 − 累计手续费。
    /// Current equity (USD) = initial + realized + unrealized − accumulated commission.
    /// </summary>
    public decimal CurrentEquityUsd
    {
        get
        {
            lock (_gate)
            {
                return _initialEquityUsd + _realizedPnlUsd + (decimal)ComputeUnrealizedUsd() - _commissionUsdTotal;
            }
        }
    }

    /// <summary>
    /// 登记某 symbol 的最新值（标记价），用于估值、未实现盈亏与成交偏移基准。
    /// Registers a symbol's latest price (mark) used for valuation, unrealized P/L, and the slippage base.
    /// </summary>
    /// <param name="symbol">标的（不得为空白）/ Symbol (must not be blank).</param>
    /// <param name="price">最新价格（&gt; 0）/ Latest price (must be positive).</param>
    public void SetMarkPrice(string symbol, double price)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol must not be blank.", nameof(symbol));
        }

        if (price <= 0d)
        {
            throw new ArgumentException("price must be positive.", nameof(price));
        }

        lock (_gate)
        {
            _markPrice[symbol] = price;
            // 与 Paper 相同：首个标记价补记未知入场价（从当刻起计盈亏）。
            // Same as Paper: the first mark pins an unknown entry price (P/L counts from here on).
            if (!_entryPrice.TryGetValue(symbol, out var ep) || ep is null)
            {
                _entryPrice[symbol] = price;
            }
        }
    }

    /// <summary>
    /// 批量登记最新值（键不得为空白）。
    /// Registers several (symbol, price) pairs (keys must not be blank).
    /// </summary>
    /// <param name="latestPrices">symbol 到最新价格 / Symbol to latest price.</param>
    public void SetMarkPrices(IReadOnlyDictionary<string, double> latestPrices)
    {
        if (latestPrices == null)
        {
            throw new ArgumentNullException(nameof(latestPrices));
        }

        foreach (var kvp in latestPrices.OrderBy(static k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            SetMarkPrice(kvp.Key, kvp.Value);
        }
    }

    /// <inheritdoc />
    public Task<Ohlcvs> GetOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt, ResolutionLevel resolutionLevel = ResolutionLevel.Hourly)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol must not be blank.", nameof(symbol));
        }

        // 零网络：回测的行情一律来自 HistoricalDataSet。/ No network: backtest bars always come from HistoricalDataSet.
        return Task.FromResult(new Ohlcvs
        {
            Symbol = symbol,
            ResolutionLevel = resolutionLevel,
            StartDateTimeUtc = startDt,
            EndDateTimeUtc = endDt,
            OhlcvSet = new HashSet<Ohlcv>(),
        });
    }

    /// <inheritdoc />
    public Task<IEnumerable<string>> GetUsdFutureSymbolsAsync()
    {
        lock (_gate)
        {
            var symbols = _notionalUsd.Where(static k => Math.Abs(k.Value) > PositionEpsilon)
                .Select(k => k.Key).ToList();
            foreach (var k in _markPrice.Keys)
            {
                if (!symbols.Contains(k, StringComparer.OrdinalIgnoreCase))
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
                if (Math.Abs(notional) < PositionEpsilon)
                {
                    continue;
                }

                double? entry = _entryPrice.TryGetValue(kvp.Key, out var ep) ? ep : null;
                double? mark = _markPrice.TryGetValue(kvp.Key, out var mp) ? mp : null;
                var price = mark ?? entry;
                if (price is null || price <= 0d)
                {
                    continue;
                }

                var sign = notional >= 0d ? 1d : -1d;
                var basePrice = entry ?? price.Value;
                var quantity = notional / basePrice;
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
                    Leverage = 1,
                });
            }

            return Task.FromResult<IEnumerable<BinancePositionDetailsUsdt>>(result);
        }
    }

    /// <inheritdoc />
    public Task<decimal> GetusdFutureAccountBalanceAsync()
    {
        lock (_gate)
        {
            return Task.FromResult(_initialEquityUsd + _realizedPnlUsd + (decimal)ComputeUnrealizedUsd() - _commissionUsdTotal);
        }
    }

    /// <inheritdoc />
    public Task<double> GetusdFutureUnrealizedProfitRateAsync()
    {
        double totalUnrealized;
        decimal baseEquity;
        lock (_gate)
        {
            totalUnrealized = ComputeUnrealizedUsd();
            baseEquity = _initialEquityUsd + _realizedPnlUsd - _commissionUsdTotal;
        }

        if (baseEquity <= 0m)
        {
            return Task.FromResult(0d);
        }

        return Task.FromResult((double)(totalUnrealized / (double)baseEquity));
    }

    /// <summary>
    /// 延迟成交模式（NextBarOpen 语义）：开启后开/平仓只入队，<see cref="FlushPendingOrders"/> 时按
    /// 当时的标记价成交（调用方在 flush 前把下一根 bar 的开盘价设为标记价）。
    /// Deferred-fill mode (NextBarOpen semantics): when enabled, orders only enqueue and fill at
    /// FlushPendingOrders time against the current marks (the caller sets the next bar's open as the marks).
    /// </summary>
    public bool DeferFills
    {
        get { lock (_gate) { return _deferFills; } }
        set
        {
            lock (_gate)
            {
                _deferFills = value;
            }
        }
    }

    /// <summary>
    /// 按当前标记价成交全部挂起订单（NextBarOpen 填充点）。
    /// Fills all pending orders against the current marks (the NextBarOpen fill point).
    /// </summary>
    public void FlushPendingOrders()
    {
        List<Action> pending;
        lock (_gate)
        {
            pending = _pendingOrders.ToList();
            _pendingOrders.Clear();
        }

        foreach (var action in pending)
        {
            action(); // 每个动作自行持有 _gate，避免锁重入 / each action acquires _gate itself; no lock re-entrancy
        }
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
            if (_deferFills)
            {
                _pendingOrders.Add(() => LiquidateCore(symbol));
                return Task.CompletedTask;
            }
        }

        LiquidateCore(symbol);
        return Task.CompletedTask;
    }

    private void LiquidateCore(string symbol)
    {
        lock (_gate)
        {
            if (!_notionalUsd.TryGetValue(symbol, out var oldNotional) || Math.Abs(oldNotional) <= PositionEpsilon)
            {
                return; // 无持仓：无操作（与 Paper 一致）/ no position: no-op (mirrors Paper)
            }

            // 平仓方向与旧持仓相反：多头平仓=卖出，空头平仓=买入（不利方向偏移）。
            // A long liquidation sells; a short liquidation buys (adverse slippage direction).
            var actionDir = oldNotional > 0d ? -1d : 1d;
            var fill = SlippageFillFor(symbol, actionDir);
            _realizedPnlUsd += (decimal)UnrealizedOf(oldNotional, symbol, closeAt: fill);
            _notionalUsd[symbol] = 0d;

            var commission = ChargeCommission(Math.Abs(oldNotional), 0d);
            _trades.Add(new BacktestTrade
            {
                TimestampUtc = _simulatedNowUtc,
                Symbol = symbol,
                Side = oldNotional > 0d ? PositionSide.Short : PositionSide.Long, // 平仓动作方向 / the close action's side
                FillPrice = (decimal)(fill ?? 0d),
                NotionalUsd = 0m,
                CommissionUsd = commission,
            });
        }
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
            if (_deferFills)
            {
                _pendingOrders.Add(() => SetUsdFutureHoldingsCore(symbol, rate, positionSide));
                return Task.CompletedTask;
            }
        }

        SetUsdFutureHoldingsCore(symbol, rate, positionSide);
        return Task.CompletedTask;
    }

    private void SetUsdFutureHoldingsCore(string symbol, double rate, PositionSide positionSide)
    {
        lock (_gate)
        {
            // 与 Paper 同序：先按旧持仓仍打开时的权益计算目标，再平掉旧仓。
            // Same order as Paper: compute the target off equity with the old position still open, then close it out.
            var equity = (double)(_initialEquityUsd + _realizedPnlUsd + (decimal)ComputeUnrealizedUsd() - _commissionUsdTotal);
            var targetDir = positionSide == PositionSide.Short ? -1d : 1d; // Both 视为多头 / Both counts as long
            var oldNotional = _notionalUsd.TryGetValue(symbol, out var old) ? old : 0d;
            var hasOld = Math.Abs(oldNotional) > PositionEpsilon;
            var target = targetDir * rate * equity;
            var hasOpen = Math.Abs(target) > PositionEpsilon;

            // 成交价 = 标记价按动作方向做不利偏移（零滑点时=标记价，与 Paper 完全一致）。
            // 开仓时动作方向=目标方向；平至空仓时动作方向=旧仓的反方向。
            // Fill = mark offset against the action direction (equals the mark at zero slippage — exactly Paper).
            // When opening the action side is the target side; when flattening it is the old side's opposite.
            var actionDir = hasOpen ? targetDir : (oldNotional > 0d ? -1d : 1d);
            var fill = SlippageFillFor(symbol, actionDir);
            if (hasOld)
            {
                _realizedPnlUsd += (decimal)UnrealizedOf(oldNotional, symbol, closeAt: fill);
            }

            var commission = ChargeCommission(Math.Abs(target), hasOld ? Math.Abs(oldNotional) : 0d);
            if (hasOpen)
            {
                _notionalUsd[symbol] = target;
                // 与 Paper 同式：入场价取（滑点偏移后的）标记价（若已知）；否则保持既有值。
                // Mirrors Paper: entry = (slippage-adjusted) mark if known; otherwise keep the prior value.
                if (fill is not null)
                {
                    _entryPrice[symbol] = fill;
                }
            }
            else
            {
                _notionalUsd[symbol] = 0d;
            }

            if (hasOld || hasOpen)
            {
                _trades.Add(new BacktestTrade
                {
                    TimestampUtc = _simulatedNowUtc,
                    Symbol = symbol,
                    Side = hasOpen
                        ? (target > 0d ? PositionSide.Long : PositionSide.Short)      // 开仓腿方向 / open-leg side
                        : (oldNotional > 0d ? PositionSide.Short : PositionSide.Long), // 平仓动作方向 / close action side
                    FillPrice = (decimal)(fill ?? 0d),
                    NotionalUsd = (decimal)Math.Abs(target),
                    CommissionUsd = commission,
                });
            }
        }
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
            return Task.FromResult(_notionalUsd.TryGetValue(symbol, out var notional) && Math.Abs(notional) > PositionEpsilon);
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

    private double? SlippageFillFor(string symbol, double dir)
    {
        if (!_markPrice.TryGetValue(symbol, out var mark) || mark <= 0d)
        {
            return null; // 无标记价：无法成交（记录 FillPrice=0）/ no mark: cannot fill (records FillPrice = 0)
        }

        if (_slippage == 0d)
        {
            return mark;
        }

        return mark * (1d + dir * _slippage);
    }

    private decimal ChargeCommission(double openNotional, double closeNotional)
    {
        if (_commissionBps <= 0m)
        {
            return 0m;
        }

        var commission = (decimal)((openNotional + closeNotional) / 10000d) * _commissionBps;
        _commissionUsdTotal += commission;
        return commission;
    }

    private double ComputeUnrealizedUsd()
    {
        // 调用方必须已持有 _gate。/ Callers must already hold _gate.
        var total = 0d;
        foreach (var kvp in _notionalUsd)
        {
            if (Math.Abs(kvp.Value) < PositionEpsilon)
            {
                continue;
            }

            total += UnrealizedOf(kvp.Value, kvp.Key, closeAt: null);
        }

        return total;
    }

    private double UnrealizedOf(double notional, string symbol, double? closeAt)
    {
        // 与 Paper 逐式相同 / statement-identical to Paper
        if (!_entryPrice.TryGetValue(symbol, out var entry) || entry is null)
        {
            return 0d;
        }

        var e = entry.Value;
        var p = closeAt ?? (_markPrice.TryGetValue(symbol, out var mp) ? mp : e);
        if (p <= 0d || e <= 0d)
        {
            return 0d;
        }

        var baseQuantity = Math.Abs(notional) / e;
        return notional > 0d
            ? baseQuantity * (p - e)
            : baseQuantity * (e - p);
    }
}
