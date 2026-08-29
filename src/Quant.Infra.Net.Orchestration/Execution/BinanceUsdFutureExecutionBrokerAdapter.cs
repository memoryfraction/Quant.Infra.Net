using Binance.Net.Enums;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Abstractions;

namespace Quant.Infra.Net.Orchestration.Execution;

/// <summary>
/// Adapts any <see cref="IBinanceUsdFutureService"/> (real Binance, <see cref="PaperBinanceUsdFutureService"/>,
/// or the Backtest engine's simulator — all three already implement it) to the broker-agnostic
/// <see cref="IExecutionBroker"/> the pipeline consumes. This is the default adapter registered by
/// AddQuantInfraNetOrchestration/AddQuantInfraNet; it preserves today's Binance behavior byte-for-byte.
/// </summary>
public sealed class BinanceUsdFutureExecutionBrokerAdapter : IExecutionBroker, IPaperMarkable
{
    private readonly IBinanceUsdFutureService _inner;

    /// <summary>Wraps an existing <see cref="IBinanceUsdFutureService"/> instance.</summary>
    public BinanceUsdFutureExecutionBrokerAdapter(IBinanceUsdFutureService inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    /// <inheritdoc />
    public Task<decimal> GetAccountEquityUsdAsync() => _inner.GetusdFutureAccountBalanceAsync();

    /// <inheritdoc />
    public Task<double> GetUnrealizedProfitRateAsync() => _inner.GetusdFutureUnrealizedProfitRateAsync();

    /// <inheritdoc />
    public async Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync()
    {
        var positions = await _inner.GetHoldingPositionAsync().ConfigureAwait(false);
        return positions
            .Select(p => new BrokerPosition { Symbol = p.Symbol, Quantity = p.Quantity, MarkPrice = p.MarkPrice })
            .ToList();
    }

    /// <inheritdoc />
    public Task SetTargetWeightAsync(string symbol, double signedWeight)
        => _inner.SetUsdFutureHoldingsAsync(
            symbol,
            Math.Abs(signedWeight),
            signedWeight >= 0d ? PositionSide.Long : PositionSide.Short);

    /// <inheritdoc />
    public Task LiquidateAsync(string symbol) => _inner.LiquidateUsdFutureAsync(symbol);

    /// <summary>Forwards to the inner service when it's a Paper simulator; no-ops for real Binance/Backtest.</summary>
    public void SetMarkPrices(IReadOnlyDictionary<string, double> latestPrices)
    {
        if (_inner is PaperBinanceUsdFutureService paper)
        {
            paper.SetMarkPrices(latestPrices);
        }
    }
}
