# 风控（中文）

> English: [risk-management-en.md](risk-management-en.md) · [索引](README-ch.md)

本框架的风控是**一个可插拔的组件**——`IRiskManager`——由**Risk 阶段**在执行前调用。
拒绝会**终止当前轮**（抛 `PipelineAbortException`、发一条 Warning 通知、**本轮不执行**）。

已核对源码：
- 接口：`src/Quant.Infra.Net.Orchestration/Abstractions/IRiskManager.cs`
- 默认实现：`src/Quant.Infra.Net.Orchestration/Risk/DefaultRiskManager.cs`
- 消费它的阶段：`src/Quant.Infra.Net.Orchestration/Stages/RiskStage.cs`
- 终止异常：`src/Quant.Infra.Net.Orchestration/Pipeline/PipelineAbortException.cs`
- 驱动默认的选项：`src/Quant.Infra.Net.Orchestration/Models/OrchestrationOptions.cs`
- DI 注册：`src/Quant.Infra.Net.Orchestration/DependencyInjection.cs`（第 84 行：`TryAddSingleton<IRiskManager, DefaultRiskManager>()`）

---

## 1. 契约

```csharp
public interface IRiskManager
{
    Task<RiskAssessment> AssessAsync(
        IReadOnlyList<TargetPosition> targets,
        PortfolioSnapshot current,
        CancellationToken ct);
}

public class RiskAssessment
{
    public bool Approved { get; init; }
    public List<string> Reasons { get; } = new(8);
}
```

- `targets` — 你的策略阶段发布的 `IReadOnlyList<TargetPosition>`（或 `TargetPositionStage` 从你的信号产出的）。
- `current` — `IPortfolioStateStore` 里的最新 `PortfolioSnapshot`（默认是内存存储；持久化问题见
  [testing-and-deployment-ch.md](testing-and-deployment-ch.md) §4）。
- 返回 `Approved = true`（`Reasons` 为空）放行；`Approved = false` 时**列出全部**触发理由。
  阶段会把理由拼成一条 Warning 通知发出，并终止本轮。

## 2. 三条默认规则（`DefaultRiskManager`）

| # | 规则 | 由什么驱动 | 触发条件 |
|---|------|-----------|---------|
| 1 | 单标的权重上限 | `MaxWeightPerSymbol`（默认 `0.3`） | 任一 target 的 `|TargetWeight| > MaxWeightPerSymbol` |
| 2 | 总敞口上限 | `MaxGrossExposure`（默认 `1.0`） | `Σ|TargetWeight| > MaxGrossExposure` |
| 3 | kill-switch 回撤 | `KillSwitchDrawdownRate`（默认 `-0.15`） | `current.UnrealizedProfitRate <= KillSwitchDrawdownRate`（附"建议全部平仓"理由） |

求值顺序：1 → 2 → 3。**所有**触发的理由都被收集（不只第一条），然后 `Approved = (reasons.Count == 0)`。

> 默认实现**每次调用无内部状态**：只读 `OrchestrationOptions` 与两个入参，没有任何记忆。这是刻意的——
> 管道跨轮携带的"记忆"只有 `IPortfolioStateStore` 里的 `PortfolioSnapshot`。

## 3. 拒绝时发生什么

来自 `RiskStage.ExecuteAsync`：

1. `context.Set<RiskAssessment>(assessment)` — 让后续阶段（和测试）能读到裁决。
2. 追加一条 `severity: Warning` 的 `PipelineEvent`：`"risk check REJECTED: <reasons>"`。
3. `INotificationHub.PublishAsync(NotificationSeverity.Warning, "Risk check rejected", <reasons>, ct)`。
4. `throw new PipelineAbortException("risk check rejected: <reasons>")`。

因为异常是 `PipelineAbortException`，runner 把它当作**业务正常终止**（不是系统故障），不视为致命——下一次
定时运行照常进行。

## 4. 怎么接自己的 `IRiskManager`

`AddQuantInfraNetOrchestration` 用 `TryAdd` 语义注册 `IRiskManager`，所以**你先注册自己的实现，默认就会自动
让位**——不需要任何额外接线。

```csharp
public sealed class MyRiskManager : IRiskManager
{
    private readonly OrchestrationOptions _o;
    public MyRiskManager(OrchestrationOptions o) => _o = o;

    public Task<RiskAssessment> AssessAsync(
        IReadOnlyList<TargetPosition> targets, PortfolioSnapshot current, CancellationToken ct)
    {
        var reasons = new List<string>();
        // 例如：当日亏损上限、交易时段门、相关性检查等
        if (current.UnrealizedProfitRate < -0.05)
            reasons.Add("day drawdown beyond -5%");

        var assessment = new RiskAssessment { Approved = reasons.Count == 0 };
        assessment.Reasons.AddRange(reasons);          // 见下方 C# 说明
        return Task.FromResult(assessment);
    }
}
```

> **C# 说明：** `RiskAssessment.Reasons` 是**只 get 的 `List<string>`**，初始容量 8，**没有 init/setter**。
> 所以真实实现里要这样写：
>
> ```csharp
> var assessment = new RiskAssessment { Approved = reasons.Count == 0 };
> assessment.Reasons.AddRange(reasons);
> return Task.FromResult(assessment);
> ```
>
> （`DefaultRiskManager` 也是这个写法。）

### 4.1 注册（两个受支持的入口）

**走 `AddQuantInfraNet`（统一运行时）：** `AddQuantInfraNet` **没有** `customRiskManager` 参数。
受支持的写法是在调用 `AddQuantInfraNet(...)` **之前**，在同一个 `ServiceCollection` 上先注册
`IRiskManager`：

```csharp
var services = new ServiceCollection();
services.AddSingleton<IRiskManager, MyRiskManager>();   // ← 你的实现胜出（TryAdd 自动让位）
services.AddQuantInfraNet(
    rt => config.GetSection("Runtime").Bind(rt),
    o  => config.GetSection("Orchestration").Bind(o),
    b  => config.GetSection("Backtest").Bind(b),
    strategyAssemblies: typeof(Program).Assembly);
```

**直接走 `AddQuantInfraNetOrchestration`（不用统一运行时）：** 规则相同——要么在调用前先注册
`IRiskManager`，要么用 `customStages` 参数传一个用你自己 `IRiskManager` 构造的 `RiskStage`。

## 5. 与 `TargetPositionStage` 和 `ExecutionStage` 的关系

- `TargetPositionStage`（源码：`Stages/TargetPositionStage.cs`）在风控看到之前就把 `|TargetWeight|`
  cap 到 `MaxWeightPerSymbol` 以内。所以 `DefaultRiskManager` 里的规则 1 是**兜底**，不是唯一防线。
- `ExecutionStage` 消费 `RebalanceExecutionModel.RebalanceAsync(targets, ct)`，按标的调
  `IExecutionBroker.SetTargetWeightAsync` / `LiquidateAsync`。它**不**重新做风控，信任 Risk 阶段的裁决。

## 6. 常见误区

- **靠 `MinRebalanceDelta` 做风控** — 它是执行死区（跳过微调），不是风控规则。
- **忘了 `PortfolioSnapshot` 是默认风控看到的唯一"状态"。** 若你需要日级状态（当日已实现盈亏、持仓
  时长等），要么自己写 `IRiskManager` 并自己持久化（默认 `IPortfolioStateStore` 是纯内存，**重启即丢**——见
  [testing-and-deployment-ch.md](testing-and-deployment-ch.md)）。
- **在 `AssessAsync` 里抛异常** — 契约是返回 `Approved=false` + 理由。抛出的异常会被当作系统故障，
  本轮非正常结束。

## 7. 下一步

- [testing-and-deployment-ch.md](testing-and-deployment-ch.md) — 给自定义 `IRiskManager` 写单测。
- [custom-broker-ch.md](custom-broker-ch.md) — 管道执行侧。
- [configuration-reference-ch.md](configuration-reference-ch.md) §2 — 三个风控旋钮详解。
