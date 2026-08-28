# Backtest Engine — Quick Start Guide

> Companion to [Trading Runtime Design](TradingRuntimeDesign.md) (full contract, section 7 & 9) and the [English](readme-en.md) / [中文](readme-ch.md) README's "Backtest Engine (Beta)" section. This document answers the questions that section leaves open: what exactly runs when you `dotnet run` the demo, how to feed it real historical data, how costs and fill timing work, and how to sweep parameters.

---

## 1. What exactly happens when you run the demo?

```bash
git clone https://github.com/memoryfraction/Quant.Infra.Net.git
cd Quant.Infra.Net
dotnet run --project src/Quant.Infra.Net.Backtest.Console
```

This does **not** touch the network, a real broker, or any market data feed. Concretely:

| Question | Answer |
|---|---|
| **Data source** | Synthetic daily bars built **inside** [`Program.cs`](../src/Quant.Infra.Net.Backtest.Console/Program.cs) — a deterministic uptrend with a sinusoidal wobble (same numbers every run). No Yahoo/Binance/CSV at all. |
| **Symbol** | A single symbol, `AAPL` — a familiar placeholder for the synthetic series. |
| **Strategy** | `MaCross` with `FastPeriod=5`, `SlowPeriod=20` — the classic 2-period moving-average cross, set fast/slow so signals appear within 120 bars. |
| **Broker / execution** | `BacktestBrokerService` — a backtest-only `IBinanceUsdFutureService` accounting implementation. Pure in-memory; **the same accounting model as the Paper broker**, plus commission/slippage tracking and an append-only trade log. |
| **Network** | Zero. Every bar is replayed through the real 8-stage `StrategyPipeline` (`DataIngest → Analysis → Signal → TargetPosition → Risk → Execution → PortfolioState → Notification`), but the data-ingest stage only ever reads from the in-memory `HistoricalDataSet`. |
| **Metrics** | `BacktestResult.Metrics` — CAGR / Sharpe / Calmar / Max drawdown (via the existing `StrategyPerformanceAnalyzer`) plus trade-level win rate / profit factor / total commission (via `TradeStatistics`). |

Expected console output (abridged, deterministic):

```
回测完成 / Backtest complete: 100 bars, 8 trades
CAGR=16.56%   Sharpe=0.55   Calmar=45.19
MaxDrawdown=-0.37%（9 天 / days）
WinRate=80.0%   ProfitFactor=23.12   Commission=0 USD
```

> 120 input bars − 20 `WarmupBars` = 100 bars of trading. The demo prints all nine metrics from one `BacktestResult`.

---

## 2. Feeding real historical data

The demo builds `HistoricalDataSet` from an in-process `Ohlcv` list purely so it runs offline. To use real data, build the same class from your bars — everything downstream (pipeline, broker, metrics) is unchanged:

```csharp
using Quant.Infra.Net.Backtest;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.SourceData.Model;

// from your own source: a CSV loader, the core library's ITraditionalFinanceSourceDataService, DB, etc.
// (fetch ONCE, up-front — never inside the backtest loop)
var bars = await MyDataLoader.LoadDailyAsync("AAPL", from, to);   // → IReadOnlyList<Ohlcv>

var data = new HistoricalDataSet(new Dictionary<string, IReadOnlyList<Ohlcv>>
{
    ["AAPL"] = bars,
});
```

Key points:

- **No pre-sorting needed** — `HistoricalDataSet` sorts each series internally and builds the union `Timeline` (ascending).
- **Look-ahead bias is structurally blocked**: the runner marks each bar's close into the broker, then invokes the pipeline for that bar only; the signal generator's cached series (`SliceUpTo`) contains bars **up to and including** the current bar — never the future. `LookAheadBiasTests` (B1) pins this down.
- **Multi-symbol** (e.g., `PairTradingZScore`): include one series per symbol; the runner walks the union timeline, and symbols with data gaps simply get no mark/fill on that bar.

Run it with your own host:

```csharp
var services = new ServiceCollection();
services.AddQuantInfraNetBacktest(
    configureBacktest: b => { b.InitialEquityUsd = 10000m; b.WarmupBars = 20; },
    configureOrchestration: o =>
    {
        o.Parameters["Symbol"] = "AAPL";
        o.Parameters["Strategy"] = "MaCross";
    });

using var provider = services.BuildServiceProvider();
var result = await provider.GetRequiredService<BacktestRunner>()
    .RunAsync(data, new[] { "AAPL" });
```

---

## 3. Costs, slippage, and fill timing (`BacktestOptions`)

All five options are plain configuration, set via the `configureBacktest` callback:

| Option | Default | Meaning |
|---|---|---|
| `InitialEquityUsd` | `10000` | Starting account equity (USD). |
| `WarmupBars` | `0` | First N bars: no trading (indicator warm-up only; the equity curve still records them). |
| `CommissionBps` | `0` | Commission in basis points, deducted on each traded notional (recorded in `Trades[].CommissionUsd` and summed into `Metrics.TotalCommissionUsd`). |
| `SlippageBps` | `0` | Fill price offset from mark against the trade direction (buy = higher, sell = lower). |
| `FillTiming` | `SameBarClose` | `SameBarClose`: fill at the signal bar's close (default). `NextBarOpen`: fill at the **next** bar's open — the causally-honest mode for signals computed from that bar's close. |

```csharp
services.AddQuantInfraNetBacktest(
    configureBacktest: b =>
    {
        b.InitialEquityUsd = 50000m;
        b.CommissionBps = 4m;      // 0.04 %
        b.SlippageBps = 2m;        // 0.02 % adverse
        b.FillTiming = Quant.Infra.Net.Backtest.Models.FillTiming.NextBarOpen;
    },
    o => { ... });
```

With `CommissionBps = SlippageBps = FillTiming = SameBarClose` (all defaults), the accounting is numerically identical to the orchestration layer's Paper broker — that cross-check is pinned by `BacktestBrokerServiceTests` (zero-cost parity case, B2).

---

## 4. Switching or replacing the strategy

Identical to the orchestration layer's extension surface — the backtest drives the **same** `StrategyPipeline`:

- **Built-ins**: set `o.Parameters["Strategy"]` to `"MaCross"`, `"MeanReversion"`, or `"PairTradingZScore"` (all three are covered end-to-end by `B5EndToEndTests`).
- **Your own signal generator**: pass `customSignalGenerator` — it fully replaces the strategy-name lookup, and the *same class* then works for backtest and Paper:

```csharp
services.AddQuantInfraNetBacktest(customSignalGenerator: new MyRsiSignalGenerator());
```

- **Custom risk gate / execution model**: still available — `AddQuantInfraNetOrchestration(customStages:…, customExecutionModel:…)` under the hood works unchanged because the backtest composes through the same DI entry point.

---

## 5. Parameter sweeps (`ParameterSweepRunner`)

Grid-search a parameter combination without sharing any state between points — each point gets its **own** broker, its **own** DI container, and a fresh `BacktestRunner`:

```csharp
var runner = new ParameterSweepRunner(
    data: data,
    symbols: new[] { "AAPL" },
    backtestOptions: new BacktestOptions { InitialEquityUsd = 10000m },
    baseOrchestration: o =>
    {
        o.Environment = Quant.Infra.Net.Shared.Model.ExchangeEnvironment.Paper;
        o.Parameters["Symbol"] = "AAPL";
        o.Parameters["Strategy"] = "MaCross";
    });

var grid = new List<IReadOnlyDictionary<string, string>>();
foreach (var f in new[] { "1", "2", "3" })
    foreach (var s in new[] { "5", "10", "15" })
        grid.Add(new Dictionary<string, string> { ["FastPeriod"] = f, ["SlowPeriod"] = s });

var results = await runner.RunAsync(grid, maxDegreeOfParallelism: 4);
var best = results
    .OrderByDescending(r => r.Backtest.Metrics.SharpeRatio)  // or any other metric
    .First();
```

`Parallel.ForEachAsync` under the hood; `pinned` results stay in grid order regardless of which point finishes first. `ParameterSweepRunnerTests` (B4) verify 3×3 = 9 mutually independent runs plus determinism for repeated points.

---

## 6. Guardrails (what the engine will *not* do)

| Guardrail | Why |
|---|---|
| Zero network requests inside the replay loop | All data must be materialized into `HistoricalDataSet` **before** the run (§11.11). |
| No look-ahead | `SliceUpTo(symbol, asOfUtc)` caps the visible history at the bar currently being replayed. |
| `Environment` forced to `Paper` | `AddQuantInfraNetBacktest` overwrites `OrchestrationOptions.Environment`; you cannot accidentally wire a live broker into a backtest. |
| No vectorized batch paths | Every bar is one event-driven `StrategyPipeline.RunAsync` call — the exact same code that runs under Paper (§11.5). |
| No RunMode field in the pipeline | Strategy code cannot tell it is being backtested — so it cannot write `if (isBacktest)` branches (§11.2). |
| Existing modules untouched | `Quant.Infra.Net*`, `.Orchestration*`, `.Console`, `MyQuantApp` are read-only dependencies; the engine is pure addition (§11.1). |
