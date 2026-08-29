# FAQ (EN)

> 中文: [faq-ch.md](faq-ch.md) · [Index](README-en.md)

Common failures, what they *mean* (intended behavior vs. misconfiguration), and how to fix them.
Style mirrors [../Manual.md](../Manual.md) section 9. Every entry is verified against the current
source in this repository.

---

### Q1: "RunMode.Testnet/Live requires RuntimeOptions.BinanceApiKey/BinanceApiSecret" — why does Paper not need this but Testnet/Live do?

**Cause**: `RunMode.Testnet` and `RunMode.Live` both trade through the **real** Binance API
(`BinanceUsdFutureService`), so the container must have credentials. `RunMode.Paper` uses the
in-memory `PaperBinanceUsdFutureService` and never touches the network, so it needs none.

This is **fail-fast by design** (see `Runtime/DependencyInjection.cs`):

```csharp
var needsCredentials = runtimeOptions.RunMode is RunMode.Testnet or RunMode.Live;
if (needsCredentials
    && (string.IsNullOrWhiteSpace(runtimeOptions.BinanceApiKey)
        || string.IsNullOrWhiteSpace(runtimeOptions.BinanceApiSecret)))
{
    throw new NotSupportedException(
        $"RunMode.{runtimeOptions.RunMode} requires RuntimeOptions.BinanceApiKey/BinanceApiSecret " +
        "(fail-fast by design; this never silently degrades to Paper).");
}
```

**Key point**: the container **never silently falls back to Paper** when Live is missing credentials —
that would be a dangerous "I meant Live but it quietly ran Paper" trap. If you see this, you simply
didn't supply credentials.

**Fix**:
```csharp
services.AddQuantInfraNet(rt =>
{
    rt.RunMode = RunMode.Live;                       // or Testnet
    rt.BinanceApiKey = "your-api-key";
    rt.BinanceApiSecret = "your-api-secret";
});
```

### Q2: "Unknown Strategy 'XYZ'" — what happens if `Parameters["Strategy"]` names a strategy that doesn't exist?

**Cause**: The orchestration layer resolves the strategy by name. There are two resolution paths, and
both **fail fast** at startup (container build) rather than silently running nothing:

- **Built-in names only** (`PairTradingZScore`, `MaCross`, `MeanReversion`): resolved by the
  `ISignalGenerator` factory in `Orchestration/DependencyInjection.cs`. Any other name throws
  `ArgumentException("Unknown Strategy 'XYZ'. Supported values: PairTradingZScore | MaCross | MeanReversion.")`.
- **A name registered via an `IStrategyDescriptor`** in one of your `strategyAssemblies`: resolved by
  the `StrategyCatalog` (reflection-scans the assemblies). A name not in the catalog throws at startup.

**So an unknown strategy name = a startup crash with a clear message**, not a silent no-op. This is
intended: it catches typos and misconfigurations immediately.

**Fix**:
- Use a valid built-in name (`MaCross`, `MeanReversion`, `PairTradingZScore`), **or**
- Register your own strategy (see [writing-a-strategy-en.md](writing-a-strategy-en.md)) and pass its
  assembly via `strategyAssemblies:`, **or**
- Pass a `customSignalGenerator` instance directly.

### Q3: "DataSourceKind.Alpaca requires RuntimeOptions.AlpacaApiKey/AlpacaApiSecret" — and what about `Custom` without an instance?

**Cause**: Both `Alpaca` and `Custom` are the two data-source kinds that **require extra input** to
construct, and both **fail fast** if that input is missing (see `DataSources/DataSourceFactory.cs`):

```csharp
Models.DataSourceKind.Alpaca => !string.IsNullOrWhiteSpace(alpacaApiKey) && !string.IsNullOrWhiteSpace(alpacaApiSecret)
    ? new AlpacaTraditionalFinanceSourceDataService(alpacaApiKey!, alpacaApiSecret!)
    : throw new ArgumentException(
        "DataSourceKind.Alpaca requires RuntimeOptions.AlpacaApiKey/AlpacaApiSecret " +
        "(free tier: sign up at https://alpaca.markets — fail-fast by design, never silently falls back).",
        nameof(alpacaApiKey)),

Models.DataSourceKind.Custom => customDataSource ?? throw new ArgumentException(
    "DataSourceKind.Custom requires a custom ITraditionalFinanceSourceDataService instance " +
    "(pass it via AddQuantInfraNet / Create's customDataSource parameter).",
    nameof(customDataSource)),
```

**Key point**: the factory **never silently falls back** to another source (e.g. it won't quietly switch
to `Demo` or `Yahoo`). A misconfigured `Alpaca`/`Custom` is a startup error.

**Fix**:
- `Alpaca`: set `rt.AlpacaApiKey` and `rt.AlpacaApiSecret` (free tier at alpaca.markets).
- `Custom`: pass a `customDataSource:` instance (see [custom-data-source-en.md](custom-data-source-en.md)).

### Q4: "Is it a bug that my `customBroker` is ignored in Backtest mode?"

**No — this is intended** (see [custom-broker-en.md](custom-broker-en.md) §5). The Backtest path is a
different code path (`AddQuantInfraNetBacktest`, the D1 mechanism) that:

1. Registers a `BacktestBrokerService` **first** as `IBinanceUsdFutureService` (so the orchestration
   default steps aside), and
2. Does **not** accept a `customBroker` parameter at all.

The reason: Backtest execution must be driven by the **simulated clock / mark prices**
(`IBacktestBroker` surface: `SetMarkPrices`, `SimulatedNowUtc`, `DeferFills`, `FillTiming`
`SameBarClose`/`NextBarOpen`, plus `CommissionBps`/`SlippageBps`). A live-style `IExecutionBroker`
would bypass the bar-by-bar fill semantics and the cost/slippage model, corrupting the backtest.

So the guard in `AddQuantInfraNet`:
```csharp
if (customBroker != null && runtimeOptions.RunMode != RunMode.Backtest)
    services.AddSingleton(customBroker);
```
**means: in Backtest, your `customBroker` is dropped on purpose.** Use `BacktestOptions`
(`InitialEquityUsd`, `CommissionBps`, `SlippageBps`, `FillTiming`) to control backtest execution instead.

### Q5: Why does my risk check reject a position I expected to be allowed?

**Cause**: `DefaultRiskManager` checks **three rules in order** and rejects if **any** fails, listing
**all** reasons (see [risk-management-en.md](risk-management-en.md)):

1. Per-symbol `|TargetWeight| <= MaxWeightPerSymbol` (default **0.3**).
2. `Σ|TargetWeight| <= MaxGrossExposure` (default **1.0**).
3. Current `UnrealizedProfitRate` is **not** at-or-below `KillSwitchDrawdownRate` (default **−0.15**); if it is, it additionally recommends **full liquidation**.

Note the third rule acts on the **current snapshot** (`PortfolioSnapshot.UnrealizedProfitRate`), not on
your new target — so a deep drawdown in the *existing* book can reject a *new* target even if that
target itself is small. On a fresh restart the snapshot is zeroed, so this rule is less likely to fire
immediately (see [testing-and-deployment-en.md](testing-and-deployment-en.md) §4).

**Fix / understand**:
```csharp
services.AddQuantInfraNet(o =>
{
    o.MaxWeightPerSymbol = 0.5;      // raise the per-symbol cap
    o.MaxGrossExposure = 1.5;        // raise gross exposure (leverage)
    o.KillSwitchDrawdownRate = -0.25; // widen the kill-switch band
});
```
Or read `assessment.Reasons` (each string names the exact rule) to see which rule fired.

### Q6: "no data available for 'SYM'" appears in the event log — is that an error?

**Cause**: `SignalDataLoader.FetchAsync` records a `DataLoad` event when your data source returned an
**empty** slice for the symbol (or threw). It then returns an **empty close series**, so the strategy
degrades to "no signal" and the round completes normally. It is a **degraded-data condition**, not a
crash — the pipeline continues.

**Fix / understand**:
- Confirm your source actually has bars for that symbol in the requested window (check `DownloadOhlcvListAsync`).
- If you use `DataSourceKind.Custom`, make sure your `customDataSource` returns a non-empty `Ohlcvs` for that symbol.
- The corresponding strategy will emit no signal for that round (see the `insufficient data for '{symbol}'` event it adds).

### Q7: Why does my custom stage pipeline not run my risk/execution stages?

**Cause**: Passing `customStages:` **completely replaces** the default eight stages. If your custom
sequence doesn't include `RiskStage`, `ExecutionStage`, and `PortfolioStateStage`, they simply don't
run. This is by design (see [writing-a-strategy-en.md](writing-a-strategy-en.md) §on custom stages and
`CustomStagesPassthroughTests`).

**Fix**: include the stages you need in your sequence, e.g.
```csharp
var stages = new IPipelineStage[]
{
    new DataIngestStage(source, broker),
    new AnalysisStage(),
    new SignalStage(generator),
    new TargetPositionStage(options),
    new RiskStage(risk, hub, store),
    new ExecutionStage(model, broker),
    new PortfolioStateStage(broker, store),
    new NotificationStage(hub),
};
services.AddQuantInfraNet(..., customStages: stages);
```
(Resolve each stage's dependencies from the container, or construct them as shown in the orchestration
`DependencyInjection.cs` default.)

### Q8: Where does the trigger interval come from, and can I set it in config?

**Short answer**: it's **constructor-registered**, not config-bound. The default is
`new IntervalTrigger(StartMode.NextMinute, TimeSpan.Zero)`. To change cadence, register your own
`IntervalTrigger` before `AddQuantInfraNetOrchestration` (see
[testing-and-deployment-en.md](testing-and-deployment-en.md) §3). There is **no `appsettings.json`
key** for it.

### Q9: Does the pipeline persist anything to disk?

**No.** The default `IPortfolioStateStore` (`InMemoryPortfolioStateStore`) is purely in-memory
(overwrite semantics), and there is no built-in persistent store. On restart it's empty. Positions
are re-derived from the broker every round. If you need durable state, implement `IPortfolioStateStore`
yourself (see [testing-and-deployment-en.md](testing-and-deployment-en.md) §4).

### Q10: I want to use Interactive Brokers / Charles Schwab — is it supported?

**Not yet / out of scope** (see [custom-broker-en.md](custom-broker-en.md) §6):
- **Interactive Brokers**: `InteractiveBrokersService` is currently an **empty shell** (every method
  throws `NotImplementedException`). The InterReact TWS protocol client is embedded in the repo
  (`src/Quant.Infra.Net/Broker/InterReact/`) but nothing connects it yet. So IB is *in progress*.
- **Charles Schwab**: a pipeline *execution* adapter is deliberately left to the **Quant.Infra.Net.Pro**
  repository — out of scope for this open-source surface.

For now, the only fully wired execution path is **Binance USD-M futures** (real / Paper / Backtest).


