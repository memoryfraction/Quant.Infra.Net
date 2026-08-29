# MCP Server — AI Agent Access (EN)

> 中文: [mcp-server-ch.md](mcp-server-ch.md) · [Index](README-en.md)

Quant.Infra.Net now ships with an **MCP (Model Context Protocol) server** — a tiny .NET console
app that exposes the platform's read-only / backtest / paper capabilities as **MCP tools**, so a
local AI agent (Claude Desktop, Cursor, Cline, Codex, or any MCP client) can drive it with
**natural language**.

> **Why MCP?** Software is increasingly *used by AI agents, not humans*. The MCP server is the
> bridge between your local AI and Quant.Infra.Net: no C# code, no UI, just a prompt.

---

## 1. What this is (and what it is not)

**In scope — deliberately:**

- `list_strategies` — list all built-in strategies (MaCross, MeanReversion, PairTradingZScore)
- `run_backtest` — one full backtest, returns CAGR / Sharpe / Calmar / MaxDrawdown / WinRate /
  ProfitFactor / TotalTrades / Commission as JSON
- `run_paper_cycle` — one Paper-mode pipeline cycle (in-memory broker, zero network, zero real
  funds), returns the event stream + optional portfolio snapshot
- `fetch_ohlcv` — daily OHLCV bars (Demo / Finnhub / FMP / TwelveData), hard-capped at 500 bars

**Partially in scope — deliberately (simulated, not real):**

- `run_paper_cycle` runs ONE Paper pipeline cycle per call. The Paper mode uses
  `PaperBinanceUsdFutureService` — an **in-memory broker** that places orders against a
  simulated book (zero network, zero real funds, zero API keys). This is **real paper-trading
  order placement**, not just a read-only observation. The event stream and portfolio
  snapshot it returns are the same artifacts a live Paper deployment would produce.

**Out of scope — deliberately (product boundary, not a bug):**

- **No Testnet / Live ordering.** Real-money trading (Binance futures, IB, Schwab, etc.) is intentionally **not** exposed as an MCP tool. That is a **product decision**: real-order capability will ship as a paid Pro feature under a separate commercial plan, not via the free MCP channel. This MCP server is read-only + simulated. Real-money trading
  (Binance futures, IB, Schwab, etc.) is intentionally **not** exposed as an MCP tool. That is a
  **product decision**: real-order capability will ship as a paid Pro feature under a separate
  commercial plan, not via the free MCP channel.
- **No arbitrary shell / file-system access.** The MCP surface is exactly the four tools above.

> **Honest statement:** this version supports **paper-trading order placement** (simulated, in-memory) via `run_paper_cycle`, but does **not** support live or testnet order placement. That is
> a deliberate scope limit, not an unfinished feature. See §6.

---

## 2. Install

Prereqs: .NET 8 SDK (or a self-contained build of `Quant.Infra.Net.Mcp.dll`).

```bash
cd Quant.Infra.Net
dotnet build src/Quant.Infra.Net.Mcp -c Release
# → src/Quant.Infra.Net.Mcp/bin/Release/net8.0/Quant.Infra.Net.Mcp.dll
```

Or run from source (Claude Desktop will launch `dotnet` for you):

```bash
dotnet run --project src/Quant.Infra.Net.Mcp
```

The process speaks **stdio** — no HTTP, no port, no extra infrastructure.

---

## 3. Claude Desktop configuration

`claude_desktop_config.json` (Windows: `%APPDATA%\Claude\claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "quant-infra-net": {
      "command": "dotnet",
      "args": [
        "E:/Github/Quant.Infra.Net/src/Quant.Infra.Net.Mcp/bin/Release/net8.0/Quant.Infra.Net.Mcp.dll"
      ]
    }
  }
}
```

> On Linux / macOS, replace the path with your local build output.
> Restart Claude Desktop. The server appears in the tools list with the four tools.

Any MCP client works the same way: point the stdio command at the .dll (or at
`dotnet run --project ...`), and the four tools are discoverable via the standard MCP
`tools/list` handshake.

---

## 4. Talk to it in natural language — a real prompt

Copy this into Claude Desktop (or any MCP client):

> **Prompt:**
> 使用 **Finnhub** 数据源，apikey: `***`，对 **AAPL** 跑一个 **MeanReversion** 策略
> （lookbackBars=100, entryZ=2.0, exitZ=0.5）在 **2024-01-01 到 2024-06-30** 的回测。
> 给我回测指标和图形；我再决定下一步。

**What the agent does (in order):**

1. Calls `list_strategies` → confirms `MeanReversion` exists, reads its parameter table.
2. Calls `run_backtest` with the strategy / symbol / window / parameters above.
3. Receives the JSON metrics block and **interprets it** (Sharpe ≥ 1? ProfitFactor ≥ 1.2?
   Enough trades?). It then tells you which of the candidate strategies looks better *on the
   same window* — the "compare and judge" step you asked for.
4. If you asked for a chart, it can also call `fetch_ohlcv` to pull the price series and render
   it client-side.

**Example return shape (abbreviated):**

```json
{
  "strategy": "MeanReversion",
  "symbols": ["AAPL"],
  "dataSource": "Finnhub",
  "window": { "start": "2024-01-01T00:00:00Z", "end": "2024-06-30T00:00:00Z" },
  "metrics": {
    "cagrPct": 12.4,
    "sharpe": 1.31,
    "calmar": 0.82,
    "maxDrawdownPct": -15.1,
    "maxDrawdownDurationDays": 22,
    "winRatePct": 54.3,
    "profitFactor": 2.34,
    "totalTrades": 18,
    "totalCommissionUsd": 18.5
  },
  "trades": 18,
  "verdict": "Looks DECENT: Sharpe >= 1, profit factor >= 1.2, enough trades. Still check drawdown and regime sensitivity before trusting it."
}
```

> **Note on `profitFactor`:** when a strategy has zero losing trades, the runtime reports
> `ProfitFactor = Infinity`. The MCP layer now serializes that as `null` (the JSON field is omitted)
> rather than a misleading `0.0`. `null` here means **no losing trades at all** — the strongest
> possible profit factor. The `interpretation` field accounts for this correctly.

---

## 4.5 Tool reference (quick lookup)

### list_strategies
| Param | Type | Required | Description |
|-------|------|----------|-------------|
| *(none)* | — | — | Returns all built-in strategy names + parameter descriptions. Zero network. |

**Returns:** JSON array of { "name": string, "description": string }.

### run_backtest
| Param | Type | Required | Description |
|-------|------|----------|-------------|
| strategy | string | ✅ | MaCross, MeanReversion, or PairTradingZScore (see list_strategies). |
| startDate | string (UTC) | ✅ | Window start, e.g. 2024-01-01. |
| ndDate | string (UTC) | ✅ | Window end, e.g. 2024-06-30. |
| symbol | string | single-symbol strategies | E.g. AAPL. Required for MaCross / MeanReversion. |
| symbolA / symbolB | string | PairTradingZScore | Two legs. |
| dataSource | string | no | Demo (default) / Finnhub / Fmp / TwelveData / LocalFile. |
| piKey | string | no | Override for Finnhub/Fmp/TwelveData (otherwise reads appsettings.json / env var). |
| localFilePath | string | LocalFile only | CSV or JSON path (absolute or relative to AppContext.BaseDirectory). |
| initialEquityUsd | int | no | Default 10000. |
| commissionBps / slippageBps | int | no | Defaults 5 / 2. |
| astPeriod / slowPeriod | int | MaCross only | MA periods. |
| lookbackBars / ntryZ / xitZ | int/double | MeanReversion / PairTrading | Lookback + z-thresholds. |
| llowShort | bool | no | Override short-allowance. |

**Returns:** JSON { strategy, symbols, dataSource, window, metrics { cagrPct, sharpe, calmar, maxDrawdownPct, maxDrawdownDurationDays, winRatePct, profitFactor, totalTrades, totalCommissionUsd }, trades, interpretation }.

### run_paper_cycle
| Param | Type | Required | Description |
|-------|------|----------|-------------|
| strategy | string | ✅ | Same as un_backtest. |
| symbol / symbolA / symbolB | string | per strategy | Same rules. |
| astPeriod / slowPeriod / lookbackBars / ntryZ / xitZ | int/double | no | Strategy params. |

**Returns:** JSON { events: [{ stage, message, timestampUtc }], errorCount: int }. Zero network.

### fetch_ohlcv
| Param | Type | Required | Description |
|-------|------|----------|-------------|
| symbol | string | ✅ | E.g. AAPL. |
| startDate | string (UTC) | ✅ | E.g. 2024-01-01. |
| ndDate | string (UTC) | ✅ | E.g. 2024-06-30. |
| dataSource | string | no | Demo (default) / Finnhub / Fmp / TwelveData / LocalFile. |
| piKey | string | no | Override for real providers. |
| localFilePath | string | LocalFile only | CSV/JSON path. |

**Returns:** JSON { symbol, bars: [{ date, open, high, low, close, volume }], truncated: bool, totalBars: int }. Hard-capped at 500 bars.

---
## 5. Data sources — the thing people get wrong

> **Data source is the #1 reason a quant project quietly dies.** The MCP server follows the
> same SOLID discipline as the rest of the repo: the **tool depends on the
> `IMcpSourceDataService` abstraction**, not on any concrete provider. The factory
> (`McpSourceDataFactory`) picks the concrete implementation from your config.

### 5.1 Provider choice (config-driven, factory pattern)

| # | Provider | Free tier | Where to get a key | appsettings section |
|---|----------|-----------|--------------------|---------------------|
| ⭐ | **Finnhub** | 60 calls/min | [finnhub.io/register](https://finnhub.io/register) | `QuantInfraNet:DataSources:Finnhub:ApiKey` |
| 2 | **FMP** (Financial Modeling Prep) | 250 req/day | [financialmodelingprep.com](https://financialmodelingprep.com/subscription) | `QuantInfraNet:DataSources:Fmp:ApiKey` |
| 3 | **Twelve Data** | 800 credits/day | [twelvedata.com](https://twelvedata.com/pricing) | `QuantInfraNet:DataSources:TwelveData:ApiKey` |
| 4 | **LocalFile** (stable, offline) | infinite | — (no key needed; specify `localFilePath`) | — |
| 5 | **Demo** (offline synthetic) | infinite | — (no key needed) | — |

> **Stooq is intentionally dropped** for the MCP surface. It has proven unstable in our testing (anti-bot challenges, intermittent failures). The MCP server does not ship a Stooq provider. **LocalFile is the recommended stable default** for examples, demos, and CI — it reads from a local CSV or JSON file, is fully deterministic, and needs no API key. For real backtests, prefer Finnhub / FMP / TwelveData.
> If you need Stooq for the in-repo backtest path, use `DataSourceKind.Stooq` in the Runtime
> `appsettings.json` (see [custom-data-source](custom-data-source-en.md)); but for the MCP
> channel, prefer Finnhub / FMP / TwelveData.

### 5.2 Put your API key in `appsettings.json`

Create (or extend) `src/Quant.Infra.Net.Mcp/appsettings.json` next to the .dll:

```json
{
  "QuantInfraNet": {
    "DataSources": {
      "Finnhub":    { "ApiKey": "REPLACE_WITH_YOUR_FINNHUB_KEY" },
      "Fmp":        { "ApiKey": "REPLACE_WITH_YOUR_FMP_KEY" },
      "TwelveData": { "ApiKey": "REPLACE_WITH_YOUR_TWELVE_DATA_KEY" }
    }
  }
}
```

> The factory reads in this order: **explicit `apiKey` tool arg** → **appsettings.json** →
> **environment variable** (`FINNHUB_API_KEY` / `FMP_API_KEY` / `TWELVEDATA_API_KEY`).
> No key → the tool returns a clear error message telling you which provider to register for.
> We **encourage you to register a free key** for at least one real provider — the Demo source
> is great for smoke tests, but for real backtests you want real bars.

### 5.3 SOLID, in one paragraph

`IMcpSourceDataService` (the *abstraction*) has one method: `DownloadDailyAsync(symbol, start, end)`.
`FinnhubSourceDataService`, `FmpSourceDataService`, `TwelveDataSourceDataService` (the *concrete
implementations*) each implement it against their REST endpoint. `McpSourceDataFactory`
(*single responsibility*: pick a provider from config) returns one of them. The `fetch_ohlcv`
tool and `run_backtest` tool depend **only on the interface** — you can add a new provider by
adding one class and one enum value, without touching any tool.

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

Use these as starting points. Adjust symbol / window / strategy / provider to your question.

### 7.1 Single-strategy backtest

> 使用 **Finnhub** 数据源，apikey: `***`，对 **MSFT** 跑一个 **MaCross** 策略
> （fastPeriod=20, slowPeriod=200）在 **2023-01-01 到 2025-12-31** 的回测。
> 给我 CAGR、Sharpe、MaxDrawdown、WinRate；如果 Sharpe < 1 就告诉我原因。

### 7.2 Compare two strategies on the same window

> 用 **FMP** 数据源，apikey: `***`，对 **NVDA** 在 **2024-01-01 到 2024-06-30** 跑两个策略：
> 1) **MeanReversion**（lookbackBars=100, entryZ=2.0, exitZ=0.5）
> 2) **MaCross**（fastPeriod=10, slowPeriod=200）
> 给我两个策略的指标对比表，并判断哪个更好、为什么。

### 7.3 Paper-cycle smoke test (no API key needed)

> 用 **Demo** 数据源，对 **AAPL** 跑一次 **MeanReversion** 的 paper cycle，
> 给我事件流和组合快照；如果 errors > 0 就列出每一条。

### 7.4 Pull price data

> 用 **TwelveData** 数据源，apikey: `***`，拉 **TSLA** 在 **2025-06-01 到 2025-08-28** 的
> 日线 OHLCV；如果超过 500 根就截断并告诉我总数。

### 7.5 Full decision loop (the "I'll decide next step" flow)

> 使用 **Finnhub** 数据源，apikey: `***`，对 **AAPL** 跑 **MeanReversion**
> （lookbackBars=100, entryZ=2.0, exitZ=0.5）在 **2024-01-01 到 2024-06-30** 的回测，
> 同时跑 **MaCross**（fastPeriod=20, slowPeriod=200）做对比。
> 给我两个策略的指标对比表 + 一句话判断哪个更好 + 我下一步应该调哪个参数。

---

### 7.6 LocalFile stable example (no API key needed)

> Use the **LocalFile** data source at `E:/data/aapl-daily.csv`, run a **MeanReversion**
> strategy (lookbackBars=100, entryZ=2.0, exitZ=0.5) on **AAPL** for **2024-01-01 to 2024-06-30**.
> Give me the backtest metrics (CAGR, Sharpe, MaxDrawdown, WinRate) and a one-line verdict;
> I will decide the next step.

> **Why LocalFile?** It is the most stable data source: zero network, zero API key, fully
> deterministic. Ideal for examples, demos, CI, and for teaching an Agent how to drive
> Quant.Infra.Net before you hook up a real provider.




| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Claude Desktop shows no `quant-infra-net` server | Path to the .dll is wrong, or .NET SDK is not on PATH | Verify `dotnet --version`; fix the `args` path; or use `dotnet run --project ...` |
| Tool returns `error: API key is required for ...` | No key in appsettings.json / env | Add the key to `appsettings.json` (see §5.2) or set the env var |
| `truncated: true` on `fetch_ohlcv` | Window > 500 bars | That's the hard cap working as designed; split the window or request a shorter range |
| `run_paper_cycle` returns 0 events | Strategy parameters missing (e.g., no `Symbol`) | Pass the symbol(s) the strategy expects — see `list_strategies` |
| `run_backtest` throws `Unknown Strategy` | Typo in the name | Use one of the names from `list_strategies` (case-sensitive) |

---

## 9. Where this lives in the repo

| Path | What |
|------|------|
| `src/Quant.Infra.Net.Mcp/` | The MCP server (console app, stdio transport, 4 tools) |
| `src/Quant.Infra.Net.Mcp/Tools/` | `ListStrategiesTool.cs`, `RunBacktestTool.cs`, `RunPaperCycleTool.cs`, `FetchOhlcvTool.cs` |
| `src/Quant.Infra.Net.Mcp/DataSources/` | `IMcpSourceDataService` (abstraction), `Finnhub*` / `Fmp*` / `TwelveData*` (concrete), `McpSourceDataFactory` (factory) |
| `src/Quant.Infra.Net.Mcp.Tests/` | 17 tests (3 P3 paper-cycle, 4 P4 fetch boundary, 10 P2 backtest) |
| `docs/manual/mcp-server-en.md` / `-ch.md` | This page |

> **Guardrail honored:** the MCP server is a *consumer* of the unified runtime's public API.
> No files under `src/Quant.Infra.Net/`, `src/Quant.Infra.Net.Runtime/`, `src/Quant.Infra.Net.Orchestration*/`,
> `src/Quant.Infra.Net.Backtest*/`, or `MyQuantApp/` were modified. See `git log --stat` for proof.








