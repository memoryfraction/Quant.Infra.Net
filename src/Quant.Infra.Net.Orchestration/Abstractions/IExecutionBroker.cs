namespace Quant.Infra.Net.Orchestration.Abstractions;

/// <summary>
/// Broker-agnostic execution surface consumed by <see cref="Execution.RebalanceExecutionModel"/> and the
/// Execution/PortfolioState stages — the pipeline never depends on a specific broker SDK type (Binance, IB,
/// Schwab, ...). Concrete brokers are plugged in via a thin adapter that wraps the broker's native client;
/// see <see cref="Execution.BinanceUsdFutureExecutionBrokerAdapter"/> for the reference implementation.
/// </summary>
/// <remarks>
/// Weights are signed (positive = long, negative = short); brokers without a short-selling concept (most cash
/// equity accounts) can simply reject or clamp negative weights in their own adapter.
/// </remarks>
public interface IExecutionBroker
{
    /// <summary>Current account equity in USD.</summary>
    Task<decimal> GetAccountEquityUsdAsync();

    /// <summary>Current unrealized-PnL rate (unrealized PnL / equity).</summary>
    Task<double> GetUnrealizedProfitRateAsync();

    /// <summary>Current open positions (empty when flat).</summary>
    Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync();

    /// <summary>Sets the target weight for a symbol (signed; positive = long, negative = short).</summary>
    Task SetTargetWeightAsync(string symbol, double signedWeight);

    /// <summary>Closes any open position for a symbol.</summary>
    Task LiquidateAsync(string symbol);
}

/// <summary>Broker-agnostic snapshot of one open position.</summary>
public sealed class BrokerPosition
{
    /// <summary>Trading symbol.</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Signed quantity (positive = long, negative = short).</summary>
    public decimal Quantity { get; init; }

    /// <summary>Current mark price.</summary>
    public decimal MarkPrice { get; init; }
}

/// <summary>
/// Optional capability: a broker that accepts externally-driven mark prices (Paper/Backtest simulators).
/// Live brokers simply don't implement this; <see cref="Stages.StageMarketData.ApplyPaperMarks"/> no-ops for them.
/// </summary>
public interface IPaperMarkable
{
    /// <summary>Registers the latest known price for each symbol (used for valuation/unrealized PnL).</summary>
    void SetMarkPrices(IReadOnlyDictionary<string, double> latestPrices);
}
