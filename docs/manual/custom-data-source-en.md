# Custom Data Source (EN)

> 中文: [custom-data-source-ch.md](custom-data-source-ch.md) · [Index](README-en.md)

The data-source surface of the runtime is **one interface** — `ITraditionalFinanceSourceDataService`
(`src/Quant.Infra.Net/SourceData/Service/ITraditionalFinanceSourceDataService.cs`) — and a
**kind enum** — `DataSourceKind` (`src/Quant.Infra.Net.Runtime/Models/DataSourceKind.cs`). The factory
that maps kind → implementation is `src/Quant.Infra.Net.Runtime/DataSources/DataSourceFactory.cs`.

To plug in **your own data**, you implement the interface once and hand the instance to the runtime via
the `customDataSource` parameter of `AddQuantInfraNet(...)`. No new enum value needed, no fork, no new
project file in `Quant.Infra.Net.Runtime`.

---

## 1. The interface you must implement

```csharp
public interface ITraditionalFinanceSourceDataService
{
    // The two methods the pipeline actually calls for "give me bars in [start,end]":
    Task<Ohlcvs> DownloadOhlcvListAsync(
        string symbol, DateTime startDt, DateTime endDt,
        ResolutionLevel Period = ResolutionLevel.Daily,
        DataSource dataSource = DataSource.YahooFinance);

    Task<Ohlcvs> BeginSyncSourceDailyDataAsync(
        string symbol, DateTime startDt, DateTime endDt,
        string fullPathFileName, ResolutionLevel Period = ResolutionLevel.Daily);

    // File-based / listing helpers — throw NotSupportedException if you don't support them:
    Task<List<Ohlcv>> GetOhlcvListAsync(string fullPathFilename);
    Task SaveOhlcvListAsync(IEnumerable<Ohlcv> ohlcvList, string fullPathFileName);
    Task<IEnumerable<string>> GetSp500SymbolsAsync(int number = 500);
}
```

The **load path** the pipeline uses is `SignalDataLoader.LoadClosesAsync` (source:
`Orchestration/Signals/SignalDataLoader.cs`):
1. Check `context.Get<Ohlcvs>()` for a cached slice for the symbol (Backtest injects this per bar —
   your source is **not** called in that case).
2. Check `context.Get<HashSet<Ohlcv>>()` (the DataIngestStage merged slot).
3. Otherwise call **`DownloadOhlcvListAsync`** on the injected source.

So **the only method you truly need** is `DownloadOhlcvListAsync`. The rest should be `throw new
NotSupportedException(...)` if unused.

## 2. A minimal fake you can copy-paste and run

Patterned on `ParityRegressionTests.FixedSeriesSource` (source:
`src/Quant.Infra.Net.Runtime.Tests/ParityRegressionTests.cs`):

```csharp
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

/// <summary>A deterministic in-memory data source for tests / demos.</summary>
public sealed class MyFakeSource : ITraditionalFinanceSourceDataService
{
    private readonly Dictionary<string, IReadOnlyList<Ohlcv>> _bars;
    public MyFakeSource(IEnumerable<KeyValuePair<string, IReadOnlyList<Ohlcv>>> series)
    {
        _bars = series.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
    }

    public Task<Ohlcvs> DownloadOhlcvListAsync(
        string symbol, DateTime startDt, DateTime endDt,
        ResolutionLevel Period = ResolutionLevel.Daily,
        DataSource dataSource = DataSource.YahooFinance)
    {
        if (!_bars.TryGetValue(symbol, out var all))
            return Task.FromResult(Empty(symbol, Period));

        var slice = all.Where(b => b.OpenDateTime >= startDt && b.OpenDateTime <= endDt).ToList();
        return Task.FromResult(new Ohlcvs
        {
            Symbol = symbol,
            ResolutionLevel = Period,
            StartDateTimeUtc = slice.Count > 0 ? slice[0].OpenDateTime : default,
            EndDateTimeUtc   = slice.Count > 0 ? slice[^1].OpenDateTime : default,
            OhlcvSet = new HashSet<Ohlcv>(slice),
        });
    }

    public Task<Ohlcvs> BeginSyncSourceDailyDataAsync(string s, DateTime a, DateTime b, string f,
        ResolutionLevel Period = ResolutionLevel.Daily)
        => throw new NotSupportedException(nameof(BeginSyncSourceDailyDataAsync));

    public Task<List<Ohlcv>> GetOhlcvListAsync(string fullPathFilename)
        => throw new NotSupportedException(nameof(GetOhlcvListAsync));

    public Task SaveOhlcvListAsync(IEnumerable<Ohlcv> ohlcvList, string fullPathFileName)
        => throw new NotSupportedException(nameof(SaveOhlcvListAsync));

    public Task<IEnumerable<string>> GetSp500SymbolsAsync(int number = 500)
        => throw new NotSupportedException(nameof(GetSp500SymbolsAsync));

    private static Ohlcvs Empty(string symbol, ResolutionLevel p) => new()
    {
        Symbol = symbol, ResolutionLevel = p,
        StartDateTimeUtc = default, EndDateTimeUtc = default,
        OhlcvSet = new HashSet<Ohlcv>(),
    };
}
```

## 3. Wiring it in

Verified against `Runtime/DependencyInjection.cs` (`AddQuantInfraNet` signature) and
`DataSources/DataSourceFactory.Create(...)`:

```csharp
var mySource = new MyFakeSource(new[]
{
    new KeyValuePair<string, IReadOnlyList<Ohlcv>>("AAPL", MyBars),
});

services.AddQuantInfraNet(
    rt => {
        rt.RunMode = RunMode.Backtest;                 // or Paper / Testnet / Live
        rt.DataSource = DataSourceKind.Custom;          // ← required for the custom path
        // rt.BinanceApiKey = ...; rt.AlpacaApiKey = ...;   (ignored under Custom)
    },
    o  => { o.Parameters["Strategy"] = "MaCross"; o.Parameters["Symbol"] = "AAPL"; },
    b  => { /* Backtest costs */ },
    customDataSource: mySource,                         // ← your instance (required when Kind=Custom)
    strategyAssemblies: typeof(Program).Assembly);
```

The factory behavior (source: `DataSourceFactory.Create`) for `DataSourceKind.Custom`:
- `customDataSource == null` → **`ArgumentException`** (fail-fast, never silent fallback).
- `customDataSource != null` → **returned as-is**.

For the other kinds, `customDataSource` is ignored (Demo / Yahoo / Csv / Binance / Stooq / Alpaca each
build their own instance from the container or from credentials).

## 4. When to use `Custom` vs. when to add a new `DataSourceKind` value

| Situation | Do this |
|---|---|
| One-off data, one process, tests, demo | **`DataSourceKind.Custom`** with a `customDataSource` instance. Zero new code in `Quant.Infra.Net.Runtime`. |
| A reusable, well-known public feed (e.g. a new exchange's public kline API) that many users would want out-of-the-box | **Add a new `DataSourceKind` value** + a new `*TraditionalFinanceSourceDataService` class in `src/Quant.Infra.Net.Runtime/DataSources/` + a new case in `DataSourceFactory.Create`. This is a **framework change**, not an app change — it ships to all downstream consumers via NuGet. |
| A feed that requires a private API key, an in-house database, or a non-public endpoint | **`Custom`**. The credential is already yours to manage; a new enum value would just leak a public knob that only one user can ever use. |

Rule of thumb: **if only you will ever need this source, use `Custom`.** If it's a general-purpose
public feed and you want it as a first-class citizen in the docs + factory, propose a new enum value.

## 5. Common mistakes

- **Forgetting to set `DataSource = DataSourceKind.Custom`** — the instance you passed is ignored and the
  default `Demo` source is used. (Or, if `DataSource` is left at `Demo`, the factory returns `Demo` and your
  instance is never consulted.)
- **Returning an empty `Ohlcvs` silently** — `SignalDataLoader.FetchAsync` records a `DataLoad` event
  (`no data available for '{symbol}' (source=...)`) and returns an empty close series when your slice is
  empty, so the strategy degrades to no signal; on an exception it records `data fetch failed for '{symbol}'
  (source=...)` and degrades the same way (source: `Orchestration/Signals/SignalDataLoader.cs`). Always
  prefer to return a real (possibly empty) `Ohlcvs` so downstream can log the exact reason.
- **Mixing time zones** — `Ohlcv.OpenDateTime` / `CloseDateTime` are expected to be UTC (`DateTimeKind.Utc`
  or `Unspecified` but representing UTC) in the existing sources (Alpaca/Stooq/Demo all use UTC).
- **Forgetting the `HashSet<Ohlcv>` dedup** — `Ohlcvs.OhlcvSet` is a `HashSet<Ohlcv>`; `Ohlcv` overrides
  `Equals`/`GetHashCode` so duplicate bars collapse. You can just `new HashSet<Ohlcv>(yourList)`.

## 6. Where to go next

- [configuration-reference-en.md](configuration-reference-en.md) §1.2 — `DataSourceKind` table.
- [writing-a-strategy-en.md](writing-a-strategy-en.md) — your strategy reading from your source.
- [testing-and-deployment-en.md](testing-and-deployment-en.md) — injecting a fake source in tests (the
  `FixedSeriesSource` pattern).
