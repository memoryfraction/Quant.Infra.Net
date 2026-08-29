# Testing & Deployment (EN)

> 中文: [testing-and-deployment-ch.md](testing-and-deployment-ch.md) · [Index](README-en.md)

This guide covers the two questions that come up right before you deploy a strategy: **how do I prove my
strategy/broker/risk logic is correct**, and **what happens when I run it for real (Paper/Live) for a long time**.
Every pattern below is modeled on code that already lives in this repository's test suite, so the snippets are
copy-paste-ready.

---

## 1. Unit-testing your own strategy

The cheapest, most reliable test is to **run a single pipeline round** inside a `ServiceCollection`
container that you fully control, with a **deterministic in-memory data source**, and assert on the
`Signal` / `TargetPosition` / `RiskAssessment` slots the pipeline writes. This is exactly what
`src/Quant.Infra.Net.Runtime.Tests/ParityRegressionTests.cs` does (see `FixedSeriesSource` +
`RunOneBar`), and it needs no network.

### Minimal runnable skeleton (MSTest)

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
        // 1) Deterministic fixture: 250 daily bars, close = 100 + 0.5*i (strictly rising).
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
        var fixedSource = new FixedSeriesSource(series); // see §1.2

        // 2) A container with Paper mode + our fixed source + the strategy under test.
        var services = new ServiceCollection();
        services.AddQuantInfraNet(
            rt => { rt.RunMode = RunMode.Paper; rt.DataSource = DataSourceKind.Custom; },
            configureOrchestration: o =>
            {
                o.Parameters["Strategy"] = "MyStrategy"; // or a built-in like "MaCross"
                o.Parameters["Symbol"] = Symbol;
                o.Parameters["WeightPerSymbol"] = "0.3";
            },
            customDataSource: fixedSource);
        using var sp = services.BuildServiceProvider();

        // 3) Run exactly one pipeline round.
        var pipeline = sp.GetRequiredService<StrategyPipeline>();
        var options = sp.GetRequiredService<OrchestrationOptions>();
        var context = new PipelineContext(runId: 1, parameters: options.Parameters);

        // Pre-inject the full series as the "merged" slot (as BacktestRunner does per bar).
        var merged = new HashSet<Ohlcv>(bars);
        context.Set(merged);

        await pipeline.RunAsync(context, CancellationToken.None);

        // 4) Assert on the slots.
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

**Why this shape works:**
- `RunMode.Paper` + `DataSourceKind.Custom` + a `customDataSource` instance gives you a fully
  offline, deterministic container (no network, no real broker).
- Pre-injecting the `HashSet<Ohlcv>` merged slot means `SignalDataLoader.HasCachedSeries` returns
  `true` and **no source fallback call happens** — you control exactly what the strategy sees.
- Running one `pipeline.RunAsync` round executes the full default eight stages, so you are testing
  the real `TargetPositionStage`, `RiskStage`, `ExecutionStage`, and `PortfolioStateStage` logic, not
  a mock.

### 1.2 The `FixedSeriesSource` helper (copy from `ParityRegressionTests.cs`)

A minimal `ITraditionalFinanceSourceDataService` that returns one fixed slice and `NotSupportedException`
for everything else. The key insight (from the M4 guide): **only `DownloadOhlcvListAsync` matters** to the
pipeline; the rest can throw.

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

## 2. Testing a custom `IRiskManager` or `IExecutionBroker`

You don't need to stand up the full pipeline. Construct the unit under test directly with fakes and
assert on its output:

```csharp
// IRiskManager: feed it a PortfolioSnapshot + targets, assert Approved/Reasons.
var risk = new DefaultRiskManager(new OrchestrationOptions
{
    MaxWeightPerSymbol = 0.3,
    MaxGrossExposure = 1.0,
    KillSwitchDrawdownRate = -0.15,
});
var snap = new PortfolioSnapshot
{
    AccountEquityUsd = 100_000m,
    UnrealizedProfitRate = -0.20, // below kill-switch → should reject + recommend liquidation
    ActualWeights = new Dictionary<string, double>(),
    TargetWeights = new Dictionary<string, double>(),
};
var targets = new[] { new TargetPosition { Symbol = "AAA", TargetWeight = 0.4 } }; // > 0.3 cap
var assessment = await risk.AssessAsync(targets, snap, CancellationToken.None);
Assert.IsFalse(assessment.Approved);
Assert.IsTrue(assessment.Reasons.Any(r => r.Contains("MaxWeightPerSymbol")));
Assert.IsTrue(assessment.Reasons.Any(r => r.Contains("kill-switch")));
```

```csharp
// IExecutionBroker: drive it with a stub and assert the calls it would make.
var broker = new MyFakeBroker(); // your adapter (see custom-broker-*.md §4)
await broker.SetTargetWeightAsync("AAA", 0.5);
var pos = await broker.GetPositionsAsync();
Assert.AreEqual(1, pos.Count);
Assert.AreEqual("AAA", pos[0].Symbol);
await broker.LiquidateAsync("AAA");
Assert.AreEqual(0, (await broker.GetPositionsAsync()).Count);
```

**Backtest/Paper parity test** — the strongest regression is to run the *same bar* through both the
Backtest path and the Paper path and assert the `Signal`/`TargetPosition`/`RiskAssessment` slots are
field-identical. `ParityRegressionTests.Parity_SameBar_Backtest_And_Paper_Fields_Are_Identical` is the
reference (it also includes a "self-validation" case that injects a deliberately stateful-buggy strategy
to prove the comparator can actually detect drift). Use it as a template for your own strategy.

## 3. Running Paper/Live for a long time (`PipelineRunner` / `IntervalTrigger`)

The Paper/Live long-running driver is the **`PipelineRunner`** background service
(`src/Quant.Infra.Net.Orchestration/Pipeline/PipelineRunner.cs`), started by the generic host as an
`IHostedService` (registered in `Orchestration/DependencyInjection.cs`).

- It executes **one round at startup**, then on every **`IntervalTrigger`** firing.
- Each round uses a **fresh `PipelineContext`** (incrementing `runId`) and the **current**
  `OrchestrationOptions` snapshot, so parameter changes are picked up on the next round.
- A failed round is **logged and does not stop the host** — the runner keeps waiting for the next
  trigger (see the `ExecuteAsync` try/catch).

### Trigger cadence — what is and isn't configurable

The default trigger is registered as:

```csharp
services.TryAddSingleton(_ => new IntervalTrigger(StartMode.NextMinute, TimeSpan.Zero));
```

`IntervalTrigger` (`src/Quant.Infra.Net/Shared/Service/IntervalTrigger.cs`) supports these
`StartMode` values (`src/Quant.Infra.Net/Shared/Model/Enums.cs`):

| `StartMode` | Cadence |
|-------------|---------|
| `NextSecond` | every second |
| `NextMinute` | every minute (the **default**) |
| `NextHour` | every hour |
| `NextDay` | every day |
| `TodayBeforeUSMarketClose` | once, offset from US Eastern 16:00 close (offset = `DelayTimeSpan`, positive = later, negative = earlier) |

**To change the cadence**, register your own `IntervalTrigger` **before** calling
`AddQuantInfraNetOrchestration(...)` (its `TryAddSingleton` will step aside):

```csharp
services.AddSingleton(new IntervalTrigger(StartMode.NextHour, TimeSpan.Zero)); // hourly
// then:
services.AddQuantInfraNet(...); // orchestration TryAdd will honor your trigger
```

> **Honest limitation:** there is **no `appsettings.json` key** that sets the trigger mode — it is
> constructor-registered, not bound from configuration. If you need a different cadence, register your
> own `IntervalTrigger` as shown above. Do not assume a config knob that doesn't exist.

### Deployment shape

`PipelineRunner` is a `BackgroundService`, so the natural host is a .NET Generic Host (e.g. the
`Quant.Infra.Net.Runtime.Console` app or a minimal-API worker):

```csharp
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(s => s.AddQuantInfraNet(
        rt => rt.RunMode = RunMode.Paper,   // or Testnet/Live
        o  => { o.Parameters["Strategy"] = "MaCross"; o.Parameters["Symbol"] = "QQQM"; }))
    .Build();
await host.RunAsync(); // starts PipelineRunner as a hosted service
```

Because the runner keeps running across individual round failures, it is safe to leave running for
days/weeks. Monitor via the per-round `RunCompleted` event or the structured `PipelineEvent` log.

## 4. Crash recovery — what state survives a restart

**The default portfolio state store is purely in-memory and does NOT survive a restart.**

`IPortfolioStateStore` is implemented by `InMemoryPortfolioStateStore`
(`src/Quant.Infra.Net.Orchestration/State/InMemoryPortfolioStateStore.cs`), which is a single
`PortfolioSnapshot?` field behind a lock — overwrite semantics, no persistence:

```csharp
public sealed class InMemoryPortfolioStateStore : IPortfolioStateStore
{
    private readonly object _gate = new();
    private PortfolioSnapshot? _latest;
    public Task SaveSnapshotAsync(PortfolioSnapshot snapshot, CancellationToken ct) { /* store in _latest */ }
    public Task<PortfolioSnapshot?> GetLatestAsync(CancellationToken ct) { /* return _latest */ }
}
```

Consequences you must plan for:

1. **On restart, the store is empty** (`GetLatestAsync` returns `null`). `RiskStage` handles this by
   substituting a zeroed `PortfolioSnapshot` (equity 0, no positions), so the *first round after a
   restart* sees a flat book — this is intended, not a bug, but it means the kill-switch and
   per-symbol risk rules operate on a blank slate until `PortfolioStateStage` rebuilds the snapshot.
2. **The broker is the source of truth for positions, not the store.** `PortfolioStateStage` re-reads
   `GetPositionsAsync` / `GetAccountEquityUsdAsync` from your `IExecutionBroker` every round, so a
   live broker's actual positions are re-derived from the broker after a restart — the in-memory
   snapshot is a *cache of the last computed state*, not the account of record.
3. **If you need durable state across restarts** (e.g. to resume mid-rebalance or to keep a
   multi-day risk ledger), **implement `IPortfolioStateStore` yourself** (e.g. backed by SQLite/Redis)
   and register it *before* `AddQuantInfraNetOrchestration` (its `TryAddSingleton` will step aside):

```csharp
services.AddSingleton<IPortfolioStateStore, MySqlitePortfolioStateStore>();
services.AddQuantInfraNet(...);
```

> **Honest note:** the repo ships **no** built-in persistent store and no crash-replay mechanism.
> Backtest is deterministic and re-derivable from data; Paper/Live recovery depends on the broker's
> own position state plus whatever durable store you add. Do not assume automatic recovery.

## 5. Checklist before a Paper → Live promotion

- [ ] Backtest/Paper parity test passes for your strategy (field-identical slots on the same bar).
- [ ] `MaxWeightPerSymbol`, `MaxGrossExposure`, `KillSwitchDrawdownRate` are set to values you actually want live.
- [ ] If your broker can't short, your adapter's negative-weight policy (reject vs clamp) is deliberate and tested.
- [ ] For Live: `RunMode.Live` + `BinanceApiKey`/`BinanceApiSecret` (or your `customBroker`) are configured — the container **fails fast** (`NotSupportedException`) if Live lacks credentials, by design.
- [ ] You understand the trigger cadence and, if needed, have registered your own `IntervalTrigger`.
- [ ] If you need state across restarts, you have registered a durable `IPortfolioStateStore`.

## 6. Where to go next

- [custom-broker-en.md](custom-broker-en.md) — building the adapter you're testing here.
- [risk-management-en.md](risk-management-en.md) — the rules your tests assert against.
- [faq-en.md](faq-en.md) — common failures (Live missing credentials, unknown strategy name, ...).
