# Risk Management (EN)

> 中文: [risk-management-ch.md](risk-management-ch.md) · [Index](README-en.md)

Risk management in this framework is a **single pluggable component** — `IRiskManager` — that the
**Risk stage** invokes *before* execution. Rejection **aborts the current run** (a `PipelineAbortException`
is thrown, a Warning notification is sent, and **no** execution happens this round).

Verified sources:
- Interface: `src/Quant.Infra.Net.Orchestration/Abstractions/IRiskManager.cs`
- Default impl: `src/Quant.Infra.Net.Orchestration/Risk/DefaultRiskManager.cs`
- Stage that consumes it: `src/Quant.Infra.Net.Orchestration/Stages/RiskStage.cs`
- Abort exception: `src/Quant.Infra.Net.Orchestration/Pipeline/PipelineAbortException.cs`
- Options that drive the default: `src/Quant.Infra.Net.Orchestration/Models/OrchestrationOptions.cs`
- DI registration: `src/Quant.Infra.Net.Orchestration/DependencyInjection.cs` (line 84: `TryAddSingleton<IRiskManager, DefaultRiskManager>()`)

---

## 1. The contract

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

- `targets` — the `IReadOnlyList<TargetPosition>` your strategy stage published (or the `TargetPositionStage`
  produced from your signals).
- `current` — the latest `PortfolioSnapshot` from `IPortfolioStateStore` (the default is in-memory; see
  [testing-and-deployment-en.md](testing-and-deployment-en.md) §4 on persistence).
- Return `Approved = true` (empty `Reasons`) to pass, or `Approved = false` with **every** triggered reason
  listed. The stage sends a Warning notification with the joined reasons and aborts the run.

## 2. The three default rules (`DefaultRiskManager`)

| # | Rule | Driven by | Trigger |
|---|------|-----------|---------|
| 1 | Per-symbol weight cap | `MaxWeightPerSymbol` (default `0.3`) | Any target with `|TargetWeight| > MaxWeightPerSymbol` |
| 2 | Gross exposure cap | `MaxGrossExposure` (default `1.0`) | `Σ|TargetWeight| > MaxGrossExposure` |
| 3 | Kill-switch drawdown | `KillSwitchDrawdownRate` (default `-0.15`) | `current.UnrealizedProfitRate <= KillSwitchDrawdownRate` (carries a "recommend full liquidation of all positions" reason) |

Order of evaluation: 1 → 2 → 3. All triggered reasons are collected (not just the first), then
`Approved = (reasons.Count == 0)`.

> The default is **stateless per call**: it only reads `OrchestrationOptions` and the two inputs. There is no
> internal memory. That's deliberate — the only "memory" the pipeline carries between rounds is the
> `PortfolioSnapshot` in `IPortfolioStateStore`.

## 3. What happens on rejection

From `RiskStage.ExecuteAsync`:

1. `context.Set<RiskAssessment>(assessment)` — so later stages (and tests) can read the verdict.
2. A `PipelineEvent` with `severity: Warning` is appended: `"risk check REJECTED: <reasons>"`.
3. `INotificationHub.PublishAsync(NotificationSeverity.Warning, "Risk check rejected", <reasons>, ct)`.
4. `throw new PipelineAbortException("risk check rejected: <reasons>")`.

Because the exception is a `PipelineAbortException`, the runner treats it as a **business-normal termination**
(not a system fault) and does not treat it as fatal — the next scheduled run proceeds.

## 4. How to plug in your own `IRiskManager`

`AddQuantInfraNetOrchestration` registers `IRiskManager` with `TryAdd` semantics, so **pre-registering your own
implementation makes the default step aside** — no other wiring needed.

```csharp
public sealed class MyRiskManager : IRiskManager
{
    private readonly OrchestrationOptions _o;
    public MyRiskManager(OrchestrationOptions o) => _o = o;

    public Task<RiskAssessment> AssessAsync(
        IReadOnlyList<TargetPosition> targets, PortfolioSnapshot current, CancellationToken ct)
    {
        var reasons = new List<string>();
        // e.g. a per-day loss cap, a market-hour gate, a correlation check, etc.
        if (current.UnrealizedProfitRate < -0.05)
            reasons.Add("day drawdown beyond -5%");

        return Task.FromResult(new RiskAssessment
        {
            Approved = reasons.Count == 0,
            // Reasons is set by the constructor; append:
        }) with { /* see below */ };
    }
}
```

> **C# note:** `RiskAssessment.Reasons` is a **get-only `List<string>`** with an initializer of 8, not an
> `init`-only settable. So in a real implementation you do:
>
> ```csharp
> var assessment = new RiskAssessment { Approved = reasons.Count == 0 };
> assessment.Reasons.AddRange(reasons);
> return Task.FromResult(assessment);
> ```
>
> (Same pattern `DefaultRiskManager` uses.)

### 4.1 Registration (two supported entry points)

**Via `AddQuantInfraNet` (the unified runtime):** the `AddQuantInfraNet` API does **not** expose a
`customRiskManager` parameter. The supported way is to pre-register `IRiskManager` on the same
`ServiceCollection` **before** calling `AddQuantInfraNet(...)`:

```csharp
var services = new ServiceCollection();
services.AddSingleton<IRiskManager, MyRiskManager>();   // ← your impl wins (TryAdd steps aside)
services.AddQuantInfraNet(
    rt => config.GetSection("Runtime").Bind(rt),
    o  => config.GetSection("Orchestration").Bind(o),
    b  => config.GetSection("Backtest").Bind(b),
    strategyAssemblies: typeof(Program).Assembly);
```

**Via `AddQuantInfraNetOrchestration` (directly, if you don't use the unified runtime):** the same rule —
register `IRiskManager` before the call, or use the `customStages` parameter to pass a `RiskStage` that was
constructed with your own `IRiskManager`.

## 5. Interaction with `TargetPositionStage` and `ExecutionStage`

- `TargetPositionStage` (source: `Stages/TargetPositionStage.cs`) caps `|TargetWeight|` at
  `MaxWeightPerSymbol` **before** your risk manager even sees it. So Rule 1 in `DefaultRiskManager` is a
  backstop, not the only line of defense.
- `ExecutionStage` consumes `RebalanceExecutionModel.RebalanceAsync(targets, ct)`, which calls
  `IExecutionBroker.SetTargetWeightAsync` / `LiquidateAsync` per symbol. It does **not** re-check risk; it
  trusts the Risk stage's verdict.

## 6. Common mistakes

- **Relying on `MinRebalanceDelta` for risk control** — it is an execution dead-zone (skip tiny adjustments),
  not a risk rule.
- **Forgetting that `PortfolioSnapshot` is the only "state" the default risk manager sees.** If you need
  day-level state (realized PnL today, position age, etc.), build it into your own `IRiskManager` and persist
  it yourself (the default `IPortfolioStateStore` is in-memory and **lost on restart** — see
  [testing-and-deployment-en.md](testing-and-deployment-en.md)).
- **Throwing from `AssessAsync`** — the contract is to return `Approved=false` with reasons. A thrown
  exception is treated as a system fault and the run ends abnormally.

## 7. Where to go next

- [testing-and-deployment-en.md](testing-and-deployment-en.md) — unit-testing a custom `IRiskManager`.
- [custom-broker-en.md](custom-broker-en.md) — the execution side of the pipeline.
- [configuration-reference-en.md](configuration-reference-en.md) §2 — the three risk knobs in detail.
