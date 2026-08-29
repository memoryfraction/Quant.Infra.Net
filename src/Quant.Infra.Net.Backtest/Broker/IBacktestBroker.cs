namespace Quant.Infra.Net.Backtest.Broker;

/// <summary>
/// 回测经纪商的仿真控制面：标记价推进、模拟时钟、延迟成交、权益/成交读取——这些成员不属于
/// <see cref="Quant.Infra.Net.Broker.Interfaces.IBinanceUsdFutureService"/>（该接口只表达交易语义，
/// 不涉及"谁在驱动时间/价格前进"）。<see cref="BacktestRunner"/> 依赖本接口而非具体实现类（DIP），
/// 以便未来替换撮合引擎实现时不必改动 BacktestRunner。
/// Backtest broker's simulation-control surface: mark-price advancement, simulated clock, deferred
/// fills, equity/trade reads — none of this belongs on <see cref="Quant.Infra.Net.Broker.Interfaces.IBinanceUsdFutureService"/>
/// (which only expresses trading semantics, not "who drives time/price forward"). <see cref="Quant.Infra.Net.Backtest.Runner.BacktestRunner"/>
/// depends on this abstraction rather than the concrete implementation (DIP), so a future matching-engine
/// swap needs no change to BacktestRunner.
/// </summary>
public interface IBacktestBroker
{
    /// <summary>登记某 symbol 的最新值（标记价），用于估值、未实现盈亏与成交偏移基准 / Registers a symbol's latest price (mark) used for valuation, unrealized P/L, and the slippage base.</summary>
    void SetMarkPrice(string symbol, double price);

    /// <summary>批量登记最新值（键不得为空白）/ Registers several (symbol, price) pairs (keys must not be blank).</summary>
    void SetMarkPrices(IReadOnlyDictionary<string, double> latestPrices);

    /// <summary>模拟当前时刻（用于给成交记录打时间戳；BacktestRunner 每根 bar 前设置）/ The simulated now instant (stamps trade records; BacktestRunner sets it before each bar).</summary>
    DateTime SimulatedNowUtc { get; set; }

    /// <summary>延迟成交模式（NextBarOpen 语义）：开启后开/平仓只入队 / Deferred-fill mode (NextBarOpen semantics): when enabled, orders only enqueue.</summary>
    bool DeferFills { get; set; }

    /// <summary>按当前标记价成交全部挂起订单（NextBarOpen 填充点）/ Fills all pending orders against the current marks (the NextBarOpen fill point).</summary>
    void FlushPendingOrders();

    /// <summary>当前权益（USD）= 初始权益 + 已实现盈亏 + 未实现盈亏 − 累计手续费 / Current equity (USD) = initial + realized + unrealized − accumulated commission.</summary>
    decimal CurrentEquityUsd { get; }

    /// <summary>只读成交记录快照（按成交顺序追加）/ Read-only snapshot of the trade log (append-ordered).</summary>
    IReadOnlyList<Quant.Infra.Net.Backtest.Models.BacktestTrade> Trades { get; }
}
