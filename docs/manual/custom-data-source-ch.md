# 自定义数据源（中文）

> English: [custom-data-source-en.md](custom-data-source-en.md) · [索引](README-ch.md)

运行时的数据源面是**一个接口**——`ITraditionalFinanceSourceDataService`
（`src/Quant.Infra.Net/SourceData/Service/ITraditionalFinanceSourceDataService.cs`）——加上一个**种类枚举**
`DataSourceKind`（`src/Quant.Infra.Net.Runtime/Models/DataSourceKind.cs`）。把"种类 → 实现"映射起来的工厂
是 `src/Quant.Infra.Net.Runtime/DataSources/DataSourceFactory.cs`。

要接**你自己的数据**：实现一次接口，通过 `AddQuantInfraNet(...)` 的 `customDataSource` 参数把实例传给
运行时。不需要新增枚举值、不需要 fork、不需要在 `Quant.Infra.Net.Runtime` 里新建任何文件。

---

## 1. 你要实现的接口

```csharp
public interface ITraditionalFinanceSourceDataService
{
    // 管道实际会调的"给我 [start,end] 的 bar"两个方法：
    Task<Ohlcvs> DownloadOhlcvListAsync(
        string symbol, DateTime startDt, DateTime endDt,
        ResolutionLevel Period = ResolutionLevel.Daily,
        DataSource dataSource = DataSource.YahooFinance);

    Task<Ohlcvs> BeginSyncSourceDailyDataAsync(
        string symbol, DateTime startDt, DateTime endDt,
        string fullPathFileName, ResolutionLevel Period = ResolutionLevel.Daily);

    // 文件 / 列表辅助——不支持就 throw NotSupportedException：
    Task<List<Ohlcv>> GetOhlcvListAsync(string fullPathFilename);
    Task SaveOhlcvListAsync(IEnumerable<Ohlcv> ohlcvList, string fullPathFileName);
    Task<IEnumerable<string>> GetSp500SymbolsAsync(int number = 500);
}
```

管道实际走的装载路径是 `SignalDataLoader.LoadClosesAsync`（源码：
`Orchestration/Signals/SignalDataLoader.cs`）：
1. 查 `context.Get<Ohlcvs>()` 是否已有该标的的缓存切片（Backtest 每 bar 注入这个——这种情况**不会**调你的
   数据源）。
2. 查 `context.Get<HashSet<Ohlcv>>()`（DataIngestStage 的合并槽）。
3. 否则调用注入的 source 的 **`DownloadOhlcvListAsync`**。

所以**你真正必须实现的方法只有一个**：`DownloadOhlcvListAsync`。其余方法如果不支持，直接
`throw new NotSupportedException(...)`。

## 2. 一个可以直接复制运行的最小 fake

按 `ParityRegressionTests.FixedSeriesSource` 的模式写（源码：
`src/Quant.Infra.Net.Runtime.Tests/ParityRegressionTests.cs`）：

```csharp
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;

/// <summary>一个确定性的内存数据源，用于测试 / 演示。</summary>
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

## 3. 接线

已对照 `Runtime/DependencyInjection.cs`（`AddQuantInfraNet` 签名）和 `DataSources/DataSourceFactory.Create(...)`
核对：

```csharp
var mySource = new MyFakeSource(new[]
{
    new KeyValuePair<string, IReadOnlyList<Ohlcv>>("AAPL", MyBars),
});

services.AddQuantInfraNet(
    rt => {
        rt.RunMode = RunMode.Backtest;                 // 或 Paper / Testnet / Live
        rt.DataSource = DataSourceKind.Custom;          // ← 走自定义路径必须设
        // rt.BinanceApiKey = ...; rt.AlpacaApiKey = ...;   (Custom 下忽略)
    },
    o  => { o.Parameters["Strategy"] = "MaCross"; o.Parameters["Symbol"] = "AAPL"; },
    b  => { /* Backtest 成本 */ },
    customDataSource: mySource,                         // ← 你的实例（Kind=Custom 时必填）
    strategyAssemblies: typeof(Program).Assembly);
```

工厂对 `DataSourceKind.Custom` 的行为（源码：`DataSourceFactory.Create`）：
- `customDataSource == null` → **`ArgumentException`**（fail-fast，绝不静默回退）。
- `customDataSource != null` → **原样返回**。

其他种类下，`customDataSource` 被忽略（Demo / Yahoo / Csv / Binance / Stooq / Alpaca 各自从容器或凭据
构建自己的实例）。

## 4. 什么时候用 `Custom`、什么时候该新增 `DataSourceKind` 枚举值

| 场景 | 怎么做 |
|---|---|
| 一次性数据、单进程、测试、演示 | **`DataSourceKind.Custom`** + `customDataSource` 实例。`Quant.Infra.Net.Runtime` 零新代码。 |
| 一个可复用的、公开的数据源（例如某个新交易所的公开 K 线 API），很多用户都想要开箱即用 | **新增 `DataSourceKind` 枚举值** + 在 `src/Quant.Infra.Net.Runtime/DataSources/` 新增一个 `*TraditionalFinanceSourceDataService` + `DataSourceFactory.Create` 加一个 case。这是**框架级改动**，不是应用级改动——通过 NuGet 发布给所有下游。 |
| 需要私有 API key、内网数据库、非公开端点的数据源 | **`Custom`**。凭据本来就是你自己的事；新增枚举值只会泄漏一个只有一个用户能用得到的公开旋钮。 |

经验法则：**如果只有你一个人会用到这个数据源，就用 `Custom`。** 如果它是通用公开数据源、你希望它
在文档 + 工厂里成为一等公民，再提议新增枚举值。

## 5. 常见误区

- **忘了设 `DataSource = DataSourceKind.Custom`** — 你传的实例被忽略，用的是默认 `Demo` 源。（或者
  `DataSource` 留在 `Demo` 时，工厂返回 `Demo`，你的实例根本不会被查。）
- **静默返回空 `Ohlcvs`** — 当你的切片为空时，`SignalDataLoader.FetchAsync` 会记一条 `DataLoad` 事件
  （`no data available for '{symbol}' (source=...)`）并返回空收盘价序列，策略据此降级为无信号；抛异常时则记
  `data fetch failed for '{symbol}' (source=...)` 并同样降级（源码：`Orchestration/Signals/SignalDataLoader.cs`）。
  优先返回一个真实的（哪怕空）`Ohlcvs`，让下游能记录精确原因。
- **时区混用** — `Ohlcv.OpenDateTime` / `CloseDateTime` 在现有各源（Alpaca/Stooq/Demo 都用 UTC）里都按
  UTC 处理。
- **忘了 `HashSet<Ohlcv>` 去重** — `Ohlcvs.OhlcvSet` 是 `HashSet<Ohlcv>`；`Ohlcv` 重写了
  `Equals`/`GetHashCode`，重复 bar 会自动合并。你直接 `new HashSet<Ohlcv>(yourList)` 即可。

## 6. 下一步

- [configuration-reference-ch.md](configuration-reference-ch.md) §1.2 — `DataSourceKind` 表。
- [writing-a-strategy-ch.md](writing-a-strategy-ch.md) — 你的策略从你的数据源读数据。
- [testing-and-deployment-ch.md](testing-and-deployment-ch.md) — 在测试里注入 fake 源（`FixedSeriesSource`
  模式）。
