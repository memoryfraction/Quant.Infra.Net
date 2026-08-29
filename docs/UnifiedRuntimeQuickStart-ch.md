# 统一运行时 Unified Runtime — 五分钟上手

> 本文是 [统一运行时设计 R0–R6](TradingRuntimeDesign.md) 的上手篇：`dotnet run` 一条命令背后到底跑了什么、唯一的开关在哪里、怎么换数据源/策略/凭据。前置阅读：[核心库使用说明](readme-ch.md)。

---

## 1. 一条命令跑起来

```bash
cd Quant.Infra.Net/src
dotnet run --project Quant.Infra.Net.Runtime.Console
```

默认 `appsettings.json` 里 `"RunMode": "Backtest"`，这条命令**完全离线**（Demo 合成数据、内存会计、零网络），打印回测绩效报告：

```
Backtest complete: 260 bars, 4 trades
CAGR=13.56%   Sharpe=0.54   Calmar=0.00
MaxDrawdown=0.00%   WinRate=100.0%   ProfitFactor=∞   Commission=0 USD
```

## 2. 唯一的开关：`Runtime:RunMode`

同一个 `Program.cs`、同一套 `appsettings.json`，四个模式只改一个值：

| `RunMode` | 驱动 | 数据 | 成交/券商 | 说明 |
|---|---|---|---|---|
| `Backtest` | `BacktestRunner` 逐 bar 回放 | Demo 合成（或你换的源） | `BacktestBrokerService`（内存，`WarmupBars` 前零仓位） | 架构级零前视：回放只喂 `bars[i..]` |
| `Paper` | `PipelineRunner` 八阶段一次 | 同上 | `PaperBinanceUsdFutureService`（内存） | 实时时钟；事件流全部可见 |
| `Testnet` | 同 `Paper` | 需真实数据源 | **需真实 API 凭据** | 凭据为空 → 启动即抛 `NotSupportedException`（设计如此，不会静默掉到 Paper） |
| `Live` | 同 `Paper` | 需真实数据源 | **需真实 API 凭据** | 同上 |

`Testnet`/`Live` 在示例宿主（凭据留空）下运行会**立即失败**——这是护栏：Demo 永远不许碰真实资金，想切实盘请写自己的宿主程序并在其中注册真实券商与数据源（凭据不得入库，见[代码规范](CodeStandard.md)）。

```json
{ "Runtime": { "RunMode": "Paper", "DataSource": "Demo" } }
```

## 3. 默认 Demo 是什么

| 项 | 默认值 | 位置 |
|---|---|---|
| 数据 | `DeterministicDemoSourceDataService`——确定性合成日线（每次运行数字完全一样） | `Quant.Infra.Net.Runtime/DataSources/` |
| 标的 | `AAPL`（合成上升趋势序，`260` bar；`AAA`/`BBB` 是合成配对腿） | `Orchestration:Parameters:Symbol` |
| 策略 | `MaCross`（`Fast=1`/`Slow=200`，权重 `0.3`） | `Orchestration:Parameters:Strategy` |
| 初始权益 | `10000` USD | `Orchestration:InitialEquityUsd` / `Backtest:InitialEquityUsd` |

## 4. 换策略（零代码 或 单文件）

**内置 3 个**——改一个值：`MaCross`（趋势）、`MeanReversion`（均值回归）、`PairTradingZScore`（统计套利，用 `SymbolA`/`SymbolB`）。参数表见[编排层设计 §9](OrchestrationLayerDesign.md#9-范例策略与端到端-demo)。

**单文件自定义**——仓库里已有一个可照抄的范例 [`Strategies/ExampleCustomStrategy.cs`](../src/Quant.Infra.Net.Runtime.Console/Strategies/ExampleCustomStrategy.cs)：一个 `IStrategyDescriptor` + 一个 `ISignalGenerator`，放进宿主程序集即被 `StrategyCatalog` 自动发现，不需要改 `Runtime` 层任何代码：

1. 复制该文件到你自己宿主项目的 `Strategies/` 目录，改类名/策略名；
2. `Parameters:Strategy` 改成你的策略名（如 `"ExampleCustom"`）；
3. 重跑——`dotnet run --project <你的宿主>`。

你的策略生成器只依赖 `IPipelineContext`；回放/实时两条路径由框架保证一致（见 R4 的 `ParityRegressionTests`）。

## 5. 换数据源

- **`Runtime:DataSource`**（`DataSourceFactory` 按种类实例化）：`Demo`（默认，离线合成）/ `Yahoo`·`Csv`（核心库 `TraditionalFinanceSourceDataService`，`IHistoricalDataSourceService` 缺省为 CSV，可换成你自己的）/ `Binance`（本层 `BinanceKlineSourceDataService` 适配，只读 K 线）/ `Custom`（**必须**向 `AddQuantInfraNet` 传入 `customDataSource` 实例——示例宿主未传入，所以配置成 `Custom` 会按设计 fail-fast 抛异常）。
- **你自己的宿主**：实现 `ITraditionalFinanceSourceDataService`，然后 `AddQuantInfraNet(..., customDataSource: new MyDataSource())` 或 `services.AddSingleton<ITraditionalFinanceSourceDataService, MyDataSource>()` 之后再调 `AddQuantInfraNet(...)`。

`Backtest` 与 `Paper` 走的是**同一个** `ITraditionalFinanceSourceDataService` 接口——换源对两条路径都生效，不需要改策略代码。

## 6. 配置速查（`appsettings.json`）

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

> ⚠️ `BinanceApiKey`/`BinanceApiSecret` 在入库版本里**必须留空**（代码规范第 9 条：敏感数据不进代码库）。真实凭据只允许出现在你本地私有配置或宿主程序的密钥管理里。
