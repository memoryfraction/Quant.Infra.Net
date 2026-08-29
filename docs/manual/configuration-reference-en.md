# Configuration Reference (EN)

> 中文: [configuration-reference-ch.md](configuration-reference-ch.md) · [Index](README-en.md)

This page is the **complete, field-by-field** reference for the three configuration objects that drive
the Runtime / Orchestration / Backtest stack. Every row was verified against the source:

- `src/Quant.Infra.Net.Runtime/Models/RuntimeOptions.cs`
- `src/Quant.Infra.Net.Orchestration/Models/OrchestrationOptions.cs` (+ `Models/NotificationOptions.cs`)
- `src/Quant.Infra.Net.Backtest/Models/BacktestOptions.cs`
- enums: `Models/RunMode.cs`, `Models/DataSourceKind.cs` (Runtime), `Shared/Model/Enums.cs` (`ExchangeEnvironment`)
- a working sample: `src/Quant.Infra.Net.Runtime.Console/appsettings.json`

All three objects are bound to configuration sections by `AddQuantInfraNet(...)`
(`src/Quant.Infra.Net.Runtime/DependencyInjection.cs`):

| Section in appsettings.json | Bound to |
|---|---|
| `Runtime` | `RuntimeOptions` |
| `Orchestration` | `OrchestrationOptions` |
| `Backtest` | `BacktestOptions` |

---

## 1. `RuntimeOptions` — the top-level switch

**Section `Runtime`** · Source: `RuntimeOptions.cs`

| Field | Type | Default | Applies when | Description |
|---|---|---|---|---|
| `RunMode` | `RunMode` | `RunMode.Backtest` | Always | The master switch. Decides driver loop + broker (Backtest / Paper / Testnet / Live). |
| `DataSource` | `DataSourceKind` | `DataSourceKind.Demo` | Always | Which historical-data source to wire in (Demo / Yahoo / Csv / Binance / Stooq / Alpaca / Custom). |
| `BinanceApiKey` | `string?` | `null` | `RunMode` = Testnet or Live | Binance USDT-margined futures API key. Ignored in Backtest/Paper. |
| `BinanceApiSecret` | `string?` | `null` | `RunMode` = Testnet or Live | Binance API secret. Ignored in Backtest/Paper. |
| `AlpacaApiKey` | `string?` | `null` | `DataSource` = Alpaca | Alpaca API key (free IEX tier, alpaca.markets). Ignored by other data sources. |
| `AlpacaApiSecret` | `string?` | `null` | `DataSource` = Alpaca | Alpaca API secret. Ignored by other data sources. |

### 1.1 `RunMode` enum (source: `Runtime/Models/RunMode.cs`)

| Value | What it means | Extra config required |
|---|---|---|
| `Backtest` (0) | Historical replay. Driven by `BacktestRunner`, accounted by `BacktestBrokerService`. Zero network. | `Backtest` section for costs/fill timing; a data source with history (Demo is fine for a first run). |
| `Paper` (1) | Wall-clock driven by `PipelineRunner` + `IntervalTrigger`; in-memory accounting by `PaperBinanceUsdFutureService`. Zero network. | None beyond data source choice. |
| `Testnet` (2) | Real Binance **testnet** API. | `BinanceApiKey` / `BinanceApiSecret` (testnet keys) — missing keys throw `NotSupportedException` at startup (fail-fast). |
| `Live` (3) | Real Binance **live** API, real funds. | `BinanceApiKey` / `BinanceApiSecret` (live keys) — same fail-fast behavior. |

### 1.2 `DataSourceKind` enum (source: `Runtime/Models/DataSourceKind.cs`)

| Value | What it means | Extra config required |
|---|---|---|
| `Demo` (0) | Offline synthetic deterministic candles (default; zero network; demos/CI). | None. |
| `Yahoo` (1) | Yahoo Finance via the core library's `TraditionalFinanceSourceDataService` (+ pythonnet/Yahoo Chart API). | Python + `yfinance` available (or the Chart API path). |
| `Csv` (2) | Local CSV files through the core library's file path (`IHistoricalDataSourceService` defaults to `HistoricalDataSourceServiceCsv`). | CSV files on disk. |
| `Binance` (3) | Binance klines via `IBinanceUsdFutureService.GetOhlcvListAsync` (read-only). | A registered `IBinanceUsdFutureService` (runtime resolves one from `RuntimeOptions` credentials). |
| `Custom` (4) | A caller-supplied `ITraditionalFinanceSourceDataService`. | You **must** pass the instance via `AddQuantInfraNet(..., customDataSource: ...)`. Missing instance throws `ArgumentException` (fail-fast). See [custom-data-source-en.md](custom-data-source-en.md). |
| `Stooq` (5) | Free daily CSV from stooq.com. No API key. Community source, **no SLA**. | None (network access to stooq.com). |
| `Alpaca` (6) | Alpaca Market Data (free IEX tier, official Alpaca.Markets SDK). **Recommended default for real historical data.** | `AlpacaApiKey` / `AlpacaApiSecret` (free keys from alpaca.markets). Missing credentials throw `ArgumentException` (fail-fast, never silent fallback). |

---

## 2. `OrchestrationOptions` — strategy, risk, and notifications

**Section `Orchestration`** · Source: `OrchestrationOptions.cs`

| Field | Type | Default | Applies when | Description |
|---|---|---|---|---|
| `Environment` | `ExchangeEnvironment` | `ExchangeEnvironment.Paper` | Always | Paper / Testnet / Live. **Under `AddQuantInfraNet`, you should not set this yourself** — `RunMode` decides it (Backtest forces Paper; Testnet/Live set their own). |
| `InitialEquityUsd` | `decimal` | `10000m` | Paper (and backtest fallback) | Starting equity in USD for the paper account. |
| `MaxWeightPerSymbol` | `double` | `0.3` | Risk stage | Rule 1: per-symbol weight cap `|w|`. |
| `MaxGrossExposure` | `double` | `1.0` | Risk stage | Rule 2: gross exposure cap `Σ|w|`. |
| `KillSwitchDrawdownRate` | `double` | `-0.15` | Risk stage | Rule 3: kill-switch drawdown threshold (negative; triggers when `UnrealizedProfitRate` ≤ value; carries a "liquidate all" recommendation). |
| `MinRebalanceDelta` | `double` | `0.01` | Execution stage | Rebalance dead zone: skip a rebalance when `|target − actual|` < value. Must be ≥ 0. |
| `Parameters` | `Dictionary<string,string>` | `{}` (case-insensitive keys) | Strategy stage | Strategy parameters. Keys are interpreted by the strategy (`Strategy`, `Symbol`, `FastPeriod`, `SlowPeriod`, `WeightPerSymbol`, …). |
| `Notifications` | `NotificationOptions` | see below | Notification stage | Notification routing config. |

### 2.1 `NotificationOptions` (source: `Orchestration/Models/NotificationOptions.cs`)

| Field | Type | Default | Description |
|---|---|---|---|
| `Enabled` | `bool` | `true` | Master switch (false → all severities silently skipped). |
| `DingtalkAccessToken` | `string?` | `null` | DingTalk access token (Info/Warning). |
| `DingtalkSecret` | `string?` | `null` | DingTalk signing secret. |
| `WeChatWebHook` | `string?` | `null` | WeChat Work webhook URL (Warning/Critical). |
| `EmailRecipients` | `string[]` | `[]` | Email recipients (Critical). |
| `EmailSmtpServer` | `string?` | `null` | SMTP host. |
| `EmailPort` | `int` | `587` | SMTP port. |
| `EmailSender` | `string?` | `null` | Sender address. |
| `EmailUsername` | `string?` | `null` | SMTP user. |
| `EmailPassword` | `string?` | `null` | SMTP password. |

> Channel routing (source: `Orchestration/Notifications/RoutingNotificationHub.cs`):
> **Info** = DingTalk · **Warning** = DingTalk + WeChat Work · **Critical** = DingTalk + WeChat Work + Email.
> Unconfigured channels are skipped; channel failures are logged and **never** re-thrown.

### 2.2 `Orchestration.Parameters` — the strategy's own knobs

| Key | Used by | Meaning | Default if absent |
|---|---|---|---|
| `Strategy` | Runtime strategy catalog | Strategy name to resolve (built-ins: `MaCross`, `MeanReversion`, `PairTradingZScore`, or any custom registered descriptor). Unknown name throws `ArgumentException` at startup (fail-fast). | `PairTradingZScore` (constant `DefaultStrategyName` in `Orchestration/DependencyInjection.cs`) |
| `Symbol` | MaCross, QqqmReverseDca, … | Trading symbol (e.g. `AAPL`, `QQQM`). | Strategy-dependent (MaCross rejects with no signal; QqqmReverseDca defaults to `QQQM`). |
| `FastPeriod` / `SlowPeriod` | MaCross | Fast/slow MA periods. | `1` / `200` |
| `AllowShort` | MaCross | `true`/`false`: whether a down-cross emits `Short` (vs `Flat`). | `false` |
| `WeightPerSymbol` | TargetPosition stage | Per-symbol target weight when a signal fires. | `0.3` |
| `MaPeriod` / `BaseWeight` / `AddIntensity` / `TrimIntensity` / `MaxWeight` / `MinWeight` | QqqmReverseDca (example custom stage) | The reverse-MA200 DCA formula knobs. | See `QqqmReverseDcaStrategy.DefaultParameters`. |
| `LookbackBars` | `SignalDataLoader` (fallback fetch) | How many bars to fetch when the context cache is empty. | `240` |
| `DataSource` (lower-level) | `SignalDataLoader` | `"yahoo"` \| `"binance"` — which fallback source to fetch from when the context has no cached series. Invalid values fall back to `yahoo`. | `yahoo` |
| `ResolutionLevel` | `SignalDataLoader` | K-line resolution for the fallback fetch. | yahoo → `Daily`, binance → `Hourly` |

---

## 3. `BacktestOptions` — backtest-only knobs

**Section `Backtest`** · Source: `BacktestOptions.cs`

| Field | Type | Default | Applies when | Description |
|---|---|---|---|---|
| `InitialEquityUsd` | `decimal` | `10000m` | `RunMode` = Backtest | Starting equity for the simulated broker. |
| `WarmupBars` | `int` | `0` | `RunMode` = Backtest | Number of leading bars reserved for indicator warm-up (no trading). |
| `CommissionBps` | `decimal` | `0m` | `RunMode` = Backtest | Commission in **basis points**, deducted from equity on traded notional. |
| `SlippageBps` | `decimal` | `0m` | `RunMode` = Backtest | Slippage in **basis points**; fill price is offset from the mark against the trade direction. |
| `FillTiming` | `FillTiming` | `FillTiming.SameBarClose` | `RunMode` = Backtest | When the signal bar's decision fills. |

### 3.1 `FillTiming` enum (source: `BacktestOptions.cs`)

| Value | Meaning |
|---|---|
| `SameBarClose` (0, default) | Fill at the **signal bar's close price**. |
| `NextBarOpen` (1) | Fill at the **next bar's open price** after the signal bar. Use this to avoid look-ahead optimism. |

> `FillTiming` is a **backtest-only** concept and must not leak into `IPipelineContext` / `OrchestrationOptions`.

---

## 4. A real, runnable sample (from the repo)

`src/Quant.Infra.Net.Runtime.Console/appsettings.json`:

```json
{
  "Runtime": {
    "RunMode": "Backtest",
    "DataSource": "Demo",
    "BinanceApiKey": "",
    "BinanceApiSecret": "",
    "AlpacaApiKey": "",
    "AlpacaApiSecret": ""
  },
  "Orchestration": {
    "InitialEquityUsd": 10000,
    "MaxWeightPerSymbol": 0.5,
    "MaxGrossExposure": 2.0,
    "KillSwitchDrawdownRate": -0.20,
    "MinRebalanceDelta": 0.02,
    "Parameters": {
      "Strategy": "MaCross",
      "Symbol": "AAPL",
      "FastPeriod": "1",
      "SlowPeriod": "200",
      "WeightPerSymbol": "0.3"
    }
  },
  "Backtest": {
    "InitialEquityUsd": 10000,
    "WarmupBars": 0,
    "CommissionBps": 0,
    "SlippageBps": 0,
    "FillTiming": "SameBarClose"
  }
}
```

The demo host (`Program.cs`) binds these three sections into the three objects and calls
`services.AddQuantInfraNet(rt => …, o => …, b => …, strategyAssemblies: typeof(Program).Assembly)`.
Flip `Runtime.RunMode` to change everything — that is the only switch.

## 5. Where to go next

- [writing-a-strategy-en.md](writing-a-strategy-en.md) — use `Parameters` to drive your own logic.
- [risk-management-en.md](risk-management-en.md) — what the three risk knobs actually do.
- [custom-data-source-en.md](custom-data-source-en.md) — `DataSource: Custom` in depth.
- [custom-broker-en.md](custom-broker-en.md) — the `customBroker` entry point (not a config field; it is a DI parameter).
- [faq-en.md](faq-en.md) — what happens when you miss a required credential.
