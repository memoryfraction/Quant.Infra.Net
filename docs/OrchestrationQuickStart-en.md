# Orchestration Layer — Quick Start Guide

> Companion to [Orchestration Layer Design](OrchestrationLayerDesign.md) (full contract) and the [English](readme-en.md) / [中文](readme-ch.md) README's "Orchestration Layer (Beta)" section. This document answers the questions that section leaves open: what exactly runs when you `dotnet run` the demo, and how to point the pipeline at your own data, symbols, and strategy.
>
> ⚠️ **R6 convergence**: the demo below now runs inside the unified host [`Quant.Infra.Net.Runtime.Console`](../src/Quant.Infra.Net.Runtime.Console/) (the old standalone demo hosts are retired) — set `"Runtime": { "RunMode": "Paper" }` in that host's `appsettings.json` first (`Testnet`/`Live` require real credentials, see §5).

---

## 1. What exactly happens when you run the demo?

```bash
git clone https://github.com/memoryfraction/Quant.Infra.Net.git
cd Quant.Infra.Net/src
# set appsettings.json first:  "Runtime" → "RunMode": "Paper"
dotnet run --project Quant.Infra.Net.Runtime.Console
```

This does **not** touch the network, a real broker, or a real market data feed. Concretely:

| Question | Answer |
|---|---|
| **Data source** | `DemoSyntheticSourceDataService` — a demo-only deterministic synthetic implementation in [`Quant.Infra.Net.Runtime`](../src/Quant.Infra.Net.Runtime/DataSources/DemoSyntheticSourceDataService.cs) (instantiated by the `DataSourceFactory` when `Runtime:DataSource = "Demo"`; the old per-host `DemoTraditionalFinanceSourceDataService` was folded into this one in R6). It generates a deterministic synthetic price series (same numbers every run); it never calls Yahoo Finance, Binance, or any external API. |
| **Symbol** | A single symbol, `AAPL` — but the price series is **synthetic** (a steady uptrend with mild noise), not real Apple stock data. The ticker name is just a familiar placeholder. |
| **Strategy** | `MaCross` (classic 200-day moving average trend following), configured in the unified host's [`appsettings.json`](../src/Quant.Infra.Net.Runtime.Console/appsettings.json) under the `Orchestration` section. This is deliberately the **single-symbol** default (not the two-symbol `PairTradingZScore`) — one symbol means one signal, one target position, one execution report, so a first-time reader can verify the whole run by eye without cross-referencing two series. |
| **Broker / execution** | `PaperBinanceUsdFutureService` — pure in-memory paper trading, zero network requests. Registered automatically because `appsettings.json` sets `"Environment": "Paper"`. |
| **Notifications** | Disabled by default (`"Notifications": { "Enabled": false }`) so the demo never needs DingTalk/WeChat Work/SMTP credentials. |

One cycle's data flow:

```
DemoSyntheticSourceDataService (260 synthetic daily bars for "AAPL")
        │
        ▼
DataIngestStage  → loads and caches the OHLCV series in the pipeline context
        ▼
AnalysisStage    → (no-op for MaCross; MaCross computes its own SMA internally)
        ▼
SignalStage      → MaCrossSignalGenerator: close ≥ SMA(200) → Long
        ▼
TargetPositionStage → AAPL target weight = +0.30 (WeightPerSymbol, capped by MaxWeightPerSymbol)
        ▼
RiskStage        → checks per-symbol weight / gross exposure / kill-switch → passes
        ▼
ExecutionStage   → PaperBinanceUsdFutureService opens an in-memory long position
        ▼
PortfolioStateStage → snapshot: equity=$10,000, 1 open position
        ▼
NotificationStage → would publish an Info summary if Notifications.Enabled=true
```

Expected console output (abbreviated):

```
[Signal] generator=MaCross: AAPL=Long (0.57)
[TargetPosition] targets: AAPL=0.30
[Risk] risk check passed
[Execution] execution done: 1/1 ok
[PortfolioState] snapshot saved: equity=10000.00 positions=1
```

---

## 2. Switching to a real data source

The demo swaps in `DemoSyntheticSourceDataService` purely so the demo runs offline and produces the same numbers every time. To use real market data, replace that one DI registration in your own `Program.cs`.

### Option A — use the core library's real data source

`Quant.Infra.Net` (the core package, not the orchestration layer) already ships `TraditionalFinanceSourceDataService`, which pulls from Yahoo Finance via `pythonnet` (see the root [README Quick Start](../README.md) for the `pythonnet` package and Python path configuration it requires). It depends on `IHistoricalDataSourceService`; register the CSV-backed implementation directly, or use MySQL/MongoDB — see [`Quant.Infra.Net/SourceData/Service/Historical/`](../src/Quant.Infra.Net/SourceData/Service/Historical/) for the available implementations:

```csharp
using Quant.Infra.Net.SourceData.Service;
using Quant.Infra.Net.SourceData.Service.Historical;

builder.Services.AddSingleton<IHistoricalDataSourceService, HistoricalDataSourceServiceCsv>();
builder.Services.AddSingleton<ITraditionalFinanceSourceDataService, TraditionalFinanceSourceDataService>();
// In a custom host, simply don't register the demo synthetic source (i.e. don't set Runtime:DataSource = "Demo").
builder.Services.AddQuantInfraNetOrchestration();
```

With `"DataSource": "yahoo"` in `appsettings.json` (already the default), the pipeline will now fetch real historical bars instead of synthetic ones. **Every stage downstream of `DataIngestStage`/the signal generator is unaffected** — orchestration reads a plain `Ohlcvs` series, it never cares where the bars came from.

### Option B — write your own data source

Implement `ITraditionalFinanceSourceDataService` yourself (e.g., wrapping an internal tick database, a broker's REST API, or a CSV export pipeline) and register it the same way:

```csharp
public sealed class MyDataService : ITraditionalFinanceSourceDataService
{
    public Task<Ohlcvs> DownloadOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt,
        ResolutionLevel period = ResolutionLevel.Daily, DataSource dataSource = DataSource.YahooFinance)
    {
        // fetch/convert your bars into an Ohlcvs (HashSet<Ohlcv>) here
    }
    // ... the other four interface members (see ITraditionalFinanceSourceDataService)
}
```

```csharp
builder.Services.AddSingleton<ITraditionalFinanceSourceDataService, MyDataService>();
builder.Services.AddQuantInfraNetOrchestration();
```

No orchestration-layer code needs to change — `DataIngestStage` and every built-in signal generator only depend on the interface.

---

## 3. Changing the symbol(s)

Pure configuration — edit `appsettings.json`, no code change, no rebuild logic needed:

```json
{
  "Orchestration": {
    "Parameters": {
      "Strategy": "MaCross",
      "Symbol": "MSFT",
      "SlowPeriod": "200"
    }
  }
}
```

For the two-symbol `PairTradingZScore` strategy, set `SymbolA`/`SymbolB` instead of `Symbol`:

```json
{
  "Orchestration": {
    "Parameters": {
      "Strategy": "PairTradingZScore",
      "SymbolA": "BTCUSDT",
      "SymbolB": "ETHUSDT",
      "DataSource": "binance",
      "LookbackBars": "240",
      "MinCorrelation": "0.7"
    }
  }
}
```

(Using `DemoSyntheticSourceDataService` (`Runtime:DataSource = "Demo"`), only `"AAA"`/`"BBB"`/anything-else-defaulting-to-an-uptrend produce meaningful synthetic signals — see [`DemoSyntheticSourceDataService.cs`](../src/Quant.Infra.Net.Runtime/DataSources/DemoSyntheticSourceDataService.cs). Once you switch to a real data source (§2), any real symbol works.)

---

## 4. Customizing or replacing the strategy

### Option A — switch between the 3 built-in strategies

Change one value, no code:

| `Parameters.Strategy` | Style | Key parameters |
|---|---|---|
| `MaCross` (demo default) | Trend following | `Symbol`, `FastPeriod`, `SlowPeriod`, `AllowShort` |
| `MeanReversion` | Mean reversion | `Symbol`, `LookbackBars`, `EntryZ`, `ExitZ`, `AllowShort` |
| `PairTradingZScore` | Statistical arbitrage | `SymbolA`, `SymbolB`, `LookbackBars`, `ZScoreEntryThreshold`, `ZScoreExitThreshold`, `MinCorrelation`, `UseAdfFilter` |

Full parameter contract: [Orchestration Layer Design §9](OrchestrationLayerDesign.md#9-范例策略与端到端-demo).

### Option B — plug in your own signal generator

Implement `ISignalGenerator` (optionally extend `BaseSignalGenerator` for the built-in data-loading/parameter-parsing helpers) and pass it to `AddQuantInfraNetOrchestration` — it fully replaces the strategy-name lookup:

```csharp
public sealed class MyStrategy : ISignalGenerator
{
    public string Id => "MyStrategy";
    public Task<IReadOnlyList<Signal>> GenerateSignalsAsync(IPipelineContext context, CancellationToken ct)
    {
        // your signal logic; return Signal objects (Symbol, Direction, Strength, Reason)
    }
}
```

```csharp
builder.Services.AddQuantInfraNetOrchestration(customSignalGenerator: new MyStrategy());
```

### Option C — replace an entire stage, or the whole pipeline

`AddQuantInfraNetOrchestration` also accepts `customStages` (replaces the default 8-stage pipeline entirely) and `customExecutionModel` (replaces `RebalanceExecutionModel`). Use this when the 8-stage shape itself doesn't fit your strategy (e.g., you want a custom risk stage, or you skip notification entirely):

```csharp
builder.Services.AddQuantInfraNetOrchestration(
    customStages: new IPipelineStage[] { new MyDataStage(), new MySignalStage(), new MyExecutionStage() });
```

---

## 5. Going to Testnet or Live (do this deliberately)

Nothing above ever touches a real exchange — `Environment` defaults to `Paper`, which is a pure in-memory broker. To go live you must do **both** of the following explicitly; `AddQuantInfraNetOrchestration()` will throw `NotSupportedException` at startup if you set a non-Paper environment without registering a real broker first:

```json
{ "Orchestration": { "Environment": "Testnet" } }
```

```csharp
// Register a real broker BEFORE calling AddQuantInfraNetOrchestration() — it only auto-registers the Paper broker.
builder.Services.AddSingleton<IBinanceUsdFutureService>(sp => new BinanceUsdFutureService(/* real API key/secret, Testnet or Live */));
builder.Services.AddQuantInfraNetOrchestration();
```

Read the risk-management defaults in [Orchestration Layer Design §5.7](OrchestrationLayerDesign.md#57-事件与配置) before doing this — the demo's `appsettings.json` intentionally uses relaxed risk limits (`MaxWeightPerSymbol: 0.5`, etc.) that are appropriate for a single-symbol offline demo, not necessarily for real capital.
