# Unified Runtime — Five-Minute Quick Start

> Companion to the [Unified Runtime design R0–R6](TradingRuntimeDesign.md): what `dotnet run` actually executes, where the single switch lives, and how to swap data source / strategy / credentials. Prerequisite reading: [core library guide (EN)](readme-en.md) / [中文](readme-ch.md).

---

## 1. Run it with one command

```bash
cd Quant.Infra.Net/src
dotnet run --project Quant.Infra.Net.Runtime.Console
```

By default `appsettings.json` has `"RunMode": "Backtest"`; the run is **fully offline** (synthetic demo data, in-memory accounting, zero network) and prints a performance report:

```
Backtest complete: 260 bars, 4 trades
CAGR=13.56%   Sharpe=0.54   Calmar=0.00
MaxDrawdown=0.00%   WinRate=100.0%   ProfitFactor=∞   Commission=0 USD
```

## 2. The one switch: `Runtime:RunMode`

One `Program.cs`, one `appsettings.json`, four modes — change a single value:

| `RunMode` | Driver | Data | Execution | Notes |
|---|---|---|---|---|
| `Backtest` | `BacktestRunner` bar-by-bar replay | Demo synthetic (or your source) | `BacktestBrokerService` (in-memory; flat before `WarmupBars`) | Architectural zero-look-ahead: replay only feeds `bars[i..]` |
| `Paper` | `PipelineRunner`, 8 stages once per cycle | same | `PaperBinanceUsdFutureService` (in-memory) | real-time clock; every stage event visible |
| `Testnet` | as `Paper` | real data source | **real API credentials required** | empty credentials → `NotSupportedException` at startup (by design — it never silently degrades to Paper) |
| `Live` | as `Paper` | real data source | **real API credentials required** | same fail-fast behavior |

Running `Testnet`/`Live` with the example host (credentials empty) **fails immediately** — that is the guardrail: the demo must never touch real money. To go live, write your own host and register a real broker + data source there (credentials must never be committed, see [code standards](CodeStandard.md)).

```json
{ "Runtime": { "RunMode": "Paper", "DataSource": "Demo" } }
```

## 3. What the default demo is

| Item | Default | Where |
|---|---|---|
| Data | `DemoSyntheticSourceDataService` — deterministic synthetic bars (identical numbers every run) | `Quant.Infra.Net.Runtime/DataSources/` |
| Symbol | `AAPL` (synthetic uptrend series, 260 bars; `AAA`/`BBB` are the synthetic pair legs) | `Orchestration:Parameters:Symbol` |
| Strategy | `MaCross` (`Fast=1` / `Slow=200`, weight `0.3`) | `Orchestration:Parameters:Strategy` |
| Initial equity | `10000` USD | `Orchestration:InitialEquityUsd` / `Backtest:InitialEquityUsd` |

## 4. Switch strategies (zero-code or single-file)

**The three built-ins** — one value: `MaCross` (trend), `MeanReversion` (mean reversion), `PairTradingZScore` (statistical arbitrage, uses `SymbolA`/`SymbolB`). Parameter tables: [orchestration design §9](OrchestrationLayerDesign.md#9-范例策略与端到端-demo).

**Single-file custom** — the repo ships a copy-paste example: [`Strategies/ExampleCustomStrategy.cs`](../src/Quant.Infra.Net.Runtime.Console/Strategies/ExampleCustomStrategy.cs). One `IStrategyDescriptor` + one `ISignalGenerator` in your host assembly is auto-discovered by the `StrategyCatalog`; nothing in the Runtime layer is touched:

1. copy that file into your host project's `Strategies/` folder, rename the classes;
2. set `Parameters:Strategy` to your strategy name (e.g. `"ExampleCustom"`);
3. re-run.

Your generator only depends on `IPipelineContext`; replay vs. realtime consistency is framework-guaranteed (see R4's `ParityRegressionTests`).

## 5. Swap the data source

- **`Runtime:DataSource`** (resolved by `DataSourceFactory`): `Demo` (default, offline synthetic) / `Yahoo`·`Csv` (core library `TraditionalFinanceSourceDataService`; `IHistoricalDataSourceService` defaults to CSV — swap in your own) / `Binance` (this layer's `BinanceKlineSourceDataService` read-only kline adapter) / `Custom` (**requires** passing a `customDataSource` instance to `AddQuantInfraNet` — the example host doesn't supply one, so `Custom` fail-fasts by design).
- **Your own host**: implement `ITraditionalFinanceSourceDataService` and either pass it via `AddQuantInfraNet(..., customDataSource: new MyDataSource())` (with `DataSource: "Custom"`) or register it with `services.AddSingleton<ITraditionalFinanceSourceDataService, MyDataSource>()` before calling `AddQuantInfraNet(...)`.

`Backtest` and `Paper` consume the **same** `ITraditionalFinanceSourceDataService` interface — swapping sources applies to both paths with zero strategy changes.

## 6. Configuration cheat-sheet (`appsettings.json`)

```jsonc
{
  "Runtime":  { "RunMode": "Backtest", "DataSource": "Demo", "BinanceApiKey": "", "BinanceApiSecret": "" },
  "Orchestration": {
    "InitialEquityUsd": 10000, "MaxWeightPerSymbol": 0.5, "MaxGrossExposure": 2.0,
    "KillSwitchDrawdownRate": -0.20, "MinRebalanceDelta": 0.02,
    "Parameters": { "Strategy": "MaCross", "Symbol": "AAPL", "FastPeriod": "1", "SlowPeriod": "200", "WeightPerSymbol": "0.3" }
  },
  "Backtest": { "InitialEquityUsd": 10000, "WarmupBars": 0, "CommissionBps": 0, "SlippageBps": 0, "FillTiming": "SameBarClose" }
}
```

> ⚠️ `BinanceApiKey`/`BinanceApiSecret` **must stay empty** in checked-in files (code standard #9: no credentials in the repo). Real credentials belong only in your private local config or a secrets manager inside your own host.
