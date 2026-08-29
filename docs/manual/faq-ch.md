# 常见问题（中文）

> English: [faq-en.md](faq-en.md) · [索引](README-ch.md)

常见失败、它们的*含义*（是有意行为还是配置错误）以及怎么修。行文风格对齐 [../Manual.md](../Manual.md)
第 9 节。每条都对照本仓库当前源码核对过。

---

### Q1: "RunMode.Testnet/Live requires RuntimeOptions.BinanceApiKey/BinanceApiSecret" —— 为什么 Paper 不需要而 Testnet/Live 需要？

**原因**：`RunMode.Testnet` 和 `RunMode.Live` 都走**真实**币安 API（`BinanceUsdFutureService`），所以容器必须有凭据。
`RunMode.Paper` 用内存的 `PaperBinanceUsdFutureService`，从不联网，所以不需要。

这是**有意 fail-fast**（见 `Runtime/DependencyInjection.cs`）：

```csharp
var needsCredentials = runtimeOptions.RunMode is RunMode.Testnet or RunMode.Live;
if (needsCredentials
    && (string.IsNullOrWhiteSpace(runtimeOptions.BinanceApiKey)
        || string.IsNullOrWhiteSpace(runtimeOptions.BinanceApiSecret)))
{
    throw new NotSupportedException(
        $"RunMode.{runtimeOptions.RunMode} requires RuntimeOptions.BinanceApiKey/BinanceApiSecret " +
        "(fail-fast by design; this never silently degrades to Paper).");
}
```

**关键点**：容器**绝不会在 Live 缺凭据时静默回退到 Paper** —— 那会是"我本想实盘却悄悄跑了纸面"的危险陷阱。
看到这个，说明你只是没给凭据。

**修复**：
```csharp
services.AddQuantInfraNet(rt =>
{
    rt.RunMode = RunMode.Live;                       // 或 Testnet
    rt.BinanceApiKey = "your-api-key";
    rt.BinanceApiSecret = "your-api-secret";
});
```

### Q2: "Unknown Strategy 'XYZ'" —— `Parameters["Strategy"]` 填了不存在的策略名会怎样？

**原因**：编排层按名字解析策略。有两条解析路径，且都在启动期（容器构建时）**fail-fast**，而不是悄悄什么都不跑：

- **仅内置名**（`PairTradingZScore`、`MaCross`、`MeanReversion`）：由 `Orchestration/DependencyInjection.cs`
  的 `ISignalGenerator` 工厂解析。任何其他名字抛
  `ArgumentException("Unknown Strategy 'XYZ'. Supported values: PairTradingZScore | MaCross | MeanReversion.")`。
- **经 `IStrategyDescriptor` 注册的名字**（在你传入的 `strategyAssemblies` 之一里）：由 `StrategyCatalog`
  （反射扫描各程序集）解析。目录里没有的名字在启动时抛异常。

**所以未知策略名 = 启动崩溃并给出清晰消息**，而不是静默空跑。这是有意的：立刻抓到拼写/配置错误。

**修复**：
- 用合法内置名（`MaCross`、`MeanReversion`、`PairTradingZScore`），**或**
- 注册你自己的策略（见 [writing-a-strategy-ch.md](writing-a-strategy-ch.md)）并通过 `strategyAssemblies:` 传入其程序集，**或**
- 直接传一个 `customSignalGenerator` 实例。

### Q3: "DataSourceKind.Alpaca requires RuntimeOptions.AlpacaApiKey/AlpacaApiSecret" —— 那 `Custom` 没给实例呢？

**原因**：`Alpaca` 和 `Custom` 是两种**需要额外输入**才能构造的数据源种类，且都在缺输入时 **fail-fast**
（见 `DataSources/DataSourceFactory.cs`）：

```csharp
Models.DataSourceKind.Alpaca => !string.IsNullOrWhiteSpace(alpacaApiKey) && !string.IsNullOrWhiteSpace(alpacaApiSecret)
    ? new AlpacaTraditionalFinanceSourceDataService(alpacaApiKey!, alpacaApiSecret!)
    : throw new ArgumentException(
        "DataSourceKind.Alpaca requires RuntimeOptions.AlpacaApiKey/AlpacaApiSecret " +
        "(free tier: sign up at https://alpaca.markets — fail-fast by design, never silently falls back).",
        nameof(alpacaApiKey)),

Models.DataSourceKind.Custom => customDataSource ?? throw new ArgumentException(
    "DataSourceKind.Custom requires a custom ITraditionalFinanceSourceDataService instance " +
    "(pass it via AddQuantInfraNet / Create's customDataSource parameter).",
    nameof(customDataSource)),
```

**关键点**：工厂**绝不静默回退**到其他源（例如不会悄悄切到 `Demo` 或 `Yahoo`）。配置错误的 `Alpaca`/`Custom`
是启动期错误。

**修复**：
- `Alpaca`：设置 `rt.AlpacaApiKey` 与 `rt.AlpacaApiSecret`（免费档在 alpaca.markets）。
- `Custom`：传一个 `customDataSource:` 实例（见 [custom-data-source-ch.md](custom-data-source-ch.md)）。

### Q4: "我的 `customBroker` 在 Backtest 模式被忽略，这是 bug 吗？"

**不是 —— 这是有意设计**（见 [custom-broker-ch.md](custom-broker-ch.md) §5）。Backtest 路径是不同的代码路径
（`AddQuantInfraNetBacktest`，D1 机制），它：

1. 先把一个 `BacktestBrokerService` 实例注册为 `IBinanceUsdFutureService`（让编排层默认让位），
2. **根本不接收** `customBroker` 参数。

原因：回测执行必须由**模拟时钟/标记价**驱动（`IBacktestBroker` 面：`SetMarkPrices`、`SimulatedNowUtc`、
`DeferFills`、`FillTiming` `SameBarClose`/`NextBarOpen`，外加 `CommissionBps`/`SlippageBps`）。一个实盘风格的
`IExecutionBroker` 会绕过逐 bar 的成交语义与成本/滑点模型，破坏回测。

所以 `AddQuantInfraNet` 里的守卫：
```csharp
if (customBroker != null && runtimeOptions.RunMode != RunMode.Backtest)
    services.AddSingleton(customBroker);
```
**意味着：Backtest 下你的 `customBroker` 被有意丢弃。** 改用 `BacktestOptions`
（`InitialEquityUsd`、`CommissionBps`、`SlippageBps`、`FillTiming`）控制回测执行。

### Q5: 为什么我的风控拒绝了一个我以为允许的仓位？

**原因**：`DefaultRiskManager` **按序**检查**三条**规则，任一条失败即拒绝，并列出**全部**理由
（见 [risk-management-ch.md](risk-management-ch.md)）：

1. 单标的 `|TargetWeight| <= MaxWeightPerSymbol`（默认 **0.3**）。
2. `Σ|TargetWeight| <= MaxGrossExposure`（默认 **1.0**）。
3. 当前 `UnrealizedProfitRate` **不得**低于等于 `KillSwitchDrawdownRate`（默认 **−0.15**）；若低于则额外建议**全部清仓**。

注意第三条作用于**当前快照**（`PortfolioSnapshot.UnrealizedProfitRate`），不是你的新目标 —— 所以*现有*账本里的一次深度回撤，
即使你的*新*目标本身很小，也可能拒绝它。在一轮重启后快照被清零，该规则短期内更不易触发
（见 [testing-and-deployment-ch.md](testing-and-deployment-ch.md) §4）。

**修复/理解**：
```csharp
services.AddQuantInfraNet(o =>
{
    o.MaxWeightPerSymbol = 0.5;      // 抬高单标的上限
    o.MaxGrossExposure = 1.5;        // 抬高总敞口（杠杆）
    o.KillSwitchDrawdownRate = -0.25; // 放宽 kill-switch 带
});
```
或读 `assessment.Reasons`（每条字符串都点名了具体规则）看是哪条触发。

### Q6: 事件日志里出现 "no data available for 'SYM'" —— 这是错误吗？

**原因**：`SignalDataLoader.FetchAsync` 在你的数据源对该 symbol 返回**空**切片（或抛异常）时记一条 `DataLoad`
事件，然后返回**空收盘价序列**，策略据此降级为"无信号"，本轮正常完成。它是**数据降级**，不是崩溃 —— 管道继续。

**修复/理解**：
- 确认你的源确实在请求窗口内有该 symbol 的 bar（检查 `DownloadOhlcvListAsync`）。
- 若用 `DataSourceKind.Custom`，确保 `customDataSource` 对该 symbol 返回非空 `Ohlcvs`。
- 对应策略本轮不会出信号（它会加一条 `insufficient data for '{symbol}'` 事件）。

### Q7: 我的自定义阶段管道为什么没跑风控/执行阶段？

**原因**：传 `customStages:` 会**完全替代**默认八阶段。如果你的自定义序列里没有 `RiskStage`、`ExecutionStage`、
`PortfolioStateStage`，它们就根本不会跑。这是有意设计（见 [writing-a-strategy-ch.md](writing-a-strategy-ch.md)
关于自定义阶段的说明与 `CustomStagesPassthroughTests`）。

**修复**：把你需要的阶段放进序列，例如
```csharp
var stages = new IPipelineStage[]
{
    new DataIngestStage(source, broker),
    new AnalysisStage(),
    new SignalStage(generator),
    new TargetPositionStage(options),
    new RiskStage(risk, hub, store),
    new ExecutionStage(model, broker),
    new PortfolioStateStage(broker, store),
    new NotificationStage(hub),
};
services.AddQuantInfraNet(..., customStages: stages);
```
（各阶段的依赖从容器解析，或照编排层 `DependencyInjection.cs` 的默认那样构造。）

### Q8: 触发间隔从哪来，能在配置里设吗？

**简短回答**：它是**构造期注册**的，不从配置绑定。默认是
`new IntervalTrigger(StartMode.NextMinute, TimeSpan.Zero)`。要改节奏，在 `AddQuantInfraNetOrchestration`
之前注册你自己的 `IntervalTrigger`（见 [testing-and-deployment-ch.md](testing-and-deployment-ch.md) §3）。
**没有**对应的 `appsettings.json` 配置键。

### Q9: 管道会把任何东西写到磁盘吗？

**不会。** 默认 `IPortfolioStateStore`（`InMemoryPortfolioStateStore`）是纯内存（覆盖式语义），
没有内置持久化存储。重启后是空的。持仓每轮从券商侧重推导。若需持久状态，自己实现 `IPortfolioStateStore`
（见 [testing-and-deployment-ch.md](testing-and-deployment-ch.md) §4）。

### Q10: 我想用 Interactive Brokers / Charles Schwab —— 支持吗？

**尚未支持/不在范围**（见 [custom-broker-ch.md](custom-broker-ch.md) §6）：
- **Interactive Brokers**：`InteractiveBrokersService` 目前是**空壳**（每个方法都抛
  `NotImplementedException`）。InterReact TWS 协议客户端已内嵌在仓库
  （`src/Quant.Infra.Net/Broker/InterReact/`），但还没有任何东西把它接上。所以 IB 是*进行中*。
- **Charles Schwab**：面向管道*执行*的适配器有意留给 **Quant.Infra.Net.Pro** 仓库 —— 不在本开源面范围。

目前唯一完全打通的执行路径是**币安 USD-M 期货**（实盘/Paper/Backtest）。


