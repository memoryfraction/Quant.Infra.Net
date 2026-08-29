# 测试与部署（中文）

> English: [testing-and-deployment-en.md](testing-and-deployment-en.md) · [索引](README-ch.md)

本指南覆盖上线前最常遇到的两个问题：**怎么证明你的策略/券商/风控逻辑是对的**，以及**真的跑起来
（Paper/Live）长跑时发生了什么**。下面的每个模式都来自本仓库测试套里已有的代码，代码片段可直接复制。

---

## 1. 给你的策略写单元测试

最便宜、最可靠的测试是：在一个**完全可控**的 `ServiceCollection` 容器里**跑一轮管道**，配一个
**确定性的内存数据源**，然后断言管道写出的 `Signal` / `TargetPosition` / `RiskAssessment` 槽位。
`src/Quant.Infra.Net.Runtime.Tests/ParityRegressionTests.cs`（`FixedSeriesSource` + `RunOneBar`）就是这么做的，且无需联网。

### 最小可运行骨架（MSTest）

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using Quant.Infra.Net.Runtime;
using Quant.Infra.Net.Runtime.Models;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

[TestClass]
public sealed class MyStrategyTests
{
    private const string Symbol = "TEST";

    [TestMethod]
    public async Task MyStrategy_Emits_Expected_Signal_On_Known_Series()
    {
        // 1) 确定性夹具：250 根日线，收盘价 = 100 + 0.5*i（严格递增）。
        var bars = new List<Ohlcv>();
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 250; i++)
        {
            var close = 100m + 0.5m * i;
            bars.Add(new Ohlcv
            {
                Symbol = Symbol,
                OpenDateTime = start.AddDays(i),
                CloseDateTime = start.AddDays(i + 1),
                Open = close - 1m, High = close + 1m, Low = close - 2m,
                Close = close, Volume = 1000m,
            });
        }
        var series = new Dictionary<string, IReadOnlyList<Ohlcv>> { [Symbol] = bars };
        var fixedSource = new FixedSeriesSource(series); // 见 §1.2

        // 2) Paper 模式 + 你的固定数据源 + 被测策略的容器。
        var services = new ServiceCollection();
        services.AddQuantInfraNet(
            rt => { rt.RunMode = RunMode.Paper; rt.DataSource = DataSourceKind.Custom; },
            configureOrchestration: o =>
            {
                o.Parameters["Strategy"] = "MyStrategy"; // 或内置策略如 "MaCross"
                o.Parameters["Symbol"] = Symbol;
                o.Parameters["WeightPerSymbol"] = "0.3";
            },
            customDataSource: fixedSource);
        using var sp = services.BuildServiceProvider();

        // 3) 只跑一轮管道。
        var pipeline = sp.GetRequiredService<StrategyPipeline>();
        var options = sp.GetRequiredService<OrchestrationOptions>();
        var context = new PipelineContext(runId: 1, parameters: options.Parameters);

        // 预注入全量序列为"合并槽"（BacktestRunner 每 bar 都这么干）。
        var merged = new HashSet<Ohlcv>(bars);
        context.Set(merged);

        await pipeline.RunAsync(context, CancellationToken.None);

        // 4) 断言槽位。
        var signals = context.Get<IReadOnlyList<Signal>>() ?? Array.Empty<Signal>();
        var targets = context.Get<IReadOnlyList<TargetPosition>>() ?? Array.Empty<TargetPosition>();
        var risk = context.Get<RiskAssessment>();

        Assert.AreEqual(1, signals.Count, "expected exactly one signal");
        Assert.AreEqual(SignalDirection.Long, signals[0].Direction, "rising series should go long");
        Assert.IsNotNull(risk);
        Assert.IsTrue(risk.Approved, string.Join("; ", risk.Reasons));
    }
}
```

**为什么这个形状成立：**
- `RunMode.Paper` + `DataSourceKind.Custom` + `customDataSource` 实例，给你一个完全离线、确定性的容器（无网络、无真实券商）。
- 预注入 `HashSet<Ohlcv>` 合并槽，使 `SignalDataLoader.HasCachedSeries` 返回 `true`，**不会触发任何数据源回退调用** —— 你精确控制策略看到什么。
- 跑一轮 `pipeline.RunAsync` 会执行默认八阶段，所以你在测的是真实的 `TargetPositionStage`、`RiskStage`、`ExecutionStage`、`PortfolioStateStage`，而不是 mock。

### 1.2 `FixedSeriesSource` 助手（可从 `ParityRegressionTests.cs` 复制）

一个最小的 `ITraditionalFinanceSourceDataService`，返回一个固定切片，其余成员抛 `NotSupportedException`。
关键洞见（见 M4 指南）：**管道真正用的只有 `DownloadOhlcvListAsync`**，其余可以抛。

```csharp
public sealed class FixedSeriesSource : ITraditionalFinanceSourceDataService
{
    private readonly Dictionary<string, IReadOnlyList<Ohlcv>> _series;
    public FixedSeriesSource(IReadOnlyDictionary<string, IReadOnlyList<Ohlcv>> series)
        => _series = series.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

    public Task<Ohlcvs> DownloadOhlcvListAsync(string symbol, DateTime a, DateTime b,
        ResolutionLevel Period = ResolutionLevel.Daily, DataSource ds = DataSource.YahooFinance)
    {
        if (!_series.TryGetValue(symbol, out var all))
            return Task.FromResult(Empty(symbol));
        return Task.FromResult(new Ohlcvs
        {
            Symbol = symbol, ResolutionLevel = Period,
            StartDateTimeUtc = all[0].OpenDateTime, EndDateTimeUtc = all[^1].OpenDateTime,
            OhlcvSet = new HashSet<Ohlcv>(all),
        });
    }

    private static Ohlcvs Empty(string s) => new()
    { Symbol = s, StartDateTimeUtc = default, EndDateTimeUtc = default, OhlcvSet = new HashSet<Ohlcv>() };

    public Task<Ohlcvs> BeginSyncSourceDailyDataAsync(string s, DateTime a, DateTime b, string f,
        ResolutionLevel Period = ResolutionLevel.Daily) => throw new NotSupportedException();
    public Task<List<Ohlcv>> GetOhlcvListAsync(string f) => throw new NotSupportedException();
    public Task SaveOhlcvListAsync(IEnumerable<Ohlcv> l, string f) => throw new NotSupportedException();
    public Task<IEnumerable<string>> GetSp500SymbolsAsync(int n = 500) => throw new NotSupportedException();
}
```

## 2. 测自定义 `IRiskManager` 或 `IExecutionBroker`

不必搭完整管道。直接用 fake 构造被测单元，断言其输出：

```csharp
// IRiskManager：喂一个 PortfolioSnapshot + 目标仓位，断言 Approved/Reasons。
var risk = new DefaultRiskManager(new OrchestrationOptions
{
    MaxWeightPerSymbol = 0.3,
    MaxGrossExposure = 1.0,
    KillSwitchDrawdownRate = -0.15,
});
var snap = new PortfolioSnapshot
{
    AccountEquityUsd = 100_000m,
    UnrealizedProfitRate = -0.20, // 低于 kill-switch → 应拒绝并建议清仓
    ActualWeights = new Dictionary<string, double>(),
    TargetWeights = new Dictionary<string, double>(),
};
var targets = new[] { new TargetPosition { Symbol = "AAA", TargetWeight = 0.4 } }; // 超过 0.3 上限
var assessment = await risk.AssessAsync(targets, snap, CancellationToken.None);
Assert.IsFalse(assessment.Approved);
Assert.IsTrue(assessment.Reasons.Any(r => r.Contains("MaxWeightPerSymbol")));
Assert.IsTrue(assessment.Reasons.Any(r => r.Contains("kill-switch")));
```

```csharp
// IExecutionBroker：用 stub 驱动它，断言它本应发出的调用。
var broker = new MyFakeBroker(); // 你的适配器（见 custom-broker-*.md §4）
await broker.SetTargetWeightAsync("AAA", 0.5);
var pos = await broker.GetPositionsAsync();
Assert.AreEqual(1, pos.Count);
Assert.AreEqual("AAA", pos[0].Symbol);
await broker.LiquidateAsync("AAA");
Assert.AreEqual(0, (await broker.GetPositionsAsync()).Count);
```

**Backtest/Paper 一致性测试** —— 最强的回归是把*同一根 bar* 分别跑 Backtest 路径和 Paper 路径，断言
`Signal`/`TargetPosition`/`RiskAssessment` 槽位逐字段一致。`ParityRegressionTests.Parity_SameBar_Backtest_And_Paper_Fields_Are_Identical`
是参考实现（它还包含一个"自证有效"用例，故意注入一个有状态 bug 的策略，以证明比较器确实能照出分歧）。
用它作为你自己策略的模板。

## 3. 长跑 Paper/Live（`PipelineRunner` / `IntervalTrigger`）

Paper/Live 长跑驱动是 **`PipelineRunner`** 后台服务
（`src/Quant.Infra.Net.Orchestration/Pipeline/PipelineRunner.cs`），由通用宿主作为 `IHostedService` 启动
（在 `Orchestration/DependencyInjection.cs` 注册）。

- 它**启动即跑第一轮**，之后在每次 **`IntervalTrigger`** 触发时再跑。
- 每一轮都用**全新的 `PipelineContext`**（递增 `runId`）与**当前**的 `OrchestrationOptions` 快照，
  所以参数改动会在下一轮生效。
- 单轮失败会**记录日志但不终止宿主** —— 运行器继续等下一次触发（见 `ExecuteAsync` 的 try/catch）。

### 触发节奏 —— 什么可配、什么不可配

默认触发器注册为：

```csharp
services.TryAddSingleton(_ => new IntervalTrigger(StartMode.NextMinute, TimeSpan.Zero));
```

`IntervalTrigger`（`src/Quant.Infra.Net/Shared/Service/IntervalTrigger.cs`）支持这些 `StartMode` 取值
（`src/Quant.Infra.Net/Shared/Model/Enums.cs`）：

| `StartMode` | 节奏 |
|-------------|------|
| `NextSecond` | 每秒 |
| `NextMinute` | 每分钟（**默认**） |
| `NextHour` | 每小时 |
| `NextDay` | 每天 |
| `TodayBeforeUSMarketClose` | 一次，相对美东 16:00 收盘偏移（偏移量 = `DelayTimeSpan`，正=延后，负=提前） |

**要改节奏**，在调用 `AddQuantInfraNetOrchestration(...)` **之前**注册你自己的 `IntervalTrigger`
（它的 `TryAddSingleton` 会自动让位）：

```csharp
services.AddSingleton(new IntervalTrigger(StartMode.NextHour, TimeSpan.Zero)); // 每小时
// 然后：
services.AddQuantInfraNet(...); // 编排层 TryAdd 会沿用你的触发器
```

> **如实说明：** **没有 `appsettings.json` 配置键**能设触发模式 —— 它是构造期注册的，不从配置绑定。
> 需要不同节奏时，按上面这样注册你自己的 `IntervalTrigger`。别假设一个不存在的配置项。

### 部署形态

`PipelineRunner` 是 `BackgroundService`，所以自然的宿主是 .NET 通用宿主（例如
`Quant.Infra.Net.Runtime.Console` 应用或 minimal-API worker）：

```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(s => s.AddQuantInfraNet(
        rt => rt.RunMode = RunMode.Paper,   // 或 Testnet/Live
        o  => { o.Parameters["Strategy"] = "MaCross"; o.Parameters["Symbol"] = "QQQM"; }))
    .Build();
await host.RunAsync(); // 以宿主服务身份启动 PipelineRunner
```

因为运行器会跨越单轮失败继续运行，适合连续跑数天/数周。通过每轮的 `RunCompleted` 事件或
结构化 `PipelineEvent` 日志来观测。

## 4. 崩溃恢复 —— 哪些状态能在重启后存活

**默认的组合状态存储是纯内存的，重启不会保留。**

`IPortfolioStateStore` 由 `InMemoryPortfolioStateStore`
（`src/Quant.Infra.Net.Orchestration/State/InMemoryPortfolioStateStore.cs`）实现，就是一个锁后的
`PortfolioSnapshot?` 字段 —— 覆盖式语义，无持久化：

```csharp
public sealed class InMemoryPortfolioStateStore : IPortfolioStateStore
{
    private readonly object _gate = new();
    private PortfolioSnapshot? _latest;
    public Task SaveSnapshotAsync(PortfolioSnapshot snapshot, CancellationToken ct) { /* 存入 _latest */ }
    public Task<PortfolioSnapshot?> GetLatestAsync(CancellationToken ct) { /* 返回 _latest */ }
}
```

你必须为以下后果做规划：

1. **重启后存储为空**（`GetLatestAsync` 返回 `null`）。`RiskStage` 会代用一个零值 `PortfolioSnapshot`
   （权益 0、无持仓），所以*重启后的第一轮*看到的是空账本 —— 这是有意设计，不是 bug，但意味着
   kill-switch 与单标的规则在 `PortfolioStateStage` 重建快照前是基于空白状态运行的。
2. **券商才是持仓的事实源，存储不是。** `PortfolioStateStage` 每轮都从你的 `IExecutionBroker` 重读
   `GetPositionsAsync` / `GetAccountEquityUsdAsync`，所以实盘券商的真实持仓在重启后会从券商侧重新推导
   —— 内存快照只是*最后一次计算状态的缓存*，不是账户账本。
3. **如果你需要跨重启的持久状态**（例如续跑一次调仓、或保留多日风控台账），**自己实现
   `IPortfolioStateStore`**（例如用 SQLite/Redis 做底），并在 `AddQuantInfraNetOrchestration` **之前**
   注册（它的 `TryAddSingleton` 会自动让位）：

```csharp
services.AddSingleton<IPortfolioStateStore, MySqlitePortfolioStateStore>();
services.AddQuantInfraNet(...);
```

> **如实说明：** 本仓库**没有**内置持久化存储，也没有崩溃重放机制。Backtest 是确定性、可从数据
> 重新推导的；Paper/Live 恢复依赖券商自己的持仓状态 + 你添加的持久化存储。别假设自动恢复。

## 5. Paper → Live 晋级清单

- [ ] 你的策略的 Backtest/Paper 一致性测试通过（同一根 bar 槽位逐字段一致）。
- [ ] `MaxWeightPerSymbol`、`MaxGrossExposure`、`KillSwitchDrawdownRate` 已设为你真正想用于实盘的值。
- [ ] 如果你的券商不能做空，适配器的负权重策略（拒绝 vs 截断）是有意为之且已测试。
- [ ] Live 时：`RunMode.Live` + `BinanceApiKey`/`BinanceApiSecret`（或你的 `customBroker`）已配置 —— 容器若 Live 缺凭据会 **fail-fast**（`NotSupportedException`），这是有意设计。
- [ ] 你理解触发节奏，必要时已注册自己的 `IntervalTrigger`。
- [ ] 若需跨重启状态，已注册一个持久化 `IPortfolioStateStore`。

## 6. 下一步

- [custom-broker-ch.md](custom-broker-ch.md) —— 构建你在这里测试的适配器。
- [risk-management-ch.md](risk-management-ch.md) —— 你的测试所断言的规则。
- [faq-ch.md](faq-ch.md) —— 常见失败（Live 缺凭据、未知策略名、……）。
