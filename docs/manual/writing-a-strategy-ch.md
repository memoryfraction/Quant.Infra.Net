# 从零写一个策略（中文）

> English: [writing-a-strategy-en.md](writing-a-strategy-en.md) · [索引](README-ch.md)

本框架里的"策略"指的是：**读行情 → 产出一条或多条 `Signal`（带 `TargetPosition`）**。其余一切——数据
装载、风控、执行、状态、通知——都是管道的事，你**不重新实现**。

仓库里支持两种形状（都有真实参考代码）：

1. **`ISignalGenerator` + `IStrategyDescriptor`**（"描述符"策略）——一个文件，不接管道。
   参考：`src/Quant.Infra.Net.Runtime.Console/Strategies/ExampleCustomStrategy.cs`。
2. **`Strategy` 子类阶段**（"阶段"策略）——继承 `LoadClosesAsync` / `Publish` / `GetInt` / `GetDouble` / `Log`
   这些帮助方法。参考：`src/Quant.Infra.Net.Runtime.Console/Strategies/QqqmReverseDcaStrategy.cs`。

两种形状在**全部四种 RunMode**（Backtest / Paper / Testnet / Live）下都能跑，不用改你的策略代码。

---

## 0. 你必须遵守的契约

源码：`Orchestration/Abstractions/ISignalGenerator.cs`、`Orchestration/Models/Signal.cs`、
`Orchestration/Models/TargetPosition.cs`。

- 输入**只**从 `IPipelineContext` 读（参数走 `context.GetParameter(...)`，数据走装载器帮助方法）。
- 输出**只**写两个约定槽位：
  - `context.Set<IReadOnlyList<Signal>>(…)` — 信号列表；
  - `context.Set<IReadOnlyList<TargetPosition>>(…)` — 目标仓位。
- `Signal` 字段：`Symbol`、`GeneratedUtc`、`Direction`（`Long`/`Short`/`Flat`）、`Strength`（你自己定义的
  `double`）、`Reason`（人类可读，**用英文**，避免控制台乱码）。
- `TargetPosition` 字段：`Symbol`、`TargetWeight`（带符号：+ 多，− 空，0 平）、`OriginSignal`
  （填上它，才有 Signal → TargetPosition → ExecutionReport 全链路审计）。
- **"无数据 / bar 不够"绝不能抛业务异常**——记事件 + 返回空集（`ISignalGenerator` 契约明文规定）。
- **跨路径保持确定性**：Backtest 每 bar 调一次；Paper 每个墙钟周期调一次。任何隐藏可变状态都会让两边
  漂移（见 [testing-and-deployment-ch.md](testing-and-deployment-ch.md) 里能抓住这种漂移的 parity 测试）。

## 1. 形状 A — `ISignalGenerator` + `IStrategyDescriptor`（简单策略推荐）

已对照 `ExampleCustomStrategy.cs` 逐字核对：

```csharp
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Runtime.Strategies;

namespace MyPortfolio.Strategies;

public sealed class ExampleCustomDescriptor : IStrategyDescriptor
{
    public const string StrategyName = "ExampleCustom";          // 在所有被扫描程序集中必须唯一
    public string Name => StrategyName;

    public ISignalGenerator Create(IServiceProvider serviceProvider)
        => new ExampleCustomSignalGenerator();                   // 需要依赖时在这里解析

    private sealed class ExampleCustomSignalGenerator : ISignalGenerator
    {
        public string Id => StrategyName;

        public Task<IReadOnlyList<Signal>> GenerateSignalsAsync(IPipelineContext context, CancellationToken ct)
        {
            var symbol = context.GetParameter("Symbol") ?? "UNKNOWN";
            return Task.FromResult<IReadOnlyList<Signal>>(new[]
            {
                new Signal
                {
                    Symbol = symbol,
                    GeneratedUtc = DateTime.UtcNow,
                    Direction = SignalDirection.Long,
                    Strength = 1.0,
                    Reason = "example custom strategy (single-file demo)"
                }
            });
        }
    }
}
```

接线（已对照 `Program.cs` 与 `Runtime/DependencyInjection.cs` 核对）：

```csharp
services.AddQuantInfraNet(
    rt => config.GetSection("Runtime").Bind(rt),
    o  => config.GetSection("Orchestration").Bind(o),   // 设 Parameters:Strategy = "ExampleCustom"
    b  => config.GetSection("Backtest").Bind(b),
    strategyAssemblies: typeof(Program).Assembly);      // ← 传"包含你自己描述符"的程序集
```

它是怎么被发现的（已对照 `Runtime/Strategies/StrategyCatalog.cs` 核对）：
catalog 会反射扫描 `typeof(StrategyCatalog).Assembly` **加上**你通过 `strategyAssemblies` 传入的每个程序集，
实例化每个导出的非抽象 `IStrategyDescriptor`（必须有公共无参构造），按 `Name` 索引（不区分大小写）。
**重名在构造时抛 `InvalidOperationException`（fail-fast）。** 未知的 `Parameters["Strategy"]` 在首次解析时
抛 `ArgumentException`，异常消息里会列出全部可用策略名。

> 内置三个策略（`MaCross`、`MeanReversion`、`PairTradingZScore`）统一登记在
> `Runtime/Strategies/BuiltInStrategyDescriptors.cs`（U4 约定）。你**不需要**自己注册它们。

## 2. 形状 B — `Strategy` 子类阶段（需要基类帮助方法时）

`Strategy` 基类（源码：`Runtime/Strategies/Strategy.cs`）免费给你：

| 成员 | 作用 |
|---|---|
| `protected abstract string StrategyName { get; }` | 你的阶段名（在管道内必须唯一）。 |
| `Task ExecuteAsync(IPipelineContext, CancellationToken)` | 模板方法——你不要重写它。 |
| `protected abstract Task ExecuteCoreAsync(IPipelineContext, CancellationToken)` | **你唯一要实现的成员。** |
| `Task<IReadOnlyList<double>> LoadClosesAsync(context, symbol, ct)` | 缓存优先的收盘价序列（复用 `SignalDataLoader`：context 缓存 → 数据源回退 → 空序列）。 |
| `void Publish(context, Signal, TargetPosition)` | 写两个约定槽位 + 一条事件。**用它（或自己 `context.Set<…>`）来产出。** |
| `int GetInt(context, key, default)` / `double GetDouble(context, key, default)` | 带默认值解析 `Orchestration.Parameters`。 |
| `void Log(context, message)` | 追加一条普通事件。 |

一个最小阶段（按 `QqqmReverseDcaStrategy.QqqmReverseDcaStage` 的模式写）：

```csharp
public sealed class MyStage : Strategy
{
    public MyStage(ITraditionalFinanceSourceDataService? yahooData,
                   IBinanceUsdFutureService? binanceService)
        : base(yahooData, binanceService) { }

    public override string StrategyName => "MyStage";

    protected override async Task ExecuteCoreAsync(IPipelineContext context, CancellationToken ct)
    {
        var symbol   = context.GetParameter("Symbol") ?? "AAPL";
        var maPeriod = Math.Max(2, GetInt(context, "MaPeriod", 200));

        var closes = await LoadClosesAsync(context, symbol, ct);
        if (closes.Count < maPeriod) { Log(context, $"insufficient data: {closes.Count} < {maPeriod}"); return; }

        var close = closes[^1];
        var sma   = closes.TakeLast(maPeriod).Average();
        var targetWeight = close < sma ? 0.5 : 0.0;          // ← 你自己的规则

        var signal = new Signal {
            Symbol = symbol, GeneratedUtc = DateTime.UtcNow,
            Direction = targetWeight > 0 ? SignalDirection.Long : SignalDirection.Flat,
            Strength = targetWeight,
            Reason = string.Format(CultureInfo.InvariantCulture, "close={0:0.2} sma={1:0.2}", close, sma)
        };
        Publish(context, signal,
                new TargetPosition { Symbol = symbol, TargetWeight = targetWeight, OriginSignal = signal });
    }
}
```

然后把它接成**自定义阶段序列**（就是 `AddQuantInfraNet` 的 `customStages` 参数，已对照
`QqqmReverseDcaStrategy.BuildStages` 核对）：

```csharp
var myStage = new MyStage(
    sp.GetService<ITraditionalFinanceSourceDataService>(),
    sp.GetService<IBinanceUsdFutureService>());

// 内置"尾部四阶段"（顺序固定，Notification 必须最后）——从容器解析：
var stages = new IPipelineStage[]
{
    myStage,
    sp.GetRequiredService<TargetPositionStage>(),  // 或者跳过：你已经产出了 TargetPosition
    sp.GetRequiredService<RiskStage>(),
    sp.GetRequiredService<ExecutionStage>(),
    sp.GetRequiredService<PortfolioStateStage>(),
    sp.GetRequiredService<NotificationStage>(),
};

services.AddQuantInfraNet(
    rt => config.GetSection("Runtime").Bind(rt),
    o  => config.GetSection("Orchestration").Bind(o),
    b  => config.GetSection("Backtest").Bind(b),
    customStages: stages,
    strategyAssemblies: typeof(Program).Assembly);
```

> **重要：** 传了 `customStages` 后，默认八阶段序列被**整体替换**。如果你的阶段已经同时产出了
> `IReadOnlyList<Signal>` 和 `IReadOnlyList<TargetPosition>`（通过 `Publish`），通常只需要在你的阶段后面
> 保留"尾部四阶段"（Risk → Execution → PortfolioState → Notification）。如果你只产出 `Signal`，就还要保留
> `TargetPositionStage`。

## 3. 我该选哪种形状？

| 场景 | 选 |
|---|---|
| 简单规则、不需要基类帮助方法、一个文件搞定 | **形状 A**（描述符） |
| 想要 `LoadClosesAsync` / `Publish` / `GetInt` / `Log` 白拿 | **形状 B**（阶段） |
| 需要在 Signal 与 Execution 之间插入自己的步骤（自己的目标仓位数学等） | **形状 B**（阶段）——只有它能进自定义阶段序列 |
| 一个程序集里多个独立策略 | 两种都行，只要 `Name` 不重 |

## 4. 发布前的清单

- [ ] `Name` / `StrategyName` 在所有被扫描程序集中唯一（不区分大小写）。
- [ ] 你产出了**两个槽位**，或者清楚知道缺的那个由哪个下游阶段补上。
- [ ] `Reason` 人类可读、用英文（本仓库控制台输出的约定）。
- [ ] "无数据 / bar 不够"不 `throw`——事件 + 空集。
- [ ] 确定性：没有隐藏的 static/实例状态让 Backtest（每 bar）与 Paper（每周期）漂移。
      参照 `src/Quant.Infra.Net.Runtime.Tests/ParityRegressionTests.cs` 写 parity 测试。
- [ ] appsettings 里设了 `Orchestration:Parameters:Strategy` 为你的名字。
- [ ] （仅形状 A）`strategyAssemblies:` 传了包含你描述符的程序集。

## 5. 下一步

- [risk-management-ch.md](risk-management-ch.md) — 风控门会对你的 `TargetPosition` 做什么。
- [testing-and-deployment-ch.md](testing-and-deployment-ch.md) — 给策略写单测 + parity 检查。
- [custom-data-source-ch.md](custom-data-source-ch.md) — 给你的策略喂你自己的数据。
