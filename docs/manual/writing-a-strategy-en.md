# Writing a Strategy From Scratch (EN)

> 中文: [writing-a-strategy-ch.md](writing-a-strategy-ch.md) · [Index](README-en.md)

A "strategy" in this framework means: **read market data → emit one or more `Signal`s (with a
`TargetPosition`)**. Everything around it — data loading, risk gate, execution, state, notifications —
is the pipeline's job, and you never re-implement it.

Two supported shapes, both in the repo:

1. **`ISignalGenerator` + `IStrategyDescriptor`** (a "descriptor" strategy) — one file, no stage wiring.
   Reference: `src/Quant.Infra.Net.Runtime.Console/Strategies/ExampleCustomStrategy.cs`.
2. **A `Strategy` subclass stage** (a "stage" strategy) — inherits `LoadClosesAsync` / `Publish` / `GetInt` /
   `GetDouble` / `Log` helpers. Reference: `src/Quant.Infra.Net.Runtime.Console/Strategies/QqqmReverseDcaStrategy.cs`.

Both run in **all four RunModes** (Backtest / Paper / Testnet / Live) without touching your code.

---

## 0. The contract you must honor

Source: `Orchestration/Abstractions/ISignalGenerator.cs`, `Orchestration/Models/Signal.cs`,
`Orchestration/Models/TargetPosition.cs`.

- Read inputs **only** from `IPipelineContext` (parameters via `context.GetParameter(...)`, data via the
  loader helpers).
- Emit **only** through the documented slots:
  - `context.Set<IReadOnlyList<Signal>>(…)` — the signal list;
  - `context.Set<IReadOnlyList<TargetPosition>>(…)` — the target positions.
- `Signal` fields: `Symbol`, `GeneratedUtc`, `Direction` (`Long`/`Short`/`Flat`), `Strength` (a `double`
  you define), `Reason` (human-readable, **English** to keep console output clean).
- `TargetPosition` fields: `Symbol`, `TargetWeight` (signed: + long, − short, 0 flat), `OriginSignal`
  (set it for the full audit trail Signal → TargetPosition → ExecutionReport).
- **Never throw business exceptions** for "no data / not enough bars" — record an event and return an
  empty list (the `ISignalGenerator` contract says so explicitly).
- **Be deterministic across paths**: Backtest calls your code once per bar; Paper calls it once per
  wall-clock cycle. Any hidden mutable state will drift between the two (see
  [testing-and-deployment-en.md](testing-and-deployment-en.md) for the parity test that catches this).

## 1. Shape A — `ISignalGenerator` + `IStrategyDescriptor` (recommended for simple strategies)

Verified against `ExampleCustomStrategy.cs`:

```csharp
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Runtime.Strategies;

namespace MyPortfolio.Strategies;

public sealed class ExampleCustomDescriptor : IStrategyDescriptor
{
    public const string StrategyName = "ExampleCustom";          // must be unique across all scanned assemblies
    public string Name => StrategyName;

    public ISignalGenerator Create(IServiceProvider serviceProvider)
        => new ExampleCustomSignalGenerator();                   // resolve deps here if you need them

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

Wiring (verified against `Program.cs` and `Runtime/DependencyInjection.cs`):

```csharp
services.AddQuantInfraNet(
    rt => config.GetSection("Runtime").Bind(rt),
    o  => config.GetSection("Orchestration").Bind(o),   // set Parameters:Strategy = "ExampleCustom"
    b  => config.GetSection("Backtest").Bind(b),
    strategyAssemblies: typeof(Program).Assembly);      // ← the assembly that contains YOUR descriptor
```

How it is found (verified against `Runtime/Strategies/StrategyCatalog.cs`):
the catalog reflection-scans `typeof(StrategyCatalog).Assembly` **plus** every assembly you pass in
`strategyAssemblies`, instantiates each exported non-abstract `IStrategyDescriptor` (must have a public
parameterless constructor), and indexes by `Name` (case-insensitive). **Duplicate names throw
`InvalidOperationException` at construction (fail-fast).** Unknown `Parameters["Strategy"]` throws
`ArgumentException` at first resolution with the full available list in the message.

> The built-in three (`MaCross`, `MeanReversion`, `PairTradingZScore`) are registered together in
> `Runtime/Strategies/BuiltInStrategyDescriptors.cs` (U4 convention). You don't need to register them.

## 2. Shape B — a `Strategy` subclass stage (when you need the base-class helpers)

The `Strategy` base (source: `Runtime/Strategies/Strategy.cs`) gives you, for free:

| Member | What it does |
|---|---|
| `protected abstract string StrategyName { get; }` | Your stage's name (must be unique in the pipeline). |
| `Task ExecuteAsync(IPipelineContext, CancellationToken)` | Template method — you never override it. |
| `protected abstract Task ExecuteCoreAsync(IPipelineContext, CancellationToken)` | **The only member you implement.** |
| `Task<IReadOnlyList<double>> LoadClosesAsync(context, symbol, ct)` | Cache-first close series (reuses `SignalDataLoader`: context cache → source fallback → empty). |
| `void Publish(context, Signal, TargetPosition)` | Writes the two documented slots + one event. **Call this (or `context.Set<…>` yourself) to emit.** |
| `int GetInt(context, key, default)` / `double GetDouble(context, key, default)` | Parse `Orchestration.Parameters` with defaults. |
| `void Log(context, message)` | Appends a normal event. |

A minimal stage (patterned on `QqqmReverseDcaStrategy.QqqmReverseDcaStage`):

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
        var targetWeight = close < sma ? 0.5 : 0.0;          // ← your own rule

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

Then wire it as a **custom stage sequence** (this is the `customStages` parameter of `AddQuantInfraNet`,
verified against `QqqmReverseDcaStrategy.BuildStages`):

```csharp
var myStage = new MyStage(
    sp.GetService<ITraditionalFinanceSourceDataService>(),
    sp.GetService<IBinanceUsdFutureService>());

// The four built-in tail stages (order fixed, Notification last) — resolve from the container:
var stages = new IPipelineStage[]
{
    myStage,
    sp.GetRequiredService<SignalStage>(),          // not needed here if you Publish directly
    sp.GetRequiredService<TargetPositionStage>(),  // or skip; you already emitted a TargetPosition
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

> **Important:** when you pass `customStages`, the default eight-stage sequence is **replaced entirely**.
> If your stage already emits both `IReadOnlyList<Signal>` and `IReadOnlyList<TargetPosition>` (via
> `Publish`), you typically keep only the four tail stages (Risk → Execution → PortfolioState →
> Notification) after your stage. If you only emit `Signal`s, keep the `TargetPositionStage` as well.

## 3. Which shape should I pick?

| Situation | Pick |
|---|---|
| Simple rule, no need for the base helpers, one file | **Shape A** (descriptor) |
| You want `LoadClosesAsync` / `Publish` / `GetInt` / `Log` for free | **Shape B** (stage) |
| You need to insert steps between Signal and Execution (your own target-position math, etc.) | **Shape B** (stage) — it's the only shape that slots into a custom stage sequence |
| Multiple independent strategies in one assembly | Both work; just keep `Name`s unique |

## 4. Checklist before you ship a strategy

- [ ] `Name` / `StrategyName` is unique (case-insensitive) across all scanned assemblies.
- [ ] You emit **both** slots, or you know which downstream stage fills the missing one.
- [ ] `Reason` is human-readable and in English (console-output convention in this repo).
- [ ] No `throw` for "no data" / "not enough bars" — event + empty list instead.
- [ ] Deterministic: no hidden static/instance state that would make Backtest (per-bar) diverge from
      Paper (per-cycle). Run the parity test in
      `src/Quant.Infra.Net.Runtime.Tests/ParityRegressionTests.cs` as a template.
- [ ] You set `Orchestration:Parameters:Strategy` in your appsettings to your name.
- [ ] You passed `strategyAssemblies:` containing your descriptor's assembly (Shape A only).

## 5. Where to go next

- [risk-management-en.md](risk-management-en.md) — what the risk gate does to your `TargetPosition`s.
- [testing-and-deployment-en.md](testing-and-deployment-en.md) — unit-test your strategy + the parity check.
- [custom-data-source-en.md](custom-data-source-en.md) — feed your strategy your own data.
