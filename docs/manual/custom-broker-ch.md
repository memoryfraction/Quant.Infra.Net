# 自定义券商执行（中文）

> English: [custom-broker-en.md](custom-broker-en.md) · [索引](README-ch.md)

管道的执行面是**一个接口**——`IExecutionBroker`
（`src/Quant.Infra.Net.Orchestration/Abstractions/IExecutionBroker.cs`）。管道
（`RebalanceExecutionModel`、`ExecutionStage`、`PortfolioStateStage`）只看到这个接口，从不直接
接触任何券商 SDK 类型。要接**你自己的券商**：写一个实现该接口的薄适配器，把实例传给
`AddQuantInfraNet(...)` 的 `customBroker` 参数。不需要 fork，不需要改管道。

---

## 1. 为什么要有这层抽象

在 `IExecutionBroker` 拆分之前（提交
`runtime(broker): decouple Orchestration pipeline from Binance via IExecutionBroker`），
`Quant.Infra.Net.Orchestration` 里所有与执行相关的阶段都直接依赖币安期货服务类型
（`IBinanceUsdFutureService`）。实际后果：统一的 `RunMode` 开关（Backtest / Paper / Testnet /
Live）只对币安 USD-M 期货能跑通——因为它是唯一有已注册实现的券商。

这个接口是唯一的接缝，让其他券商（Interactive Brokers、Charles Schwab、任何自研网关）都能挂在
同一个 `RunMode` 开关下：**实现 `IExecutionBroker`，传进去，其余零改动。**

## 2. 接口逐方法讲解

```csharp
public interface IExecutionBroker
{
    Task<decimal> GetAccountEquityUsdAsync();
    Task<double> GetUnrealizedProfitRateAsync();
    Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync();
    Task SetTargetWeightAsync(string symbol, double signedWeight);
    Task LiquidateAsync(string symbol);
}

public sealed class BrokerPosition
{
    public string Symbol { get; init; }
    public decimal Quantity { get; init; }   // 带符号：正=多头，负=空头
    public decimal MarkPrice { get; init; }
}
```

| 成员 | 被谁调用 | 说明 |
|------|---------|------|
| `GetAccountEquityUsdAsync` | `RebalanceExecutionModel`（权重=名义/权益）、`PortfolioStateStage`（快照）、`RiskStage`（经快照） | 返回当前账户权益（**USD**）。 |
| `GetUnrealizedProfitRateAsync` | `PortfolioStateStage`（快照 → kill-switch 输入） | 未实现盈亏率 = 未实现盈亏/权益（如 `-0.02` = −2%）。 |
| `GetPositionsAsync` | `RebalanceExecutionModel`（实际权重）、`PortfolioStateStage`（实际权重表） | `Quantity` **带符号**（正多头/负空头）；`MarkPrice` 是当前标记价。 |
| `SetTargetWeightAsync(symbol, signedWeight)` | `RebalanceExecutionModel` | **带符号**目标权重：正=多头，负=空头。权重相对 `GetAccountEquityUsdAsync`（模型按 `Quantity * MarkPrice / equity` 计算实际权重）。 |
| `LiquidateAsync(symbol)` | `RebalanceExecutionModel`（当 `|TargetWeight|` 低于 `1e-9` 时） | 完全平掉该标的的持仓。 |

### 没有做空概念的券商

大多数现金股票账户不能做空。接口契约（见 `IExecutionBroker` 的 XML 注释）明确写着：*没有做空
概念的券商（大多数现金股票账户）可以在自己的适配器里直接拒绝或把负权重截断为 0*。二选一：

- **拒绝**：`throw new NotSupportedException($"shorting {symbol} is not supported")` —— 管道会把
  该标的记为一次失败执行报告并继续（见 `RebalanceExecutionModel` 的逐 symbol try/catch）。
- **截断**：把负权重当 `0` 处理（只做多调仓到平/多）。

### 可选能力：`IPaperMarkable`

```csharp
public interface IPaperMarkable
{
    void SetMarkPrices(IReadOnlyDictionary<string, double> latestPrices);
}
```

这是**可选**的。`ExecutionStage` 和 `PortfolioStateStage` 在调仓/估值前都会调用
`StageMarketData.ApplyPaperMarks(context, broker)`，其内部是
`if (broker is not IPaperMarkable) return;` —— 实盘券商不实现它，调用直接空转。它的用途：让
*模拟*券商（Paper 模拟器）收到管道的最新收盘价，使其估值和盈亏按当前价格标记，而不是用陈旧价格。

## 3. 参考实现走读

`BinanceUsdFutureExecutionBrokerAdapter`
（`src/Quant.Infra.Net.Orchestration/Execution/BinanceUsdFutureExecutionBrokerAdapter.cs`）
包装任意已有的 `IBinanceUsdFutureService` —— 真实币安、`PaperBinanceUsdFutureService`、或回测
引擎的 `BacktestBrokerService`（三者都已实现它）：

```csharp
public sealed class BinanceUsdFutureExecutionBrokerAdapter : IExecutionBroker, IPaperMarkable
{
    private readonly IBinanceUsdFutureService _inner;

    public BinanceUsdFutureExecutionBrokerAdapter(IBinanceUsdFutureService inner) { ... }

    public Task<decimal> GetAccountEquityUsdAsync() => _inner.GetusdFutureAccountBalanceAsync();

    public Task<double> GetUnrealizedProfitRateAsync() => _inner.GetusdFutureUnrealizedProfitRateAsync();

    public async Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync()
    {
        var positions = await _inner.GetHoldingPositionAsync().ConfigureAwait(false);
        return positions
            .Select(p => new BrokerPosition { Symbol = p.Symbol, Quantity = p.Quantity, MarkPrice = p.MarkPrice })
            .ToList();
    }

    public Task SetTargetWeightAsync(string symbol, double signedWeight)
        => _inner.SetUsdFutureHoldingsAsync(
            symbol,
            Math.Abs(signedWeight),
            signedWeight >= 0d ? PositionSide.Long : PositionSide.Short);

    public Task LiquidateAsync(string symbol) => _inner.LiquidateUsdFutureAsync(symbol);

    public void SetMarkPrices(IReadOnlyDictionary<string, double> latestPrices)
    {
        if (_inner is PaperBinanceUsdFutureService paper)
        {
            paper.SetMarkPrices(latestPrices);
        }
    }
}
```

两处值得注意：

1. **负权重处理**委派给了被包装的服务：适配器传 `Math.Abs(weight)` 加 `PositionSide.Long` /
   `PositionSide.Short` 标志。不能做空的现金股票适配器则应自己在 `SetTargetWeightAsync` 里抛/截断。
2. **`IPaperMarkable` 是条件转发** —— 仅当内部服务是 Paper 模拟器时才生效；对真实币安或回测
   模拟器该方法静默空转，正是上面"实盘券商不实现它"的行为。

## 4. 一个可复制运行的最小适配器（内存 fake 券商）

你**不需要**真实券商 API 就能验证——一个内存券商足以证明管道能对你的适配器跑起来：

```csharp
using Quant.Infra.Net.Orchestration.Abstractions;

public sealed class FakeCashEquityBroker : IExecutionBroker
{
    private readonly object _gate = new();
    private decimal _equityUsd = 100_000m;
    private readonly Dictionary<string, decimal> _notionalUsd = new(StringComparer.OrdinalIgnoreCase); // 带符号
    private readonly Dictionary<string, decimal> _mark = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Symbol, double Weight, DateTime At)> _calls = new();

    public Task<decimal> GetAccountEquityUsdAsync()
        => Task.FromResult(_equityUsd);

    public Task<double> GetUnrealizedProfitRateAsync()
        => Task.FromResult(0d); // 真实适配器返回 未实现盈亏/权益

    public Task<IReadOnlyList<BrokerPosition>> GetPositionsAsync()
    {
        var list = _notionalUsd
            .Where(kv => kv.Value != 0m)
            .Select(kv => new BrokerPosition
            {
                Symbol = kv.Key,
                Quantity = kv.Value,   // a real adapter returns shares; here we keep signed notional as a stand-in
                MarkPrice = _mark.TryGetValue(kv.Key, out var mp) ? mp : 1m,
            }).ToList();
        return Task.FromResult<IReadOnlyList<BrokerPosition>>(list);
    }

    public Task SetTargetWeightAsync(string symbol, double signedWeight)
    {
        if (signedWeight < 0d)
        {
            throw new NotSupportedException(
                $"{symbol}: shorting is not supported by this cash-equity adapter");
        }
        // weight -> shares when a mark is known; otherwise keep signed notional as a stand-in
        _notionalUsd[symbol] = (decimal)(signedWeight * (double)_equityUsd);
        _calls.Add((symbol, signedWeight, DateTime.UtcNow));
        return Task.CompletedTask;
    }

    public Task LiquidateAsync(string symbol)
    {
        _notionalUsd[symbol] = 0m;
        _calls.Add((symbol, 0d, DateTime.UtcNow));
        return Task.CompletedTask;
    }

    // 可选：采用管道的最新收盘价作为标记价（Paper 模拟器就是这么做的）。
    public sealed class WithMarks : FakeCashEquityBroker, IPaperMarkable
    {
        public void SetMarkPrices(IReadOnlyDictionary<string, double> latestPrices)
        {
            foreach (var kv in latestPrices) _mark[kv.Key] = (decimal)kv.Value;
        }
    }
}
```

> 注意：真实适配器会用券商自己的账户数据填 `GetUnrealizedProfitRateAsync`、用持仓行情流维护
> `MarkPrice`。上面的 fake 保持最简以便离线运行。

## 5. 接线

已对照 `src/Quant.Infra.Net.Runtime/DependencyInjection.cs`（`AddQuantInfraNet` 签名与
`customBroker` 注册块）核对：

```csharp
var broker = new FakeCashEquityBroker(); // 或你的真实适配器

services.AddQuantInfraNet(
    rt => rt.RunMode = RunMode.Paper,           // 或 Testnet / Live
    o  => { o.Parameters["Strategy"] = "MaCross"; o.Parameters["Symbol"] = "AAPL"; },
    b  => { /* 回测选项——Paper 下不读 */ },
    customDataSource: null,                     // （或你的数据源，见另一篇指南）
    customBroker: broker);                      // ← 你的券商
```

注册如何解析（源码：`Runtime/DependencyInjection.cs` + `Orchestration/DependencyInjection.cs`）：

- `RunMode != Backtest` **且** `customBroker != null` → `services.AddSingleton(customBroker)`
  （你的实例胜出）。
- 编排层随后 `services.TryAddSingleton<IExecutionBroker>(sp => new
  BinanceUsdFutureExecutionBrokerAdapter(sp.GetRequiredService<IBinanceUsdFutureService>()))` ——
  `TryAdd` 意味着因为你已注册了一个，它会**自动让位**。
- 管道各阶段从容器取 `IExecutionBroker`，因此它们看到的都是你的实例。

### 为什么 `customBroker` 在 Backtest 模式被忽略

这是**有意设计，不是 bug**（详见 [faq-ch.md](faq-ch.md) 的 FAQ 条目）。Backtest 路径（D1 机制，见
[编排层设计](../OrchestrationLayerDesign.md)）先把一个 `BacktestBrokerService` 实例注册为
`IBinanceUsdFutureService`，而 `AddQuantInfraNetBacktest` 是另一个方法，**根本不接收**
`customBroker` 参数。回测里的执行必须由模拟时钟/标记价驱动（`IBacktestBroker` 面：
`SetMarkPrices`、`SimulatedNowUtc`、`DeferFills`、`FlushPendingOrders`）——一个实盘风格的
`IExecutionBroker` 会绕过逐 bar 的成交语义与成本/滑点模型。`AddQuantInfraNet` 里的守卫
`if (customBroker != null && runtimeOptions.RunMode != RunMode.Backtest)` 正是强制这一点：
**Backtest 始终用 `BacktestBrokerService`。**

## 6. 其他券商的现状（截至本提交）

| 券商 | 状态 |
|------|------|
| **币安 USD-M 期货** | ✅ 全链路打通：真实 API 服务（`BinanceUsdFutureService`）、Paper 模拟器（`PaperBinanceUsdFutureService`）、回测模拟器（`BacktestBrokerService`）都实现了 `IBinanceUsdFutureService`，因此都挂在同一个适配器下。 |
| **Interactive Brokers** | ⚠️ **尚未接通。** `InteractiveBrokersService`
  （`src/Quant.Infra.Net/Broker/Service/InteractiveBrokersService.cs`）是**空壳** ——
  每个公开方法当前都 `throw new NotImplementedException()`。完整的 InterReact IB TWS 协议客户端
  已内嵌在仓库 `src/Quant.Infra.Net/Broker/InterReact/`（真实可用代码），但**还没有任何东西把它
  接到服务上**。所以"IB"是*进行中*，不是*已支持*。一个能跑的 IB 适配器应在其之上实现
  `IExecutionBroker` 并作为 `customBroker` 传入。 |
| **Charles Schwab** | 🚫 **不在本仓库范围。** 核心库里的 `SchwabBrokerService`
  （`src/Quant.Infra.Net/Broker/Service/SchwabBrokerService.cs`）是独立的只读行情+下单面；面向
  *管道执行*的 Schwab 适配器有意留给 **Quant.Infra.Net.Pro** 仓库，不属于本开源面。 |

## 7. 下一步

- [writing-a-strategy-ch.md](writing-a-strategy-ch.md) —— 会驱动你这个券商的策略。
- [risk-management-ch.md](risk-management-ch.md) —— 你的券商会看到的前置风控检查。
- [testing-and-deployment-ch.md](testing-and-deployment-ch.md) —— 用 fake 管道为你的适配器写单元测试。
- [faq-ch.md](faq-ch.md) —— 为什么 `customBroker` 在 Backtest 被忽略，以及更多。
