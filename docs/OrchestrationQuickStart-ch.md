# 编排层 Orchestration Layer —— 详细使用说明

> 本文档是 [编排层设计文档](OrchestrationLayerDesign.md)（完整契约）与 [中文](readme-ch.md) / [English](readme-en.md) README「编排层 Orchestration Layer（Beta）」小节的配套文档，回答那一节留下的问题：`dotnet run` 跑起来的 Demo 到底用的什么数据、什么标的、什么策略；以及如何换成自己的数据源、标的和策略。

---

## 1. "一分钟内跑起来"到底跑的是什么？

```bash
git clone https://github.com/memoryfraction/Quant.Infra.Net.git
cd Quant.Infra.Net/src
dotnet run --project Quant.Infra.Net.Orchestration.Console
```

这个命令**不会**联网、不会连真实券商、也不会拉真实行情。具体来说：

| 问题 | 答案 |
|---|---|
| **数据源** | `DemoTraditionalFinanceSourceDataService` —— 一个只用于 Demo 的进程内假实现，注册在 [`Program.cs`](../src/Quant.Infra.Net.Orchestration.Console/Program.cs) 里。它生成**确定性合成价格序列**（每次运行数字完全一样），从不调用 Yahoo Finance、Binance 或任何外部 API。 |
| **标的** | 单一标的 `AAPL`——但价格序列是**合成的**（稳定上升趋势 + 轻微噪声），不是真实苹果股价数据。用这个代码只是因为读者眼熟，不代表接了真实行情。 |
| **策略** | `MaCross`（经典 200 日均线趋势跟踪），配置在 [`appsettings.json`](../src/Quant.Infra.Net.Orchestration.Console/appsettings.json) 里。这是刻意选择的**单标的**默认策略（而不是双标的的 `PairTradingZScore`）——单标的意味着一条信号、一条目标仓位、一条执行报告，第一次接触的读者不用对照两条序列就能肉眼核对整个运行过程。 |
| **券商 / 执行** | `PaperBinanceUsdFutureService`——纯内存纸上交易，零网络请求。因为 `appsettings.json` 里 `"Environment": "Paper"`，这个实现会被自动注册。 |
| **通知** | 默认关闭（`"Notifications": { "Enabled": false }`），所以跑 Demo 不需要配置钉钉/企微/邮件的任何凭证。 |

一个周期的数据流：

```
DemoTraditionalFinanceSourceDataService（为 "AAPL" 生成 260 根合成日线）
        │
        ▼
DataIngestStage  → 装载行情并缓存到管道上下文
        ▼
AnalysisStage    → （MaCross 不需要它，均线自己在信号生成器内部算）
        ▼
SignalStage      → MaCrossSignalGenerator：收盘价 ≥ SMA(200) → Long
        ▼
TargetPositionStage → AAPL 目标权重 = +0.30（WeightPerSymbol，受 MaxWeightPerSymbol 封顶）
        ▼
RiskStage        → 检查单标的权重 / 总敞口 / 熔断阈值 → 通过
        ▼
ExecutionStage   → PaperBinanceUsdFutureService 在内存中开多头仓位
        ▼
PortfolioStateStage → 快照：权益=$10,000，持仓 1 笔
        ▼
NotificationStage → 若 Notifications.Enabled=true 会发送 Info 摘要
```

预期控制台输出（节选）：

```
[Signal] generator=MaCross: AAPL=Long (0.57)
[TargetPosition] targets: AAPL=0.30
[Risk] risk check passed
[Execution] execution done: 1/1 ok
[PortfolioState] snapshot saved: equity=10000.00 positions=1
```

---

## 2. 换成真实数据源

Demo 用 `DemoTraditionalFinanceSourceDataService` 纯粹是为了离线运行、每次结果一致。要接真实行情，只需在你自己的 `Program.cs` 里换掉这一处 DI 注册。

### 方式 A —— 用核心库自带的真实数据源

`Quant.Infra.Net`（核心包，非编排层）已经自带 `TraditionalFinanceSourceDataService`，通过 `pythonnet` 拉取 Yahoo Finance 数据（所需的 `pythonnet` 包与 Python 路径配置见根 [README 快速开始](../README.md)）。它依赖 `IHistoricalDataSourceService`——直接注册 CSV 实现，或改用 MySQL/MongoDB，可选实现见 [`Quant.Infra.Net/SourceData/Service/Historical/`](../src/Quant.Infra.Net/SourceData/Service/Historical/)：

```csharp
using Quant.Infra.Net.SourceData.Service;
using Quant.Infra.Net.SourceData.Service.Historical;

builder.Services.AddSingleton<IHistoricalDataSourceService, HistoricalDataSourceServiceCsv>();
builder.Services.AddSingleton<ITraditionalFinanceSourceDataService, TraditionalFinanceSourceDataService>();
// 删除 AddSingleton<ITraditionalFinanceSourceDataService, DemoTraditionalFinanceSourceDataService>() 这一行。
builder.Services.AddQuantInfraNetOrchestration();
```

`appsettings.json` 里 `"DataSource": "yahoo"`（已是默认值）不用改，管道就会去拉真实历史 K 线而不是合成数据。**`DataIngestStage`/信号生成器之后的所有阶段完全不受影响**——编排层只读取标准的 `Ohlcvs` 序列，根本不关心数据从哪来。

### 方式 B —— 自己实现数据源

自己实现 `ITraditionalFinanceSourceDataService`（比如包一层内部行情库、券商 REST 接口，或 CSV 导出管道），注册方式一样：

```csharp
public sealed class MyDataService : ITraditionalFinanceSourceDataService
{
    public Task<Ohlcvs> DownloadOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt,
        ResolutionLevel period = ResolutionLevel.Daily, DataSource dataSource = DataSource.YahooFinance)
    {
        // 在这里把你的行情转换成 Ohlcvs（HashSet<Ohlcv>）
    }
    // ……接口其余 4 个成员（见 ITraditionalFinanceSourceDataService）
}
```

```csharp
builder.Services.AddSingleton<ITraditionalFinanceSourceDataService, MyDataService>();
builder.Services.AddQuantInfraNetOrchestration();
```

编排层代码不需要改一行——`DataIngestStage` 和所有内置信号生成器只依赖这个接口。

---

## 3. 更换标的

纯配置改动——编辑 `appsettings.json`，不用改代码、不用重新设计逻辑：

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

若用双标的的 `PairTradingZScore` 策略，把 `Symbol` 换成 `SymbolA`/`SymbolB`：

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

（继续用 `DemoTraditionalFinanceSourceDataService` 的话，只有 `"AAA"`/`"BBB"`/其余任意标的（默认走上升趋势序列）才能产出有意义的合成信号，具体规则见 [`DemoTraditionalFinanceSourceDataService.cs`](../src/Quant.Infra.Net.Orchestration.Console/DemoTraditionalFinanceSourceDataService.cs)。一旦按第 2 节换成真实数据源，任何真实标的都可用。）

---

## 4. 定制或替换策略

### 方式 A —— 在 3 个内置策略间切换

改一个值，零代码：

| `Parameters.Strategy` | 风格 | 关键参数 |
|---|---|---|
| `MaCross`（Demo 默认） | 趋势跟踪 | `Symbol`、`FastPeriod`、`SlowPeriod`、`AllowShort` |
| `MeanReversion` | 均值回归 | `Symbol`、`LookbackBars`、`EntryZ`、`ExitZ`、`AllowShort` |
| `PairTradingZScore` | 统计套利 | `SymbolA`、`SymbolB`、`LookbackBars`、`ZScoreEntryThreshold`、`ZScoreExitThreshold`、`MinCorrelation`、`UseAdfFilter` |

完整参数契约见 [编排层设计文档 §9](OrchestrationLayerDesign.md#9-范例策略与端到端-demo)。

### 方式 B —— 接入自己的信号生成器

实现 `ISignalGenerator`（可选继承 `BaseSignalGenerator` 复用内置的数据装载/参数解析辅助方法），传给 `AddQuantInfraNetOrchestration`——会完全替代按策略名查找内置生成器的逻辑：

```csharp
public sealed class MyStrategy : ISignalGenerator
{
    public string Id => "MyStrategy";
    public Task<IReadOnlyList<Signal>> GenerateSignalsAsync(IPipelineContext context, CancellationToken ct)
    {
        // 你的信号逻辑；返回 Signal 对象（Symbol、Direction、Strength、Reason）
    }
}
```

```csharp
builder.Services.AddQuantInfraNetOrchestration(customSignalGenerator: new MyStrategy());
```

### 方式 C —— 替换整个阶段甚至整条管道

`AddQuantInfraNetOrchestration` 还接受 `customStages`（完全替代默认 8 阶段管道）和 `customExecutionModel`（替代 `RebalanceExecutionModel`）。当 8 阶段的形态本身不适合你的策略时用这个（比如需要自定义风控阶段，或完全跳过通知）：

```csharp
builder.Services.AddQuantInfraNetOrchestration(
    customStages: new IPipelineStage[] { new MyDataStage(), new MySignalStage(), new MyExecutionStage() });
```

---

## 5. 切到 Testnet 或实盘（必须刻意为之）

以上所有内容都不会碰到真实交易所——`Environment` 默认 `Paper`，纯内存券商。要切实盘必须**同时**显式完成以下两步；如果只改环境不注册真实券商，`AddQuantInfraNetOrchestration()` 会在启动时抛出 `NotSupportedException`：

```json
{ "Orchestration": { "Environment": "Testnet" } }
```

```csharp
// 必须在调用 AddQuantInfraNetOrchestration() 之前注册真实券商——该方法只会自动注册 Paper 模拟券商。
builder.Services.AddSingleton<IBinanceUsdFutureService>(sp => new BinanceUsdFutureService(/* 真实 API Key/Secret，Testnet 或 Live */));
builder.Services.AddQuantInfraNetOrchestration();
```

动手之前请先读 [编排层设计文档 §5.7](OrchestrationLayerDesign.md#57-事件与配置) 里的风控默认值——Demo 的 `appsettings.json` 故意用了偏宽松的风控上限（`MaxWeightPerSymbol: 0.5` 等），这是为单标的离线演示准备的，不一定适合真实资金。
