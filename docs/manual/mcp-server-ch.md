# MCP Server — AI Agent 接入（中文）

> English: [mcp-server-en.md](mcp-server-en.md) · [索引](README-ch.md)

Quant.Infra.Net 自带一个 **MCP（Model Context Protocol）服务器**。它是一个 .NET 工具，把研究、回测和纸面模拟能力暴露成 **MCP 工具**，让本地 AI（Claude Desktop、Claude Code、VS Code Copilot、Cursor、Cline……）用**自然语言**来驱动。你用一句话描述策略，就能拿到带图表的专业回测，然后在对话里继续迭代优化。

> **为什么是 MCP？** 越来越多的软件是被 AI agent 使用，而不是被人直接使用。MCP server 是你本地 AI 和 Quant.Infra.Net 之间的桥：不写 C#，不需要界面，一句话就够。

---

## 1. 它是什么（以及不是什么）

**工具：**

| 工具 | 作用 |
|---|---|
| `list_strategies` | 内置策略与参数：MaCross、MeanReversion、PairTradingZScore、**MultiFactor** |
| `list_data_sources` | 哪些数据源免费、要不要 key、覆盖范围、符号写法、已知限制 |
| `run_backtest` | 单次回测：指标与"买入持有"基准对比、市场状态拆解、年度收益、自动警告、按月采样的权益曲线，并生成**自包含的图文 HTML 报告** |
| `optimize_strategy` | 网格调参：在样本内搜索，每个组合都在**样本外**留出段上验证；给出样本内最优和**最稳健**的参数，并判断是否过拟合 |
| `walk_forward` | 滚动前推：每个训练窗口重新选参，用下一个没见过的窗口交易，再把各段样本外结果拼接起来 |
| `fetch_ohlcv` | 原始日线（每次 ≤ 500 根） |
| `run_paper_cycle` | 一次 Paper 管道周期（内存券商，零真实资金） |

**提示词模板**（支持 MCP prompts 的客户端里可一键启动）：`quickstart`（5 分钟上手）、`strategy_research`（从想法到稳健性验证全流程）、`regime_check`（风格切换诊断）。

**刻意不做（这是产品边界，不是 bug）：**

- **不提供 Testnet / 实盘下单。** 真实资金交易（币安合约、盈透、嘉信等）刻意**不**作为 MCP 工具暴露，真实下单将作为付费 Pro 功能走独立的商业计划。
- **不提供任意 shell / 文件系统访问。** 服务器唯一会写的文件是 HTML 报告，写到你的报告目录。

---

## 2. 安装（一行）

前提：[.NET 8 或更新的 SDK](https://dotnet.microsoft.com/download)。

```bash
dotnet tool install -g Quant.Infra.Net.Mcp
# 以后升级：dotnet tool update -g Quant.Infra.Net.Mcp
```

安装后 PATH 里会多一个 `quant-infra-mcp` 命令。它走 **stdio** 协议：没有 HTTP，不占端口，不需要额外基础设施。

装了 **.NET 10 SDK** 的话可以连安装都省掉，让客户端直接从 NuGet 运行：`dnx Quant.Infra.Net.Mcp --yes`。

开发者从源码运行：

```bash
git clone https://github.com/memoryfraction/Quant.Infra.Net.git
dotnet run --project Quant.Infra.Net/src/Quant.Infra.Net.Mcp
```

---

## 3. 客户端配置

**Claude Desktop**（`claude_desktop_config.json`；Windows 路径是 `%APPDATA%\Claude\claude_desktop_config.json`）：

```json
{
  "mcpServers": {
    "quant-infra-net": {
      "command": "quant-infra-mcp",
      "env": { "TIINGO_API_KEY": "可选的免费 key" }
    }
  }
}
```

装了 .NET 10、想免安装：`"command": "dnx", "args": ["Quant.Infra.Net.Mcp", "--yes"]`。

**Claude Code：**

```bash
claude mcp add quant-infra-net -- quant-infra-mcp
```

**VS Code / Copilot**（`.vscode/mcp.json`）：

```json
{ "servers": { "quant-infra-net": { "type": "stdio", "command": "quant-infra-mcp" } } }
```

重启客户端，应能看到 7 个工具、3 个提示词。

**可选环境变量**

| 变量 | 作用 |
|---|---|
| `TIINGO_API_KEY` | Tiingo 免费 key，美股数据比 Yahoo 更稳 |
| `QUANT_INFRA_REPORTS_DIR` | 报告目录（默认 `~/Documents/QuantInfraNet/reports`） |
| `QUANT_INFRA_CACHE_DIR` / `QUANT_INFRA_CACHE=off` | 数据缓存目录（默认 `%LOCALAPPDATA%/QuantInfraNet/cache`）/ 关闭缓存 |
| `FINNHUB_API_KEY` / `FMP_API_KEY` / `TWELVEDATA_API_KEY` | 旧数据源 |

---

## 4. 用自然语言驱动它：从想法到稳健结论

**快速上手：** 启动 `quickstart` 提示词，或者直接问：

> 用 Yahoo 数据，回测 SPY 的 200 日均线择时策略，从 2005 年到今天。和买入持有对比，给我报告。

**一段真实的多因子对话：**

> 在 SPY、QQQ、IWM、EFA、EEM、TLT、GLD、DBC 之间轮动，从 2008 年开始。每月持有过去 6 个月动量最强的 3 只，同等条件下优先选波动小的；SPY 跌破 200 日均线时切到 SHY。以 SPY 为基准。

Agent 会调用 `run_backtest`，参数是 `strategy=MultiFactor, universe=..., factors="momentum=1,lowvol=0.5", topK=3, regimeSymbol=SPY, defensiveSymbol=SHY, benchmark=SPY, dataSource=Yahoo`。一次真实运行（2008-01 → 2026-11）的结果：

| | 策略 | SPY 买入持有 |
|---|---|---|
| 年化收益 | 7.6% | 11.2% |
| 夏普 | 0.68 | 0.64 |
| 最大回撤 | **−18.3%** | −51.9% |

同时返回市场状态表：在"震荡上行"里赚得最多；在"恐慌下跌"里亏钱，但比 SPY 少得多。还会给出 HTML 报告路径。接着你可以继续问：

> 持 3 只、看 6 个月动量，这个选择是不是过拟合了？用 TopK 2–4、动量 3/6/12 个月做一次滚动前推检验。

`walk_forward` 的结果：16 个样本外窗口里 75% 盈利，前推效率 0.64。但最优参数**不稳定**：16 个窗口选出了 7 种不同组合，拼接后的样本外夏普也低于 SPY。这就是诚实的结论：轮动用更低的收益换来了小得多的回撤，而调出来的最优参数并不持久。

### 4.1 HTML 报告

报告是单个文件，图表用内嵌 SVG 画，断网也能打开，可以直接发给别人。内容包括：

- 指标卡（与基准并排对比）
- "请先读这里"风险提示
- 权益 vs 基准曲线、回撤图
- **滚动 6 个月夏普**
- **市场状态表**
- 年度收益柱状图、月度收益热力图
- 统计表（Sortino、Beta/Alpha、持仓时间占比、换手率）
- 参数表、最近 100 笔成交

调参和滚动前推的报告还会附上网格表或窗口表。

### 4.2 风格切换：一个策略某段有效、换一段就失效，怎么办

动量崩溃、趋势策略在震荡市里来回打脸、均值回归在单边市里亏钱，这些都是常态。工具分四层应对：

1. **诊断：** 每次回测都把日收益拆成"牛/熊"（基准相对 200 日均线）×"平稳/动荡"（20 日实现波动率相对中位数）四种状态，并画滚动 6 个月夏普，让你看清优势在**哪段时间**消失。
2. **验证：** `optimize_strategy` 对每个组合同时给出样本内和样本外的结果。推荐的是两者中较差那个最好的组合，而不是样本内峰值；样本内冠军在样本外亏钱时会直接报警。
3. **自适应：** `walk_forward` 每隔 `testMonths` 个月，用之前 `trainMonths` 个月的数据重新选参。拼接起来的样本外曲线，就是"持续自适应调参"实际能拿到的收益；参数稳定性和前推效率（样本外/样本内）告诉你重新调参是真有用，还是在追逐噪音。
4. **防御：** `MultiFactor` 可以避免押注单一风格：多因子混合（动量 + 低波 + 趋势），绝对动量过滤（动量为负的不持有），市场状态过滤（`regimeSymbol` 跌破均线就切到现金或 `defensiveSymbol`）。

### 4.3 多因子

`MultiFactor` 是**横截面**多因子模型。每个调仓日（每 `RebalanceDays` 根 K 线）对 `Universe` 中每个标的计算各个因子，在全部标的之间做 z-score 标准化，按 `Factors` 权重加总，持有得分最高的 `TopK` 只，等权。

| 因子 | 定义 |
|---|---|
| `momentum` | 过去 `MomentumLookback` 根 K 线的收益（默认 126），可跳过最近 `SkipRecent` 根（经典 12-1 动量：`MomentumLookback=252;SkipRecent=21`） |
| `lowvol` | 负的日收益波动率（`VolLookback`，默认 63） |
| `trend` | 收盘价 / SMA(`TrendPeriod`，默认 200) − 1 |
| `reversal` | 负的最近 5 根收益（短期反转） |
| `riskadjmom` | 动量 ÷ 波动率 |

只使用截至当下的数据，没有未来函数。估值、质量等基本面因子需要财报数据，暂未内置。

### 4.4 需要知道的默认设置

- **自动预热：** 自动下载 `startDate` 之前的历史，指标从第一天就能用；绩效只从 `startDate` 开始统计。
- **默认按下一根 K 线开盘价成交**（`fillAtNextOpen=false` 改为按信号 K 线收盘价成交）。
- **仓位：** 单标的策略 100%，配对每条腿 50%，多因子每只 100%/TopK（可用 `positionSizePct` 覆盖）。
- **成本：** 每笔成交手续费 5 bps + 滑点 2 bps。
- **已知限制：** 每个持仓权重相同（100%/TopK），所以市场状态过滤切到 `defensiveSymbol` 时，只有 1/TopK 的资金在防御资产里，其余是现金。想让防御资产满仓，可以用 `positionSizePct` 调整。
- **速度：** 数据有缓存。单只 ETF、15 年回测约 10 秒；9 只 ETF、18 年多因子约 1 分钟；滚动前推约 1–2 分钟。

---

## 4.5 工具参数速查

### run_backtest
| 参数 | 说明 |
|---|---|
| `strategy` ✅ | MaCross / MeanReversion / PairTradingZScore / MultiFactor |
| `startDate` ✅、`endDate` ✅ | UTC 日期；`endDate` 可写 `today` |
| `symbol` | 单标的策略 |
| `symbolA`、`symbolB` | 配对交易 |
| `universe`、`factors`、`topK`、`rebalanceDays`、`regimeSymbol`、`defensiveSymbol` | 多因子 |
| `parameters` | 其他任意策略参数：`"Key=Value;Key=Value"` 或 JSON 对象 |
| `benchmark` | 基准标的（默认：交易标的买入持有，或候选池等权） |
| `dataSource`、`apiKey`、`localFilePath` | 见第 5 节 |
| `initialEquityUsd`、`commissionBps`、`slippageBps`、`positionSizePct`、`killSwitchDrawdownPct`、`fillAtNextOpen` | 模拟设置 |
| `fastPeriod`、`slowPeriod`、`lookbackBars`、`entryZ`、`exitZ`、`allowShort` | 经典策略参数 |
| `report` | 是否生成 HTML 报告（默认是） |

### optimize_strategy
策略和数据参数同上，另加：`grid` ✅（如 `"SlowPeriod=50,100,150,200; FastPeriod=1..41:20"`；值本身含逗号时用 `|` 分隔）、`objective`（sharpe / cagr / calmar / maxdrawdown）、`testFraction`（默认 0.3）、`maxCombinations`（默认 60）。

### walk_forward
策略和数据参数同上，另加：`grid`（可选）、`trainMonths`（默认 36）、`testMonths`（默认 12）、`objective`、`maxCombinations`（默认 40）。

### list_strategies · list_data_sources
无参数，不联网。

### fetch_ohlcv
`symbol`、`startDate`、`endDate`、`dataSource`、`apiKey`、`localFilePath`。每次最多 500 根。

### run_paper_cycle
`strategy`、`symbol` / `symbolA` / `symbolB`、策略参数。返回事件流，不联网。

---

## 5. 数据源

> **数据是回测的头号痛点。** MCP server 现在默认使用**免费、免 key** 的数据源，所有数据落盘缓存，并且只依赖一个抽象接口（`IMcpSourceDataService`）：某个数据源坏了，换掉它，不用改任何工具。

### 5.1 用哪个

| 数据源 | 费用 / key | 覆盖 | 说明 |
|---|---|---|---|
| ⭐ **Yahoo** | 免费、**免 key** | 美股、港股（`0700.HK`）、A 股（`600519.SS`）、ETF、指数（`^GSPC`）、外汇（`EURUSD=X`）、期货（`GC=F`） | OHLC 已按分红和拆股复权（总收益口径）；非官方接口，可能限流（有缓存兜底）；个股有幸存者偏差 |
| ⭐ **Binance** | 免费、**免 key** | 加密现货：`BTCUSDT`、`ETHUSDT`……（`BTC`、`BTC-USD` 会自动转换） | 走公开行情镜像 `data-api.binance.vision`；一年约 365 根 |
| **Tiingo** | 免费 key（`TIINGO_API_KEY`） | 美股、ETF，约 30 年，已复权 | 比 Yahoo 稳定 |
| **LocalFile** | 免费 | 你自己的 CSV（`date,open,high,low,close,volume[,adjusted_close]`）或 JSON | 多标的时传一个放着 `SYMBOL.csv` 的文件夹，或 `C:/data/{symbol}.csv` 这样的模板 |
| **Demo** | 免费 | 离线合成数据 | 只用于冒烟测试 |
| Finnhub / Fmp / TwelveData | 免费 key | 美股 | 为兼容保留；免费档限制多（Finnhub 的历史 K 线可能需要付费） |

> **Stooq 刻意不提供**（实测不稳定：反爬验证、间歇性失败）。

### 5.2 API Key

只有 Tiingo（和几个旧数据源）需要 key。服务器按这个顺序查找：
1. 工具参数里显式传入的 `apiKey`
2. 可执行文件旁边的 `appsettings.json`（`QuantInfraNet:DataSources:{Tiingo|Finnhub|Fmp|TwelveData}:ApiKey`）
3. 环境变量（如 `TIINGO_API_KEY`），最简单的做法是写在客户端配置的 `env` 里

缺 key 时会返回明确的错误信息，告诉你去哪里注册，以及可以改用哪个免 key 的数据源。

### 5.3 缓存

数据按"数据源 + 标的"缓存。终点在 3 天前的区间永不过期，最近的区间 12 小时后过期。所以在对话里反复迭代（"试试 150 日""加上 GLD"）几秒就能出结果，也不会撞到限流。

### 5.4 SOLID，一段话讲清

`IMcpSourceDataService`（抽象）只有一个方法：`DownloadDailyAsync(symbol, start, end)`。每个数据源各自实现它；`CachingSourceDataService` 可以包装其中任意一个；`McpSourceDataFactory`（单一职责：把名字映射成数据源）负责创建。工具**只依赖接口**，新增一个数据源只需要加一个类、一个枚举值。

---

## 6. 边界，直白地说

这个 MCP Server 是**只读 + 模拟**的：

- **读：** K 线、策略目录、回测指标与报告、paper 事件流。
- **模拟：** 每次调用跑一次 Paper 管道周期（内存券商、零网络、零资金）。
- **永不：** 实盘下单、使用真实交易所的 API Key、暴露 Testnet/Live。

> **为什么画这条线？** Quant.Infra.Net 的核心承诺是：一个策略文件加一个配置文件，就能走完"想法 → 回测 → 纸面盘 → 实盘"。MCP 通道是这个漏斗**最上面的一段**：Agent（或人）在投入任何真实资金**之前**做探索、对比和决策。真实下单是**付费 Pro 功能**，走独立商业计划，不走免费的 MCP 通道。这就是产品真实的样子。

---

## 7. Agent Prompt 模式（直接复制粘贴）

下面是起点，按你的问题改标的、窗口、策略或数据源即可。全部使用**免费、免 key** 的数据。

### 7.1 单策略回测 + 报告

> 用 Yahoo 数据，回测 **QQQ** 的 **MaCross**（slowPeriod=200），从 **2005-01-01 到今天**。和买入持有对比，用大白话解释市场状态表，给我报告路径。

### 7.2 同窗口对比两个策略

> 用 Yahoo，**SPY** 从 2010 年到今天，对比 **MaCross**（slowPeriod=200）和 **MeanReversion**（lookbackBars=20, entryZ=1.5, exitZ=0.3）。用一张表列出年化收益、夏普、最大回撤、交易次数，并告诉我哪个更适合怕回撤的人。

### 7.3 多因子轮动

> MultiFactor，候选池 **SPY,QQQ,IWM,EFA,EEM,TLT,GLD,DBC**，因子 momentum=1,lowvol=0.5，持 3 只，月度调仓，regimeSymbol 用 SPY，defensiveSymbol 用 SHY，基准 SPY，Yahoo 数据，2008 年到今天。

### 7.4 调参不自欺

> optimize_strategy：QQQ 的 MaCross，网格 SlowPeriod=50..250:25，Yahoo，2005 年到今天，目标夏普。最优参数是在一片"高原"上，还是一个孤立的尖峰？样本外还成立吗？

### 7.5 能不能扛住风格切换

> 对上面的多因子做 walk_forward，网格 "TopK=2,3,4; MomentumLookback=63,126,252"，训练 36 个月、测试 12 个月。有多少个窗口盈利？选出的参数稳不稳定？拼接后的样本外曲线能不能跑赢 SPY？

### 7.6 加密货币

> 用 Binance 数据，回测 BTC 的 MaCross（slowPeriod=100），2019 年到今天；再对 BTC,ETH,SOL,BNB,XRP 做 MultiFactor，持 2 只，regimeSymbol 用 BTC。

### 7.7 自己的数据（LocalFile）

> 用 LocalFile，文件夹 `E:/data/daily`（每个标的一个 SYMBOL.csv），对 AAA,BBB,CCC,DDD 做 MultiFactor，2016 到 2020 年。

> **为什么用 LocalFile？** 结果完全确定，不联网，不需要 API key。适合 CI、教学和私有数据。

---

## 8. 排错

| 现象 | 可能原因 | 解决 |
|------|----------|------|
| 客户端里看不到 `quant-infra-net` | 工具不在 PATH 上，或没装 .NET SDK | 运行 `dotnet --version`、`dotnet tool list -g` 检查；装好后重启客户端；.NET 10 可以改用 `dnx` 配置 |
| `Yahoo request failed (HTTP 429)` | 被 Yahoo 限流 | 等一分钟（已缓存的区间不会重新拉），或改用 Tiingo（免费 key）/ LocalFile |
| `API key is required for Tiingo` | 没有 key | 在客户端配置的 `env` 里加 `TIINGO_API_KEY`，或改用 Yahoo |
| `No bars between …` | 该窗口内标的还不存在，或符号写法不对 | 用 `list_data_sources` 核对写法（如 `0700.HK`、`BTCUSDT`） |
| 多标的长窗口 `walk_forward` 很慢 | 引擎逐根 K 线跑完整管道 | 缩短窗口、缩小网格或减少标的；有缓存后重跑会更快 |
| `fetch_ohlcv` 返回 `truncated: true` | 窗口超过 500 根 | 这是设计上限，拆成几段窗口 |
| `run_backtest` 报 `Unknown strategy` | 策略名拼错 | 用 `list_strategies` 里的名字 |

---

## 9. 在仓库里的位置

| 路径 | 内容 |
|------|------|
| `src/Quant.Infra.Net.Mcp/` | MCP server（.NET 工具 `quant-infra-mcp`，stdio，NuGet 包类型 `McpServer`，`.mcp/server.json`） |
| `src/Quant.Infra.Net.Mcp/Tools/` | `RunBacktestTool`、`OptimizeTools`（optimize_strategy + walk_forward）、`ListDataSourcesTool`、`ListStrategiesTool`、`FetchOhlcvTool`、`RunPaperCycleTool` |
| `src/Quant.Infra.Net.Mcp/Engine/` | `BacktestEngine`（数据只加载一次、自动预热、运行）、`ResearchService`（请求解析、参数网格、并行回测、结果拼接） |
| `src/Quant.Infra.Net.Mcp/Strategies/` | `MultiFactorRotation`（由运行时的 `StrategyCatalog` 自动发现） |
| `src/Quant.Infra.Net.Mcp/Reporting/` | `PerformanceAnalytics`（基准、市场状态、月度/年度收益）、`HtmlReportWriter`（内嵌 SVG 报告） |
| `src/Quant.Infra.Net.Mcp/DataSources/` | `IMcpSourceDataService`，`Yahoo*` / `Binance*` / `Tiingo*` / `LocalFile*` 及旧数据源，`CachingSourceDataService`，`McpSourceDataFactory` |
| `src/Quant.Infra.Net.Mcp/Prompts/` | MCP 提示词：`quickstart`、`strategy_research`、`regime_check` |
| `src/Quant.Infra.Net.Mcp.Tests/` | 离线测试；另有一个联网冒烟测试，设置 `QUANT_INFRA_LIVE_TESTS=1` 才运行 |

> **护栏已遵守：** MCP server 只作为统一运行时公共 API 的**消费者**。`src/Quant.Infra.Net/`、`src/Quant.Infra.Net.Runtime/`、`src/Quant.Infra.Net.Orchestration*/`、`src/Quant.Infra.Net.Backtest*/`、`MyQuantApp/` 下没有任何文件被修改，可用 `git log --stat` 核实。
