# 回测引擎 Backtest Engine —— 详细使用说明

> 本文档是 [Trading Runtime Design](TradingRuntimeDesign.md)（完整契约，§7 / §9）与 [中文](readme-ch.md) / [English](readme-en.md) README「Backtest Engine（Beta）」小节的配套文档，回答那一节留下的问题：`dotnet run` 跑起来的 Demo 到底用的什么数据、什么标的、什么策略；怎么喂真实历史数据；手续费/滑点/成交时机怎么配；怎么做参数扫描。

---

## 1. "一分钟跑起来"到底跑的是什么？

```bash
git clone https://github.com/memoryfraction/Quant.Infra.Net.git
cd Quant.Infra.Net
dotnet run --project src/Quant.Infra.Net.Runtime.Console
```

> R6 起统一宿主：`Quant.Infra.Net.Runtime.Console`（原独立回测 Demo 宿主已退役；本命令默认 `Runtime:RunMode = "Backtest"`，与本文档描述的回测路径等价）。

这个命令**不会**联网、不会连真实券商、也不会拉真实行情。具体来说：

| 问题 | 答案 |
|---|---|
| **数据源** | [`Quant.Infra.Net.Runtime`](../src/Quant.Infra.Net.Runtime/DataSources/DemoSyntheticSourceDataService.cs) 里的 `DemoSyntheticSourceDataService`（`Runtime:DataSource = "Demo"` 时由 `DataSourceFactory` 实例化）——确定性**合成日线**，不经过 Yahoo/Binance/CSV 任何真实渠道。 |
| **标的** | 单一标的 `AAPL`——只是给合成序列一个读者眼熟的代号（`AAA`/`BBB` 是合成配对腿）。 |
| **策略** | `MaCross`（默认 `FastPeriod=1`, `SlowPeriod=200`）——经典均线趋势；260 根合成 bar 中约 200 根后开始出信号。 |
| **券商 / 执行** | `BacktestBrokerService`——仅回测使用的 `IBinanceUsdFutureService` 记账实现。纯内存；**记账口径与 Paper 券商完全一致**，外加手续费/滑点记账与只增不减的成交日志。 |
| **网络** | 零。每根 bar 都走真实的 8 阶段 `StrategyPipeline`（`数据采集 → 统计分析 → 信号生成 → 目标仓位 → 风控前置检查 → 执行调仓 → 组合状态更新 → 通知推送`），但数据采集阶段只从内存中的 `HistoricalDataSet` 读取。 |
| **指标** | `BacktestResult.Metrics`——CAGR / 夏普 / 卡尔玛 / 最大回撤（复用既有 `StrategyPerformanceAnalyzer`）+ 交易层胜率 / 盈亏比 / 总手续费（`TradeStatistics`）。 |

预期控制台输出（节选，确定性）：

```
Backtest complete: 260 bars, 4 trades
CAGR=13.56%   Sharpe=0.54   Calmar=0.00
MaxDrawdown=0.00%   WinRate=100.0%   ProfitFactor=∞   Commission=0 USD
```

> 260 根输入 bar、`WarmupBars=0`（默认），`MaCross`（Slow=200）约 200 根后开始出信号；Demo 一次性打印 9 项指标（盈亏比在零亏损样本下输出 ∞）。

---

## 2. 喂真实历史数据

Demo 用进程内合成的 `Ohlcv` 列表构建 `HistoricalDataSet`，纯粹为了离线运行。要接真实数据，用你自己的 K 线构建同一个类即可——下游（管道 / 记账 / 指标）完全不变：

```csharp
using Quant.Infra.Net.Backtest;
using Quant.Infra.Net.Backtest.Data;
using Quant.Infra.Net.Backtest.Runner;
using Quant.Infra.Net.SourceData.Model;

// 你自己的数据源：CSV 装载器、核心库的 ITraditionalFinanceSourceDataService、数据库等。
//（一次性、在回测之外预取——绝不放在回测循环里）
var bars = await MyDataLoader.LoadDailyAsync("AAPL", from, to);   // → IReadOnlyList<Ohlcv>

var data = new HistoricalDataSet(new Dictionary<string, IReadOnlyList<Ohlcv>>
{
    ["AAPL"] = bars,
});
```

要点：

- **无需预先排序**——`HistoricalDataSet` 内部对每支序列排序，并构建全部标的时序戳的并集 `Timeline`（升序）。
- **防未来函数是结构性保证**：runner 每根 bar 先把收盘价标记进 broker、再只针对该根 bar 调用管道；信号生成器看到的缓存序列（`SliceUpTo`）最多到**当前**这根 bar，永远看不到未来。`LookAheadBiasTests`（B1）把它钉死。
- **多标的**（如 `PairTradingZScore`）：每个 symbol 放一支序列；runner 走并集时间线，缺数据的 symbol 在缺失 bar 上自然没有标记价/成交。

在自己的宿主里跑：

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

## 3. 手续费、滑点与成交时机（`BacktestOptions`）

5 个选项都是纯配置，通过 `configureBacktest` 回调设置：

| 选项 | 默认值 | 含义 |
|---|---|---|
| `InitialEquityUsd` | `10000` | 初始账户权益（USD）。 |
| `WarmupBars` | `0` | 前 N 根 bar 不交易（仅指标预热；权益曲线仍记录）。 |
| `CommissionBps` | `0` | 手续费（基点），按成交名义价值扣减（逐条记入 `Trades[].CommissionUsd`，并汇总进 `Metrics.TotalCommissionUsd`）。 |
| `SlippageBps` | `0` | 成交价相对标记价朝不利方向偏移（买更贵、卖更便宜）。 |
| `FillTiming` | `SameBarClose` | `SameBarClose`：信号 bar 收盘价成交（默认）；`NextBarOpen`：**下一**根 bar 开盘价成交——对"用收盘价算信号"的因果诚实模式。 |

```csharp
services.AddQuantInfraNetBacktest(
    configureBacktest: b =>
    {
        b.InitialEquityUsd = 50000m;
        b.CommissionBps = 4m;      // 0.04 %
        b.SlippageBps = 2m;        // 0.02 % 不利方向
        b.FillTiming = Quant.Infra.Net.Backtest.Models.FillTiming.NextBarOpen;
    },
    o => { ... });
```

`CommissionBps = SlippageBps = FillTiming = SameBarClose`（全默认）时，记账结果与编排层 Paper 券商**数值完全一致**——这条交叉印证由 `BacktestBrokerServiceTests`（零成本对等用例，B2）钉死。

---

## 4. 切换或替换策略

扩展面与编排层完全一致——回测驱动的就是**同一条** `StrategyPipeline`：

- **内置策略**：`o.Parameters["Strategy"]` 设为 `"MaCross"` / `"MeanReversion"` / `"PairTradingZScore"` 三选一（三者均有 `B5EndToEndTests` 端到端覆盖）。
- **自己的信号生成器**：传 `customSignalGenerator`——完全替代按策略名查找内置生成器的逻辑，且**同一个类**稍后可以直接用于 Paper：

```csharp
services.AddQuantInfraNetBacktest(customSignalGenerator: new MyRsiSignalGenerator());
```

- **自定义风控 / 执行模型**：底层仍走 `AddQuantInfraNetOrchestration(customStages:…, customExecutionModel:…)`，同样可用——回测通过同一个 DI 入口组装管道。

---

## 5. 参数扫描（`ParameterSweepRunner`）

对参数组合做网格扫描，各点之间**零共享状态**——每个点都有**独立的** broker、**独立的** DI 容器和全新的 `BacktestRunner`：

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
    .OrderByDescending(r => r.Backtest.Metrics.SharpeRatio)  // 或任何其他指标
    .First();
```

底层是 `Parallel.ForEachAsync`；结果按**网格顺序**落位，与哪个点先跑完无关。`ParameterSweepRunnerTests`（B4）验证 3×3 = 9 个互不干扰的点、以及同一点重复运行的确定性。

---

## 6. 护栏（引擎**不会**做的事）

| 护栏 | 原因 |
|---|---|
| 回放循环内零网络请求 | 所有数据必须在回测**之前**一次性物化进 `HistoricalDataSet`（§11.11）。 |
| 无未来函数 | `SliceUpTo(symbol, asOfUtc)` 保证可见历史最多到当前 bar。 |
| `Environment` 强制 `Paper` | `AddQuantInfraNetBacktest` 会覆盖 `OrchestrationOptions.Environment`——不可能误把实盘券商接进回测。 |
| 无向量化批处理路径 | 每根 bar 都是一次事件驱动 `StrategyPipeline.RunAsync` 调用——与 Paper 下运行的是**完全相同的代码**（§11.5）。 |
| 管道里无 RunMode 字段 | 策略代码根本不知道自己正被回测——因此不可能写出 `if (isBacktest)` 分支（§11.2）。 |
| 既有模块零改动 | `Quant.Infra.Net*`、`.Orchestration*`、`.Console`、`MyQuantApp` 全部只读依赖；本引擎纯增量（§11.1）。 |
