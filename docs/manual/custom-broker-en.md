# Custom Broker Execution (EN)

> 中文: [custom-broker-ch.md](custom-broker-ch.md) · [Index](README-en.md)

The execution surface of the pipeline is **one interface** — `IExecutionBroker`
(`src/Quant.Infra.Net.Orchestration/Abstractions/IExecutionBroker.cs`). The pipeline
(`RebalanceExecutionModel`, `ExecutionStage`, `PortfolioStateStage`) only ever sees this
interface, never a broker SDK type. To plug in **your own broker**, you write a thin adapter
implementing the interface and pass the instance to the `customBroker` parameter of
`AddQuantInfraNet(...)`. No fork, no pipeline changes.

---

## 1. Why this abstraction exists

Before the `IExecutionBroker` split (commit `runtime(broker): decouple Orchestration pipeline
from Binance via IExecutionBroker`), every execution-facing stage in
`Quant.Infra.Net.Orchestration` depended directly on the Binance futures service type
(`IBinanceUsdFutureService`). The practical consequence: the unified `RunMode` switch
(Backtest / Paper / Testnet / Live) only ever worked for Binance USD-M futures, because that
was the only broker with a registered implementation.

The interface is the single seam that lets other brokers (Interactive Brokers, Charles Schwab,
any in-house gateway) hang off the same `RunMode` switch: **implement `IExecutionBroker`,
pass it in, nothing else changes.**

## 2. The interface, method by method

```csharp
public interface IExecutionBroker
{
    Task<decimal> GetAccountEquityUsdAsync();
    Task<double> GetUnrealizedProfitRateAsync();
    Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync();
    Task SetTargetWeightAsync(string symbol, double signedWeight);
    Task LiquidateAsync(string symbol);
}

public sealed class BrokerPosition
{
    public string Symbol { get; init; }
    public decimal Quantity { get; init; }   // signed: positive = long, negative = short
    public decimal MarkPrice { get; init; }
}
```

| Member | Used by | Notes |
|--------|---------|-------|
| `GetAccountEquityUsdAsync` | `RebalanceExecutionModel` (weight = notional / equity), `PortfolioStateStage` (snapshot), `RiskStage` (via snapshot) | Return current account equity in **USD**. |
| `GetUnrealizedProfitRateAsync` | `PortfolioStateStage` (snapshot → kill-switch input) | Unrealized PnL / equity (e.g. `-0.02` = −2%). |
| `GetPositionsAsync` | `RebalanceExecutionModel` (actual weight), `PortfolioStateStage` (actual weight table) | `Quantity` is **signed** (positive long / negative short); `MarkPrice` is the current mark. |
| `SetTargetWeightAsync(symbol, signedWeight)` | `RebalanceExecutionModel` | **Signed** target weight: positive = long, negative = short. The weight is relative to `GetAccountEquityUsdAsync` (the model computes actual weight as `Quantity * MarkPrice / equity`). |
| `LiquidateAsync(symbol)` | `RebalanceExecutionModel` (when `|TargetWeight|` falls below `1e-9`) | Close the symbol's open position entirely. |

### Brokers without a short-selling concept

Most cash-equity accounts cannot short. The interface contract (see the XML docs on
`IExecutionBroker`) says explicitly: *brokers without a short-selling concept (most cash
equity accounts) can simply reject or clamp negative weights in their own adapter*. Pick one:

- **Reject**: `throw new NotSupportedException($"shorting {symbol} is not supported")` — the
  pipeline records it as a failed execution report for that symbol and continues (see
  `RebalanceExecutionModel`: per-symbol try/catch).
- **Clamp**: treat a negative weight as `0` (a long-only rebalance to flat/long).

### Optional capability: `IPaperMarkable`

```csharp
public interface IPaperMarkable
{
    void SetMarkPrices(IReadOnlyDictionary<string, double> latestPrices);
}
```

This is **optional**. `ExecutionStage` and `PortfolioStateStage` both call
`StageMarketData.ApplyPaperMarks(context, broker)` before trading/valuation, which does
`if (broker is not IPaperMarkable) return;` — live brokers simply don't implement it and the
call no-ops. Its purpose: let a *simulated* broker (Paper simulator) receive the pipeline's
latest closes so its valuation and PnL are marked to current prices rather than stale ones.

## 3. Walkthrough of the reference implementation

`BinanceUsdFutureExecutionBrokerAdapter`
(`src/Quant.Infra.Net.Orchestration/Execution/BinanceUsdFutureExecutionBrokerAdapter.cs`)
wraps any existing `IBinanceUsdFutureService` — real Binance, `PaperBinanceUsdFutureService`,
or the Backtest engine's `BacktestBrokerService` (all three already implement it):

```csharp
public sealed class BinanceUsdFutureExecutionBrokerAdapter : IExecutionBroker, IPaperMarkable
{
    private readonly IBinanceUsdFutureService _inner;

    public BinanceUsdFutureExecutionBrokerAdapter(IBinanceUsdFutureService inner) { ... }

    public Task<decimal> GetAccountEquityUsdAsync() => _inner.GetusdFutureAccountBalanceAsync();

    public Task<double> GetUnrealizedProfitRateAsync() => _inner.GetusdFutureUnrealizedProfitRateAsync();

    public async Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync()
    {
        var positions = await _inner.GetHoldingPositionAsync().ConfigureAwait(false);
        return positions
            .Select(p => new BrokerPosition { Symbol = p.Symbol, Quantity = p.Quantity, MarkPrice = p.MarkPrice })
            .ToList();
    }

    public Task SetTargetWeightAsync(string symbol, double signedWeight)
        => _inner.SetUsdFutureHoldingsAsync(
            symbol,
            Math.Abs(signedWeight),
            signedWeight >= 0d ? PositionSide.Long : PositionSide.Short);

    public Task LiquidateAsync(string symbol) => _inner.LiquidateUsdFutureAsync(symbol);

    public void SetMarkPrices(IReadOnlyDictionary<string, double> latestPrices)
    {
        if (_inner is PaperBinanceUsdFutureService paper)
        {
            paper.SetMarkPrices(latestPrices);
        }
    }
}
```

Two things to notice:

1. **Negative-weight handling** is delegated to the wrapped service: the adapter passes
   `Math.Abs(weight)` plus a `PositionSide.Long` / `PositionSide.Short` flag. A cash-equity
   adapter that cannot short would instead throw/clamp inside `SetTargetWeightAsync` itself.
2. **`IPaperMarkable` is forwarded conditionally** — only when the inner service is the Paper
   simulator. For real Binance or the Backtest simulator the method silently no-ops, which is
   exactly the "live brokers don't implement it" behavior above.

## 4. A minimal adapter you can copy-paste (in-memory fake broker)

You do **not** need a real broker API to try this — an in-memory broker is enough to prove the
pipeline runs against your adapter:

```csharp
using Quant.Infra.Net.Orchestration.Abstractions;

public sealed class FakeCashEquityBroker : IExecutionBroker
{
    private readonly object _gate = new();
    private decimal _equityUsd = 100_000m;
    private readonly Dictionary<string, decimal> _notionalUsd = new(StringComparer.OrdinalIgnoreCase); // signed
    private readonly Dictionary<string, decimal> _mark = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Symbol, double Weight, DateTime At)> _calls = new();

    public Task<decimal> GetAccountEquityUsdAsync()
        => Task.FromResult(_equityUsd);

    public Task<double> GetUnrealizedProfitRateAsync()
        => Task.FromResult(0d); // a real adapter returns unrealized PnL / equity

    public Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync()
    {
        var list = _notionalUsd
            .Where(kv => kv.Value != 0m)
            .Select(kv => new BrokerPosition
            {
                Symbol = kv.Key,
                Quantity = kv.Value,   // a real adapter returns shares; here we keep signed notional as a stand-in
                MarkPrice = _mark.TryGetValue(kv.Key, out var mp) ? mp : 1m,
            }).ToList();
        return Task.FromResult<IReadOnlyList<BrokerPosition>>(list);
    }

    public Task SetTargetWeightAsync(string symbol, double signedWeight)
    {
        if (signedWeight < 0d)
        {
            throw new NotSupportedException(
                $"{symbol}: shorting is not supported by this cash-equity adapter");
        }
        // weight -> shares when a mark is known; otherwise keep signed notional as a stand-in
        _notionalUsd[symbol] = (decimal)(signedWeight * (double)_equityUsd);
        _calls.Add((symbol, signedWeight, DateTime.UtcNow));
        return Task.CompletedTask;
    }

    public Task LiquidateAsync(string symbol)
    {
        _notionalUsd[symbol] = 0m;
        _calls.Add((symbol, 0d, DateTime.UtcNow));
        return Task.CompletedTask;
    }

    // Optional: adopt the pipeline's latest closes as marks (like the Paper simulator does).
    public sealed class WithMarks : FakeCashEquityBroker, IPaperMarkable
    {
        public void SetMarkPrices(IReadOnlyDictionary<string, double> latestPrices)
        {
            foreach (var kv in latestPrices) _mark[kv.Key] = (decimal)kv.Value;
        }
    }
}
```

> Note: a real adapter would fill `GetUnrealizedProfitRateAsync` from the broker's own account
> data and track `MarkPrice` from the broker's position feed. The fake above keeps it simple so
> it runs offline.

## 5. Wiring it in

Verified against `src/Quant.Infra.Net.Runtime/DependencyInjection.cs` (the `AddQuantInfraNet`
signature and the `customBroker` registration block):

```csharp
var broker = new FakeCashEquityBroker(); // or your real adapter

services.AddQuantInfraNet(
    rt => rt.RunMode = RunMode.Paper,           // or Testnet / Live
    o  => { o.Parameters["Strategy"] = "MaCross"; o.Parameters["Symbol"] = "AAPL"; },
    b  => { /* backtest options — not read in Paper */ },
    customDataSource: null,                     // (or your data source; see the other guide)
    customBroker: broker);                      // ← your broker
```

How registration resolves (source: `Runtime/DependencyInjection.cs` + `Orchestration/DependencyInjection.cs`):

- `RunMode != Backtest` **and** `customBroker != null` → `services.AddSingleton(customBroker)`
  (your instance wins).
- The orchestration layer then does `services.TryAddSingleton<IExecutionBroker>(sp => new
  BinanceUsdFutureExecutionBrokerAdapter(sp.GetRequiredService<IBinanceUsdFutureService>()))` —
  `TryAdd` means it **steps aside** because you already registered one.
- The pipeline stages consume `IExecutionBroker` from the container, so they all see your
  instance.

### Why `customBroker` is ignored in Backtest mode

This is **intended, not a bug** (FAQ entry in
[faq-en.md](faq-en.md)). The Backtest path (D1 mechanism, see
[Backtest layer design](../OrchestrationLayerDesign.md)) registers a `BacktestBrokerService`
*first* as `IBinanceUsdFutureService`, and `AddQuantInfraNetBacktest` is a different method that
does **not** accept a `customBroker` parameter at all. In Backtest the execution must be driven
by the simulated clock / mark prices (`IBacktestBroker` surface: `SetMarkPrices`,
`SimulatedNowUtc`, `DeferFills`, `FlushPendingOrders`) — a live-style `IExecutionBroker` would
bypass the bar-by-bar fill semantics and cost/slippage model. The `AddQuantInfraNet` guard
`if (customBroker != null && runtimeOptions.RunMode != RunMode.Backtest)` simply enforces this:
**Backtest always uses `BacktestBrokerService`.**

## 6. Current state of the other brokers (as of this commit)

| Broker | Status |
|--------|--------|
| **Binance USD-M futures** | ✅ Fully wired end-to-end: real API service (`BinanceUsdFutureService`), Paper simulator (`PaperBinanceUsdFutureService`), and the Backtest simulator (`BacktestBrokerService`) all implement `IBinanceUsdFutureService` and thus hang off the same adapter. |
| **Interactive Brokers** | ⚠️ **Not yet connected.** `InteractiveBrokersService`
  (`src/Quant.Infra.Net/Broker/Service/InteractiveBrokersService.cs`) is an **empty shell** —
  every public method currently `throw new NotImplementedException()`. The full InterReact IB TWS
  protocol client is embedded in the repo under
  `src/Quant.Infra.Net/Broker/InterReact/` (real, working code), but **nothing connects it to the
  service yet**. So "IB" is *in progress*, not *supported*. A working IB adapter for this pipeline
  would implement `IExecutionBroker` on top of it and be passed as `customBroker`. |
| **Charles Schwab** | 🚫 **Out of scope for this repo.** The `SchwabBrokerService`
  (`src/Quant.Infra.Net/Broker/Service/SchwabBrokerService.cs`) in the core library is a separate
  read/market-data + order surface; a *pipeline execution* adapter for Schwab is deliberately left
  to the **Quant.Infra.Net.Pro** repository and is not part of this open-source surface. |

## 7. Where to go next

- [writing-a-strategy-en.md](writing-a-strategy-en.md) — the strategy that will drive your broker.
- [risk-management-en.md](risk-management-en.md) — the pre-trade checks your broker will see.
- [testing-and-deployment-en.md](testing-and-deployment-en.md) — unit-testing your adapter against
  a fake pipeline.
- [faq-en.md](faq-en.md) — why `customBroker` is ignored in Backtest, and more.
