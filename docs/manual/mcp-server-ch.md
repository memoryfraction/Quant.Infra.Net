# MCP Server — AI Agent 接入（中文）

> English: [mcp-server-en.md](mcp-server-en.md) · [索引](README-ch.md)

Quant.Infra.Net 现在内置了一个 **MCP（Model Context Protocol）Server** —— 一个很小的 .NET
控制台程序，把平台的**只读 / 回测 / 纸面模拟**能力打包成 **MCP 工具（tools）**，让你本机的
AI Agent（Claude Desktop、Cursor、Cline、Codex，或任何 MCP 客户端）用**自然语言**直接驱动
Quant.Infra.Net。

> **为什么做 MCP？** 未来的软件越来越多是**给 AI Agent 用，而不是给人用**。MCP Server 就是你
> 本地 AI 与 Quant.Infra.Net 之间的桥：不用写 C#，不用开 UI，一句话就行。

---

## 1. 这个 MCP Server 是什么（不是什么）

**刻意包含的（产品边界内）：**

- `list_strategies` — 列出全部内置策略（MaCross / MeanReversion / PairTradingZScore）
- `run_backtest` — 跑一次完整回测，返回 CAGR / Sharpe / Calmar / MaxDrawdown / WinRate /
  ProfitFactor / TotalTrades / Commission（JSON）
- `run_paper_cycle` — 跑一次 Paper 模式管道周期（内存券商、零网络、零真实资金），返回事件流
  + 可选的组合快照
- `fetch_ohlcv` — 拉取日线 OHLCV（Demo / Finnhub / FMP / TwelveData），**硬上限 500 根**

**部分包含 —— 刻意（模拟，非真实）：**

- `run_paper_cycle` 每次调用跑一次 Paper 管道周期。Paper 模式用
  `PaperBinanceUsdFutureService` —— 一个**内存券商**，把订单下到模拟账本上（零网络、零真实资金、
  零 API Key）。这是**真实的纸面盘下单**，不只是只读观察。它返回的事件流和组合快照，与真实
  Paper 部署产生的工件完全一致。

**刻意不包含的（产品边界外）：**

- **不暴露 Testnet / Live 下单能力。** 真实资金交易（币安合约、盈透、嘉信等）是**有意的产品范围限制**，不会走免费 MCP 通道。 这是**有意的产品范围限制**，不是 bug、也不是"以后做"
  的模糊承诺。真实资金交易（币安合约、盈透、嘉信等）未来会作为**付费 Pro 功能**在独立商业
  计划下提供，**不会**走免费 MCP 通道。
- **不暴露任意 shell / 文件系统访问。** MCP 表面就是上面这 4 个工具，一个不多。

> **诚实声明：** 当前版本支持**纸面盘下单**（`run_paper_cycle`，模拟、内存、零真实资金），但**不支持**实盘或测试网下单。这是有意的范围限制，不是未完成的功能。
> 详见第 6 节。

---

## 2. 安装

前置：.NET 8 SDK（或 `Quant.Infra.Net.Mcp.dll` 的自包含构建产物）。

```bash
cd Quant.Infra.Net
dotnet build src/Quant.Infra.Net.Mcp -c Release
# 产物：src/Quant.Infra.Net.Mcp/bin/Release/net8.0/Quant.Infra.Net.Mcp.dll
```

或者直接从源码运行（Claude Desktop 会替你调起 `dotnet`）：

```bash
dotnet run --project src/Quant.Infra.Net.Mcp
```

进程走 **stdio** —— 不需要 HTTP、不占端口、零额外基础设施。

---

## 3. Claude Desktop 配置

`claude_desktop_config.json`（Windows 路径：`%APPDATA%\Claude\claude_desktop_config.json`）：

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

> Linux / macOS 把路径换成你本机构建输出即可。
> 重启 Claude Desktop，工具列表里就能看到 4 个工具。

任何 MCP 客户端都一样：把 stdio 命令指向 .dll（或 `dotnet run --project ...`），4 个工具就
能通过标准 MCP `tools/list` 握手被发现。

---

## 4. 用自然语言驱动它 —— 一个真实 Prompt

把下面这段贴进 Claude Desktop（或任何 MCP 客户端）：

> **Prompt：**
> 使用 **Finnhub** 数据源，apikey: `***`，对 **AAPL** 跑一个 **MeanReversion** 策略
> （lookbackBars=100, entryZ=2.0, exitZ=0.5）在 **2024-01-01 到 2024-06-30** 的回测。
> 给我回测指标和图形；我再决定下一步。

**Agent 实际会做的事（按顺序）：**

1. 调 `list_strategies` → 确认 `MeanReversion` 存在，读取它的参数表。
2. 调 `run_backtest`，传入上面的策略 / 标的 / 窗口 / 参数。
3. 拿到 JSON 指标块，**自己解读**（Sharpe ≥ 1？ProfitFactor ≥ 1.2？交易次数够不够？），
   然后**在同一窗口上对比**候选策略，告诉你哪个更好 —— 这就是你要的"自己判断"。
4. 如果你要图形，它还可以调 `fetch_ohlcv` 把 K 线拉下来，在客户端本地渲染。

**返回示例（节选）：**

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

> **关于 `profitFactor`：** 当策略零笔亏损交易时，Runtime 报告 `ProfitFactor = Infinity`。
> MCP 层现在把它序列化为 `null`（JSON 中省略该字段），而不是误导性的 `0.0`。这里的 `null`
> 表示**完全没有亏损交易**，其实是最强的盈亏比。`interpretation` 字段已正确处理此情况。

---

## 5. 数据源 —— 很多人踩坑的地方

> **数据源是量化项目悄悄死掉的第一大原因。** MCP Server 沿用了本仓库的 SOLID 纪律：
> **工具只依赖 `IMcpSourceDataService` 抽象**，不依赖任何具体 Provider；`McpSourceDataFactory`
> 根据你的配置挑具体实现（工厂模式）。

### 5.1 Provider 选择（配置驱动，工厂模式）

| # | Provider | 免费层 | 申请 Key | appsettings 段 |
|---|----------|--------|----------|----------------|
| ⭐ | **Finnhub** | 60 次/分钟 | [finnhub.io/register](https://finnhub.io/register) | `QuantInfraNet:DataSources:Finnhub:ApiKey` |
| 2 | **FMP**（Financial Modeling Prep） | 250 次/天 | [financialmodelingprep.com](https://financialmodelingprep.com/subscription) | `QuantInfraNet:DataSources:Fmp:ApiKey` |
| 3 | **Twelve Data** | 800 次/天 | [twelvedata.com](https://twelvedata.com/pricing) | `QuantInfraNet:DataSources:TwelveData:ApiKey` |
| 4 | **LocalFile**（稳定、离线） | 无限 | —（无需 Key；指定 `localFilePath`） | — |
| 5 | **Demo**（离线合成） | 无限 | —（无需 Key） | — |

> **Stooq 在 MCP 表面被刻意放弃。实测不稳定（反爬挑战、间歇性失败）。MCP Server 不内置 Stooq Provider。**LocalFile 是范例 / Demo / CI 的推荐稳定默认**——从本地 CSV 或 JSON 文件读取，完全确定性，无需 API Key。真实回测请优先用 Finnhub / FMP / TwelveData。如果你需要在仓库回测路径里用 Stooq，可以在 Runtime `appsettings.json` 里
> 设 `DataSourceKind.Stooq`（见 [custom-data-source](custom-data-source-ch.md)）；但 MCP 通道
> 请优先用 Finnhub / FMP / TwelveData。

### 5.2 把 API Key 放进 `appsettings.json`

在 .dll 旁边创建（或扩展）`src/Quant.Infra.Net.Mcp/appsettings.json`：

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

> 工厂读取顺序：**工具显式 `apiKey` 参数** → **appsettings.json** → **环境变量**
> （`FINNHUB_API_KEY` / `FMP_API_KEY` / `TWELVEDATA_API_KEY`）。
> 没 Key 时工具会返回清晰的错误信息，告诉你该去哪个 Provider 注册。
> **强烈建议**你至少为一家真实 Provider 申请一个免费 Key —— Demo 源适合冒烟测试，
> 真实回测要真实 K 线。

### 5.3 SOLID，一段话讲清

`IMcpSourceDataService`（**抽象**）只有一个方法：`DownloadDailyAsync(symbol, start, end)`。
`FinnhubSourceDataService` / `FmpSourceDataService` / `TwelveDataSourceDataService`
（**具体实现**）各自对着自己的 REST 端点实现它。`McpSourceDataFactory`
（**单一职责**：从配置里挑 Provider）返回其中之一。`fetch_ohlcv` 和 `run_backtest`
**只依赖接口** —— 加新 Provider 只需加一个类 + 一个枚举值，不动任何工具。

---

## 6. 边界，直白地说

这个 MCP Server 是**只读 + 模拟**的表面：

- **读：** OHLCV、策略目录、回测指标、paper 事件流。
- **模拟：** 每次调用跑一次 Paper 管道周期（内存券商、零网络、零资金）。
- **永不：** 下单、触碰真实行情商的 API Key、暴露 Testnet/Live。

> **为什么画这条线？** Quant.Infra.Net 的核心承诺是：一个策略文件 + 一个配置文件，就能从
> 想法 → 回测 → 纸面盘 → 实盘。MCP 通道是**漏斗最上面那一段** —— Agent（或人）想在投入任何
> 真实资金**之前**做**探索、对比、决策**。真实下单是**付费 Pro 功能**，走独立商业计划，不会
> 走免费 MCP 通道。这是产品诚实的形状。

---

## 7. Agent Prompt 模式（直接复制粘贴）

以这些为起点，按你的问题改标的 / 窗口 / 策略 / Provider。

### 7.1 单策略回测

> 使用 **Finnhub** 数据源，apikey: `***`，对 **MSFT** 跑一个 **MaCross** 策略
> （fastPeriod=20, slowPeriod=200）在 **2023-01-01 到 2025-12-31** 的回测。
> 给我 CAGR、Sharpe、MaxDrawdown、WinRate；如果 Sharpe < 1 就告诉我原因。

### 7.2 同窗口对比两个策略

> 用 **FMP** 数据源，apikey: `***`，对 **NVDA** 在 **2024-01-01 到 2024-06-30** 跑两个策略：
> 1) **MeanReversion**（lookbackBars=100, entryZ=2.0, exitZ=0.5）
> 2) **MaCross**（fastPeriod=10, slowPeriod=200）
> 给我两个策略的指标对比表，并判断哪个更好、为什么。

### 7.3 Paper 冒烟测试（无需 API Key）

> 用 **Demo** 数据源，对 **AAPL** 跑一次 **MeanReversion** 的 paper cycle，
> 给我事件流和组合快照；如果 errors > 0 就列出每一条。

### 7.4 拉取 K 线

> 用 **TwelveData** 数据源，apikey: `***`，拉 **TSLA** 在 **2025-06-01 到 2025-08-28** 的
> 日线 OHLCV；如果超过 500 根就截断并告诉我总数。

### 7.5 完整决策循环（"我再决定下一步"流）

> 使用 **Finnhub** 数据源，apikey: `***`，对 **AAPL** 跑 **MeanReversion**
> （lookbackBars=100, entryZ=2.0, exitZ=0.5）在 **2024-01-01 到 2024-06-30** 的回测，
> 同时跑 **MaCross**（fastPeriod=20, slowPeriod=200）做对比。
> 给我两个策略的指标对比表 + 一句话判断哪个更好 + 我下一步应该调哪个参数。

---

### 7.6 LocalFile 稳定范例（无需 API Key）

> 用 **LocalFile** 数据源，文件路径 `E:/data/aapl-daily.csv`，对 **AAPL** 跑一个
> **MeanReversion** 策略（lookbackBars=100, entryZ=2.0, exitZ=0.5）在 **2024-01-01 到 2024-06-30**
> 的回测。给我 CAGR、Sharpe、MaxDrawdown、WinRate 和一句话判断；我再决定下一步。

> **为什么用 LocalFile？** 它是最稳定的数据源：零网络、零 API Key、完全确定性。最适合做范例、
> Demo、CI，以及在接真实 Provider 之前教 Agent 怎么驱动 Quant.Infra.Net。




| 症状 | 可能原因 | 处理 |
|------|----------|------|
| Claude Desktop 看不到 `quant-infra-net` | .dll 路径错，或 .NET SDK 不在 PATH | 检查 `dotnet --version`；修 `args` 路径；或改用 `dotnet run --project ...` |
| 工具返回 `error: API key is required for ...` | appsettings.json / 环境变量没 Key | 按 §5.2 把 Key 加到 `appsettings.json` 或设环境变量 |
| `fetch_ohlcv` 返回 `truncated: true` | 窗口 > 500 根 | 这是硬上限按设计工作；拆窗口或请求更短范围 |
| `run_paper_cycle` 返回 0 events | 策略参数缺失（如没传 `Symbol`） | 按 `list_strategies` 给策略该要的 symbol |
| `run_backtest` 抛 `Unknown Strategy` | 名字拼错 | 用 `list_strategies` 返回的名字（大小写敏感） |

---

## 9. 在仓库里的位置

| 路径 | 内容 |
|------|------|
| `src/Quant.Infra.Net.Mcp/` | MCP Server（控制台应用、stdio 传输、4 个工具） |
| `src/Quant.Infra.Net.Mcp/Tools/` | `ListStrategiesTool.cs`、`RunBacktestTool.cs`、`RunPaperCycleTool.cs`、`FetchOhlcvTool.cs` |
| `src/Quant.Infra.Net.Mcp/DataSources/` | `IMcpSourceDataService`（抽象）、`Finnhub*` / `Fmp*` / `TwelveData*`（具体）、`McpSourceDataFactory`（工厂） |
| `src/Quant.Infra.Net.Mcp.Tests/` | 17 个测试（3 个 P3 paper-cycle、4 个 P4 fetch 边界、10 个 P2 backtest） |
| `docs/manual/mcp-server-en.md` / `-ch.md` | 本文档 |

> **护栏守住了：** MCP Server 是统一运行时**公共 API 的消费者**。
> `src/Quant.Infra.Net/`、`src/Quant.Infra.Net.Runtime/`、`src/Quant.Infra.Net.Orchestration*/`、
> `src/Quant.Infra.Net.Backtest*/`、`MyQuantApp/` 一个文件都没动。`git log --stat` 可验证。







