# MCP Server — AI Agent Access (EN)

> 中文: [mcp-server-ch.md](mcp-server-ch.md) · [Index](README-en.md)

Quant.Infra.Net ships an **MCP (Model Context Protocol) server**: a .NET tool that exposes research, backtesting and paper-trading capabilities as **MCP tools**. A local AI agent such as Claude Desktop, Claude Code, VS Code Copilot, Cursor or Cline can then drive it in **natural language**. You describe a strategy, get a professional backtest with charts, and keep refining it in conversation.

> **Why MCP?** More and more software is *used by AI agents rather than people*. The MCP server is the bridge between your local AI and Quant.Infra.Net: no C# code and no UI, just a prompt.

---

## 1. What this is (and what it is not)

**Tools:**

| Tool | Purpose |
|---|---|
| `list_strategies` | Built-in strategies + parameters: MaCross, MeanReversion, PairTradingZScore, **MultiFactor** |
| `list_data_sources` | Which data sources are free / need a key, coverage, symbol formats, caveats |
| `run_backtest` | One backtest. Returns metrics vs a buy-and-hold benchmark, a market-regime breakdown, yearly returns, warnings and a monthly equity curve, and writes a **self-contained HTML report with charts** |
| `optimize_strategy` | Grid search on the in-sample window, with every combination validated on an **out-of-sample** tail. Returns the best and the *most robust* parameters plus an overfitting verdict |
| `walk_forward` | Rolling train → test windows: re-optimize on each train window, trade the next unseen window, and stitch the out-of-sample results |
| `fetch_ohlcv` | Raw daily bars (≤ 500 per call) |
| `run_paper_cycle` | One Paper-mode pipeline cycle (in-memory broker, zero real funds) |

**Prompts** (one-click workflows in clients that support MCP prompts): `quickstart`, `strategy_research`, `regime_check`.

**Out of scope, deliberately (a product boundary, not a bug):**

- **No Testnet / Live ordering.** Real-money trading (Binance futures, IB, Schwab, etc.) is intentionally **not** exposed as an MCP tool. Real-order capability will ship as a paid Pro feature under a separate commercial plan.
- **No arbitrary shell / file-system access.** The only file the server writes is the HTML report, into your reports folder.

---

## 2. Install (one line)

Prereq: [.NET 8 SDK or newer](https://dotnet.microsoft.com/download).

```bash
dotnet tool install -g Quant.Infra.Net.Mcp
# upgrade later: dotnet tool update -g Quant.Infra.Net.Mcp
```

This puts a `quant-infra-mcp` command on your PATH. It speaks **stdio**, so there's no HTTP server, no port and no extra infrastructure.

With the **.NET 10 SDK** you can skip installing and let the client run it from NuGet with `dnx Quant.Infra.Net.Mcp --yes`.

From source, for contributors:

```bash
git clone https://github.com/memoryfraction/Quant.Infra.Net.git
dotnet run --project Quant.Infra.Net/src/Quant.Infra.Net.Mcp
```

---

## 3. Client configuration

**Claude Desktop** (`claude_desktop_config.json`; on Windows `%APPDATA%\Claude\claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "quant-infra-net": {
      "command": "quant-infra-mcp",
      "env": { "TIINGO_API_KEY": "optional-free-key" }
    }
  }
}
```

With .NET 10 and no install step: `"command": "dnx", "args": ["Quant.Infra.Net.Mcp", "--yes"]`.

**Claude Code:**

```bash
claude mcp add quant-infra-net -- quant-infra-mcp
```

**VS Code / Copilot** (`.vscode/mcp.json`):

```json
{ "servers": { "quant-infra-net": { "type": "stdio", "command": "quant-infra-mcp" } } }
```

Restart the client. You should see 7 tools and 3 prompts.

**Optional environment variables**

| Variable | Purpose |
|---|---|
| `TIINGO_API_KEY` | Free Tiingo key, a more stable US equity feed |
| `QUANT_INFRA_REPORTS_DIR` | Report folder (default `~/Documents/QuantInfraNet/reports`) |
| `QUANT_INFRA_CACHE_DIR` / `QUANT_INFRA_CACHE=off` | Data cache folder (default `%LOCALAPPDATA%/QuantInfraNet/cache`) / disable caching |
| `FINNHUB_API_KEY` / `FMP_API_KEY` / `TWELVEDATA_API_KEY` | Legacy providers |

---

## 4. Talk to it — from idea to robust result

**Quick start:** run the `quickstart` prompt, or ask:

> Backtest a 200-day moving-average timing strategy on SPY from 2005 to today using Yahoo data. Compare it to buy and hold and show me the report.

**A real multi-factor conversation:**

> Rotate between SPY, QQQ, IWM, EFA, EEM, TLT, GLD and DBC since 2008. Every month hold the 3 with the best 6-month momentum, preferring lower-volatility ones, and go to SHY when SPY is below its 200-day average. Benchmark SPY.

The agent calls `run_backtest` with `strategy=MultiFactor, universe=..., factors="momentum=1,lowvol=0.5", topK=3, regimeSymbol=SPY, defensiveSymbol=SHY, benchmark=SPY, dataSource=Yahoo`. A real run (2008-01 → 2026-11) returned:

| | Strategy | SPY buy & hold |
|---|---|---|
| CAGR | 7.6% | 11.2% |
| Sharpe | 0.68 | 0.64 |
| Max drawdown | **−18.3%** | −51.9% |

Along with that it returned the regime table (best in Bull-Volatile, loses in Bear-Volatile but far less than SPY) and the path of the HTML report. Then you keep going:

> Is the 3-holdings / 6-month choice overfitted? Walk-forward it with TopK 2–4 and lookbacks 3/6/12 months.

`walk_forward` → 16 out-of-sample windows, 75% profitable, walk-forward efficiency 0.64. The parameter choice is **unstable**: 7 different "best" settings across the 16 windows. The stitched out-of-sample Sharpe is below SPY's. That is the honest answer: the rotation buys a much smaller drawdown at the cost of return, and its tuned parameters do not persist.

**What the JSON looks like (abbreviated):**

```json
{
  "strategy": "MultiFactor",
  "dataSource": "Yahoo",
  "metrics": { "cagrPct": 7.56, "sharpe": 0.68, "maxDrawdownPct": -18.26, "sortino": 0.93 },
  "benchmark": { "label": "SPY buy & hold", "cagrPct": 11.15, "sharpe": 0.64, "beta": 0.29, "alphaPct": 4.32 },
  "regimes": [ { "regime": "Bull-Calm", "shareOfDaysPct": 49.7, "strategyAnnReturnPct": 7.79, "benchmarkAnnReturnPct": 14.22 } ],
  "regimeVerdict": "Best in Bull-Volatile (22.1%/yr), worst in Bear-Volatile (-9.4%/yr). Strongly regime-dependent: …",
  "warnings": [ "Survivorship bias: today's tickers exclude delisted companies; prefer ETFs/indices for long tests." ],
  "report": { "path": "C:\\Users\\you\\Documents\\QuantInfraNet\\reports\\MultiFactor_SPY_QQQ_…html" },
  "equityCurveMonthly": [ { "date": "2008-01-31", "strategy": 10012.4, "benchmark": 9391.2 } ]
}
```

> `profitFactor` / `sharpe` are omitted (`null`) when not finite. For example, zero losing trades gives an infinite profit factor.

### 4.1 The HTML report

A self-contained file with inline SVG, no internet needed, and you can email it. It includes KPI tiles vs the benchmark, a "read this first" warnings box, equity vs benchmark, the drawdown, a **rolling 6-month Sharpe**, the **market-regime table**, calendar-year bars, a monthly heatmap, a statistics table (Sortino, beta/alpha, exposure, turnover), parameters and the last 100 fills. `optimize_strategy` and `walk_forward` reports add their grid / window tables.

### 4.2 Strategy styles change: how the tools deal with it

A strategy that worked in one period often fails in the next (momentum crashes, trend-following whipsaws, mean reversion in trending markets). The tools handle this in four layers:

1. **Diagnose:** every backtest splits daily returns into Bull/Bear (benchmark vs its 200-day SMA) × Calm/Volatile (20-day realized vol vs the median) and plots a rolling 6-month Sharpe, so you can see *when* the edge vanishes.
2. **Validate:** `optimize_strategy` reports in-sample *and* out-of-sample results for every combination. It recommends the combination with the best *worst-of* the two, not the in-sample peak, and flags when the in-sample winner loses out-of-sample.
3. **Adapt:** `walk_forward` re-optimizes every `testMonths` on the trailing `trainMonths`. The stitched out-of-sample curve is what continuous re-tuning would actually have earned. Parameter stability and walk-forward efficiency (OOS/IS) tell you whether re-tuning helps or just chases noise.
4. **Defend:** `MultiFactor` can avoid betting on one style. It blends factors (momentum + low-vol + trend), uses an absolute-momentum filter (hold nothing with negative momentum), and has a regime filter (`regimeSymbol` below its SMA → cash or `defensiveSymbol`).

### 4.3 Multi-factor

`MultiFactor` is a **cross-sectional** model. On each rebalance (every `RebalanceDays` bars) it computes each factor for every symbol in `Universe`, z-scores each factor across the universe, sums them with your `Factors` weights, and holds the `TopK` best at equal weight.

| Factor | Definition |
|---|---|
| `momentum` | Return over `MomentumLookback` bars (default 126), skipping the last `SkipRecent` (classic 12-1: `MomentumLookback=252;SkipRecent=21`) |
| `lowvol` | Negative daily-return volatility over `VolLookback` (63) |
| `trend` | Close / SMA(`TrendPeriod`, 200) − 1 |
| `reversal` | Negative 5-bar return |
| `riskadjmom` | Momentum ÷ volatility |

Only as-of data is used, so there is no look-ahead. Fundamental factors such as value and quality need a fundamentals feed and are not built in yet.

### 4.4 Defaults that matter

- **Automatic warm-up:** history before `startDate` is fetched so indicators are ready on day one. Performance is measured from `startDate` only.
- **Fills at the next bar's open** by default (`fillAtNextOpen=false` fills at the signal bar's close).
- **Position size:** 100% for single-symbol strategies, 50% per pair leg, 100%/TopK for MultiFactor (`positionSizePct` overrides).
- **Costs:** 5 bps commission + 2 bps slippage per fill.
- **Known limitation:** every holding gets the same weight (100%/TopK), so when the regime filter switches to `defensiveSymbol` only 1/TopK of equity is in it and the rest stays in cash. Use `positionSizePct` if you want the defensive leg fully invested.
- **Speed:** downloaded data is cached. A 15-year single-ETF backtest takes ~10 s. A 9-ETF, 18-year multi-factor run takes ~1 min, and walk-forward ~1–2 min.

---

## 4.5 Tool reference (quick lookup)

### run_backtest
| Param | Description |
|---|---|
| `strategy` ✅ | MaCross / MeanReversion / PairTradingZScore / MultiFactor |
| `startDate` ✅, `endDate` ✅ | UTC dates; `endDate` may be `today` |
| `symbol` | Single-symbol strategies |
| `symbolA`, `symbolB` | PairTradingZScore |
| `universe`, `factors`, `topK`, `rebalanceDays`, `regimeSymbol`, `defensiveSymbol` | MultiFactor |
| `parameters` | Any other strategy parameters: `"Key=Value;Key=Value"` or a JSON object |
| `benchmark` | Benchmark symbol (default: buy & hold of the traded symbol / equal-weight universe) |
| `dataSource`, `apiKey`, `localFilePath` | See §5 |
| `initialEquityUsd`, `commissionBps`, `slippageBps`, `positionSizePct`, `killSwitchDrawdownPct`, `fillAtNextOpen` | Simulation settings |
| `fastPeriod`, `slowPeriod`, `lookbackBars`, `entryZ`, `exitZ`, `allowShort` | Classic strategy parameters |
| `report` | Write the HTML report (default true) |

### optimize_strategy
Same strategy/data arguments, plus `grid` ✅ (`"SlowPeriod=50,100,150,200; FastPeriod=1..41:20"`; use `|` between values that contain commas), `objective` (sharpe / cagr / calmar / maxdrawdown), `testFraction` (default 0.3) and `maxCombinations` (default 60).

### walk_forward
Same strategy/data arguments, plus `grid` (optional), `trainMonths` (36), `testMonths` (12), `objective` and `maxCombinations` (40).

### list_strategies · list_data_sources
No parameters, zero network.

### fetch_ohlcv
`symbol`, `startDate`, `endDate`, `dataSource`, `apiKey`, `localFilePath`. Hard-capped at 500 bars.

### run_paper_cycle
`strategy`, `symbol` / `symbolA` / `symbolB`, strategy parameters. Returns the event stream; zero network.

---

## 5. Data sources

> **Data is the #1 pain point in backtesting.** The MCP server now defaults to sources that are **free and need no API key**, caches everything on disk, and keeps one abstraction (`IMcpSourceDataService`) so a broken provider can be swapped without touching any tool.

### 5.1 Which source to use

| Source | Cost / key | Coverage | Notes |
|---|---|---|---|
| ⭐ **Yahoo** | free, **no key** | US, HK (`0700.HK`) and China A (`600519.SS`) stocks; ETFs; indices (`^GSPC`); FX (`EURUSD=X`); futures (`GC=F`) | OHLC adjusted for dividends and splits (total return); unofficial endpoint, may rate-limit (cache helps); survivorship bias on single stocks |
| ⭐ **Binance** | free, **no key** | Crypto spot: `BTCUSDT`, `ETHUSDT`… (`BTC`, `BTC-USD` are normalized) | Public market-data mirror `data-api.binance.vision`; ~365 bars/year |
| **Tiingo** | free key (`TIINGO_API_KEY`) | US stocks & ETFs, ~30 years, adjusted | More stable than Yahoo |
| **LocalFile** | free | Your CSV (`date,open,high,low,close,volume[,adjusted_close]`) or JSON | Multi-symbol: pass a folder of `SYMBOL.csv` files or a template like `C:/data/{symbol}.csv` |
| **Demo** | free | Offline synthetic | Smoke tests only |
| Finnhub / Fmp / TwelveData | free keys | US stocks | Kept for compatibility; free tiers are limited (Finnhub historical candles may need a paid plan) |

> **Stooq is intentionally not exposed** (proven unstable: anti-bot challenges, intermittent failures).

### 5.2 API keys

Only Tiingo (and the legacy providers) need one. The server looks for it in this order: **explicit `apiKey` argument** → **`appsettings.json`** next to the executable (`QuantInfraNet:DataSources:{Tiingo|Finnhub|Fmp|TwelveData}:ApiKey`) → **environment variable** (`TIINGO_API_KEY`, … — easiest to set in the client's `env` block). A missing key returns a clear error telling you where to register and which no-key alternative to use.

### 5.3 Cache

Bars are cached per provider and symbol. Ranges that ended more than 3 days ago never expire; recent ranges expire after 12 hours. Iterating in conversation ("try 150 days", "add GLD") therefore re-runs in seconds without hitting rate limits.

### 5.4 SOLID, in one paragraph

`IMcpSourceDataService` (the *abstraction*) has one method, `DownloadDailyAsync(symbol, start, end)`. Each provider implements it, `CachingSourceDataService` decorates any of them, and `McpSourceDataFactory` (*single responsibility*: map a name to a provider) creates them. The tools depend **only on the interface**, so adding a provider means one class and one enum value.

---

## 6. The boundary, stated plainly

This MCP server is a **read-only + simulated** surface:

- **Reads:** OHLCV, strategy catalog, backtest metrics, paper-cycle event stream.
- **Simulates:** one Paper pipeline cycle per call (in-memory broker, zero network, zero funds).
- **Never:** places an order, touches an API key for a live venue, or exposes Testnet/Live.

> **Why the boundary?** Quant.Infra.Net's core promise is that a strategy file + a config file
> takes you from idea → backtest → paper → live. The MCP channel is the *top of that funnel* —
> the part where an agent (or a human) wants to **explore, compare, and decide** before any
> capital is at risk. Real-order placement is a **paid Pro feature** with a separate commercial
> plan, and it will not ride on the free MCP channel. That is the product's honest shape.

---

## 7. Agent prompt patterns (copy-paste)

These are starting points. Change the symbol, window, strategy or provider to fit your question. All of them use **free, no-key data**.

### 7.1 Single-strategy backtest with a report

> Use Yahoo data. Backtest **MaCross** on **QQQ** (slowPeriod=200) from **2005-01-01 to today**. Compare it to buy & hold and explain the regime table. Give me the report path.

### 7.2 Compare two strategies on the same window

> Using Yahoo, on **SPY** from 2010 to today, compare **MaCross** (slowPeriod=200) with **MeanReversion** (lookbackBars=20, entryZ=1.5, exitZ=0.3). Show one table with CAGR, Sharpe, max drawdown and trades, and tell me which suits a drawdown-averse investor.

### 7.3 Multi-factor rotation

> MultiFactor over **SPY,QQQ,IWM,EFA,EEM,TLT,GLD,DBC**, factors momentum=1,lowvol=0.5, topK 3, monthly, regimeSymbol SPY, defensiveSymbol SHY, benchmark SPY, Yahoo, 2008 to today.

### 7.4 Optimize without fooling yourself

> optimize_strategy: MaCross on QQQ, grid SlowPeriod=50..250:25, Yahoo, 2005 to today, objective sharpe. Is the best parameter on a plateau or a lonely peak? Did it hold out of sample?

### 7.5 Does it survive regime changes?

> walk_forward the MultiFactor setup above with grid "TopK=2,3,4; MomentumLookback=63,126,252", train 36 months, test 12. How many windows were profitable, how stable were the chosen parameters, and does the stitched out-of-sample curve beat SPY?

### 7.6 Crypto

> Use Binance data. Backtest MaCross on BTC (slowPeriod=100) from 2019 to today, then MultiFactor over BTC,ETH,SOL,BNB,XRP with topK 2 and regimeSymbol BTC.

### 7.7 Your own data (LocalFile)

> Use LocalFile with the folder `E:/data/daily` (one SYMBOL.csv per ticker). Run MultiFactor over AAA,BBB,CCC,DDD from 2016 to 2020.

> **Why LocalFile?** It is fully deterministic, with zero network and no API key. It suits CI, teaching, and proprietary data.

---

## 8. Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| The client shows no `quant-infra-net` server | Tool not on PATH, or .NET SDK missing | `dotnet --version`; `dotnet tool list -g`; restart the client after installing. On .NET 10 try the `dnx` config |
| `Yahoo request failed (HTTP 429)` | Yahoo rate limit | Wait a minute (cached ranges are not refetched), or use Tiingo (free key) / LocalFile |
| `API key is required for Tiingo` | No key | Add `TIINGO_API_KEY` to the client's `env` block, or use Yahoo |
| `No bars between …` | Symbol didn't exist in the window, or wrong format | Check the symbol format with `list_data_sources` (e.g. `0700.HK`, `BTCUSDT`) |
| A long multi-asset `walk_forward` is slow | The engine replays every bar through the full pipeline | Shorten the window, use a smaller grid, or fewer symbols; cached data makes reruns faster |
| `truncated: true` on `fetch_ohlcv` | Window > 500 bars | That's the hard cap working as designed; split the window |
| `run_backtest` throws `Unknown strategy` | Typo in the name | Use one of the names from `list_strategies` |

---

## 9. Where this lives in the repo

| Path | What |
|------|------|
| `src/Quant.Infra.Net.Mcp/` | The MCP server (.NET tool `quant-infra-mcp`, stdio transport, NuGet package type `McpServer`, `.mcp/server.json`) |
| `src/Quant.Infra.Net.Mcp/Tools/` | `RunBacktestTool`, `OptimizeTools` (optimize_strategy + walk_forward), `ListDataSourcesTool`, `ListStrategiesTool`, `FetchOhlcvTool`, `RunPaperCycleTool` |
| `src/Quant.Infra.Net.Mcp/Engine/` | `BacktestEngine` (load once, warm-up, run), `ResearchService` (requests, grids, parallel runs, stitching) |
| `src/Quant.Infra.Net.Mcp/Strategies/` | `MultiFactorRotation` (discovered by the runtime's `StrategyCatalog`) |
| `src/Quant.Infra.Net.Mcp/Reporting/` | `PerformanceAnalytics` (benchmark, regimes, monthly/yearly), `HtmlReportWriter` (inline-SVG report) |
| `src/Quant.Infra.Net.Mcp/DataSources/` | `IMcpSourceDataService`, `Yahoo*` / `Binance*` / `Tiingo*` / `LocalFile*` / legacy providers, `CachingSourceDataService`, `McpSourceDataFactory` |
| `src/Quant.Infra.Net.Mcp/Prompts/` | MCP prompts: `quickstart`, `strategy_research`, `regime_check` |
| `src/Quant.Infra.Net.Mcp.Tests/` | Offline tests, plus a live smoke test gated by `QUANT_INFRA_LIVE_TESTS=1` |

> **Guardrail honored:** the MCP server is a *consumer* of the unified runtime's public API.
> No files under `src/Quant.Infra.Net/`, `src/Quant.Infra.Net.Runtime/`, `src/Quant.Infra.Net.Orchestration*/`,
> `src/Quant.Infra.Net.Backtest*/`, or `MyQuantApp/` were modified. See `git log --stat` for proof.
