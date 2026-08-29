# 配置参考（中文）

> English: [configuration-reference-en.md](configuration-reference-en.md) · [索引](README-ch.md)

本页是驱动 Runtime / Orchestration / Backtest 栈的**三个配置对象的完整逐字段参考**。所有条目均已
对照源码核对：

- `src/Quant.Infra.Net.Runtime/Models/RuntimeOptions.cs`
- `src/Quant.Infra.Net.Orchestration/Models/OrchestrationOptions.cs`（含 `Models/NotificationOptions.cs`）
- `src/Quant.Infra.Net.Backtest/Models/BacktestOptions.cs`
- 枚举：`Models/RunMode.cs`、`Models/DataSourceKind.cs`（Runtime）、`Shared/Model/Enums.cs`（`ExchangeEnvironment`）
- 可运行样例：`src/Quant.Infra.Net.Runtime.Console/appsettings.json`

三个对象都由 `AddQuantInfraNet(...)`（`src/Quant.Infra.Net.Runtime/DependencyInjection.cs`）绑定配置节：

| appsettings.json 配置节 | 绑定到 |
|---|---|
| `Runtime` | `RuntimeOptions` |
| `Orchestration` | `OrchestrationOptions` |
| `Backtest` | `BacktestOptions` |

---

## 1. `RuntimeOptions` — 总开关

**配置节 `Runtime`** · 源码：`RuntimeOptions.cs`

| 字段 | 类型 | 默认值 | 生效条件 | 一句话说明 |
|---|---|---|---|---|
| `RunMode` | `RunMode` | `RunMode.Backtest` | 始终 | 总开关。决定驱动循环 + 券商（Backtest / Paper / Testnet / Live）。 |
| `DataSource` | `DataSourceKind` | `DataSourceKind.Demo` | 始终 | 接哪个历史数据源（Demo / Yahoo / Csv / Binance / Stooq / Alpaca / Custom）。 |
| `BinanceApiKey` | `string?` | `null` | `RunMode` = Testnet 或 Live | 币安 U 本位合约 API Key。Backtest/Paper 下忽略。 |
| `BinanceApiSecret` | `string?` | `null` | `RunMode` = Testnet 或 Live | 币安 API Secret。Backtest/Paper 下忽略。 |
| `AlpacaApiKey` | `string?` | `null` | `DataSource` = Alpaca | Alpaca API Key（免费 IEX 层，alpaca.markets）。其他数据源忽略。 |
| `AlpacaApiSecret` | `string?` | `null` | `DataSource` = Alpaca | Alpaca API Secret。其他数据源忽略。 |

### 1.1 `RunMode` 枚举（源码：`Runtime/Models/RunMode.cs`）

| 取值 | 含义 | 需要的额外配置 |
|---|---|---|
| `Backtest` (0) | 历史回放。`BacktestRunner` 驱动，`BacktestBrokerService` 记账。零网络。 | `Backtest` 节配成本/成交时机；一个有历史数据的数据源（首跑用 Demo 即可）。 |
| `Paper` (1) | 墙钟驱动（`PipelineRunner` + `IntervalTrigger`），`PaperBinanceUsdFutureService` 内存记账。零网络。 | 数据源选择之外无额外要求。 |
| `Testnet` (2) | 真实币安**测试网** API。 | `BinanceApiKey` / `BinanceApiSecret`（测试网 key）——缺 key 启动即抛 `NotSupportedException`（fail-fast）。 |
| `Live` (3) | 真实币安**实盘** API，真金白银。 | `BinanceApiKey` / `BinanceApiSecret`（实盘 key）——同样 fail-fast。 |

### 1.2 `DataSourceKind` 枚举（源码：`Runtime/Models/DataSourceKind.cs`）

| 取值 | 含义 | 需要的额外配置 |
|---|---|---|
| `Demo` (0) | 离线合成确定性 K 线（默认；零网络；Demo/CI 用）。 | 无。 |
| `Yahoo` (1) | Yahoo Finance（核心库 `TraditionalFinanceSourceDataService` + pythonnet/Yahoo Chart API）。 | 可用 Python + `yfinance`（或走 Chart API 路径）。 |
| `Csv` (2) | 本地 CSV 文件（核心库文件路径，`IHistoricalDataSourceService` 缺省为 `HistoricalDataSourceServiceCsv`）。 | 磁盘上的 CSV 文件。 |
| `Binance` (3) | 币安 K 线（走 `IBinanceUsdFutureService.GetOhlcvListAsync`，只读）。 | 已注册 `IBinanceUsdFutureService`（Runtime 会用 `RuntimeOptions` 凭据构造）。 |
| `Custom` (4) | 调用方提供的 `ITraditionalFinanceSourceDataService`。 | **必须**经 `AddQuantInfraNet(..., customDataSource: ...)` 传入实例。缺实例抛 `ArgumentException`（fail-fast）。详见 [custom-data-source-ch.md](custom-data-source-ch.md)。 |
| `Stooq` (5) | stooq.com 免费日线 CSV。无需 API Key。社区源，**无 SLA**。 | 无（需能访问 stooq.com）。 |
| `Alpaca` (6) | Alpaca Market Data（免费 IEX 层，官方 Alpaca.Markets SDK）。**真实历史数据的推荐默认**。 | `AlpacaApiKey` / `AlpacaApiSecret`（alpaca.markets 免费申请）。缺凭据抛 `ArgumentException`（fail-fast，绝不静默回退）。 |

---

## 2. `OrchestrationOptions` — 策略、风控与通知

**配置节 `Orchestration`** · 源码：`OrchestrationOptions.cs`

| 字段 | 类型 | 默认值 | 生效条件 | 一句话说明 |
|---|---|---|---|---|
| `Environment` | `ExchangeEnvironment` | `ExchangeEnvironment.Paper` | 始终 | Paper / Testnet / Live。**走 `AddQuantInfraNet` 时你不该自己设它**——`RunMode` 决定（Backtest 强制 Paper；Testnet/Live 各自设置）。 |
| `InitialEquityUsd` | `decimal` | `10000m` | Paper（及回测兜底） | 纸面账户起始权益（USD）。 |
| `MaxWeightPerSymbol` | `double` | `0.3` | Risk 阶段 | 规则 1：单标的权重上限 `|w|`。 |
| `MaxGrossExposure` | `double` | `1.0` | Risk 阶段 | 规则 2：总敞口上限 `Σ|w|`。 |
| `KillSwitchDrawdownRate` | `double` | `-0.15` | Risk 阶段 | 规则 3：kill-switch 回撤阈值（负数；`UnrealizedProfitRate` ≤ 该值即触发；附"建议全部平仓"理由）。 |
| `MinRebalanceDelta` | `double` | `0.01` | Execution 阶段 | 调仓死区：`|target − actual|` 小于该值则跳过。必须 ≥ 0。 |
| `Parameters` | `Dictionary<string,string>` | `{}`（键不区分大小写） | Strategy 阶段 | 策略参数。键由策略自行解释（`Strategy`、`Symbol`、`FastPeriod`、`SlowPeriod`、`WeightPerSymbol` …）。 |
| `Notifications` | `NotificationOptions` | 见下表 | Notification 阶段 | 通知路由配置。 |

### 2.1 `NotificationOptions`（源码：`Orchestration/Models/NotificationOptions.cs`）

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `Enabled` | `bool` | `true` | 总开关（false → 所有级别静默跳过）。 |
| `DingtalkAccessToken` | `string?` | `null` | 钉钉 access token（Info/Warning）。 |
| `DingtalkSecret` | `string?` | `null` | 钉钉签名 secret。 |
| `WeChatWebHook` | `string?` | `null` | 企业微信 WebHook URL（Warning/Critical）。 |
| `EmailRecipients` | `string[]` | `[]` | 邮件收件人（Critical）。 |
| `EmailSmtpServer` | `string?` | `null` | SMTP 主机。 |
| `EmailPort` | `int` | `587` | SMTP 端口。 |
| `EmailSender` | `string?` | `null` | 发件人地址。 |
| `EmailUsername` | `string?` | `null` | SMTP 用户名。 |
| `EmailPassword` | `string?` | `null` | SMTP 密码。 |

> 通道路由（源码：`Orchestration/Notifications/RoutingNotificationHub.cs`）：
> **Info** = 钉钉 · **Warning** = 钉钉 + 企微 · **Critical** = 钉钉 + 企微 + 邮件。
> 未配置的通道跳过；通道失败只记日志，**绝不**抛出。

### 2.2 `Orchestration.Parameters` — 策略自己的旋钮

| 键 | 谁用 | 含义 | 缺省值 |
|---|---|---|---|
| `Strategy` | Runtime 策略目录 | 要解析的策略名（内置：`MaCross`、`MeanReversion`、`PairTradingZScore`，或任意已注册的自定义描述符）。未知名启动即抛 `ArgumentException`（fail-fast）。 | `PairTradingZScore`（常量 `DefaultStrategyName`，见 `Orchestration/DependencyInjection.cs`） |
| `Symbol` | MaCross、QqqmReverseDca 等 | 交易标的（如 `AAPL`、`QQQM`）。 | 策略各自定义（MaCross 缺 Symbol 则拒绝并记事件；QqqmReverseDca 默认 `QQQM`）。 |
| `FastPeriod` / `SlowPeriod` | MaCross | 快/慢均线周期。 | `1` / `200` |
| `AllowShort` | MaCross | `true`/`false`：下穿时是否出 `Short`（否则 `Flat`）。 | `false` |
| `WeightPerSymbol` | TargetPosition 阶段 | 信号触发时的单标的目标权重。 | `0.3` |
| `MaPeriod` / `BaseWeight` / `AddIntensity` / `TrimIntensity` / `MaxWeight` / `MinWeight` | QqqmReverseDca（范例自定义阶段） | 逆向 MA200 定投公式参数。 | 见 `QqqmReverseDcaStrategy.DefaultParameters`。 |
| `LookbackBars` | `SignalDataLoader`（回退拉取） | context 缓存为空时拉取的 bar 数。 | `240` |
| `DataSource`（低层键） | `SignalDataLoader` | `"yahoo"` \| `"binance"` — context 无缓存时走哪个回退源。非法值回退 `yahoo`。 | `yahoo` |
| `ResolutionLevel` | `SignalDataLoader` | 回退拉取的 K 线级别。 | yahoo → `Daily`，binance → `Hourly` |

---

## 3. `BacktestOptions` — 回测专属旋钮

**配置节 `Backtest`** · 源码：`BacktestOptions.cs`

| 字段 | 类型 | 默认值 | 生效条件 | 一句话说明 |
|---|---|---|---|---|
| `InitialEquityUsd` | `decimal` | `10000m` | `RunMode` = Backtest | 仿真 broker 起始权益。 |
| `WarmupBars` | `int` | `0` | `RunMode` = Backtest | 前 N 根 bar 只用于指标预热，不交易。 |
| `CommissionBps` | `decimal` | `0m` | `RunMode` = Backtest | 手续费（**基点**），按成交名义价值从权益扣减。 |
| `SlippageBps` | `decimal` | `0m` | `RunMode` = Backtest | 滑点（**基点**），成交价按不利方向从标记价偏移。 |
| `FillTiming` | `FillTiming` | `FillTiming.SameBarClose` | `RunMode` = Backtest | 信号 bar 的决策在哪成交价执行。 |

### 3.1 `FillTiming` 枚举（源码：`BacktestOptions.cs`）

| 取值 | 含义 |
|---|---|
| `SameBarClose` (0，默认) | 在**信号 bar 的收盘价**成交。 |
| `NextBarOpen` (1) | 在信号 bar **下一根 bar 的开盘价**成交。想避免前视乐观就用这个。 |

> `FillTiming` 是**回测专属**概念，不得泄漏进 `IPipelineContext` / `OrchestrationOptions`。

---

## 4. 一份真实可跑的样例（来自仓库）

`src/Quant.Infra.Net.Runtime.Console/appsettings.json`：

```json
{
  "Runtime": {
    "RunMode": "Backtest",
    "DataSource": "Demo",
    "BinanceApiKey": "",
    "BinanceApiSecret": "",
    "AlpacaApiKey": "",
    "AlpacaApiSecret": ""
  },
  "Orchestration": {
    "InitialEquityUsd": 10000,
    "MaxWeightPerSymbol": 0.5,
    "MaxGrossExposure": 2.0,
    "KillSwitchDrawdownRate": -0.20,
    "MinRebalanceDelta": 0.02,
    "Parameters": {
      "Strategy": "MaCross",
      "Symbol": "AAPL",
      "FastPeriod": "1",
      "SlowPeriod": "200",
      "WeightPerSymbol": "0.3"
    }
  },
  "Backtest": {
    "InitialEquityUsd": 10000,
    "WarmupBars": 0,
    "CommissionBps": 0,
    "SlippageBps": 0,
    "FillTiming": "SameBarClose"
  }
}
```

Demo 宿主（`Program.cs`）把这三个节绑定到三个对象，然后调用
`services.AddQuantInfraNet(rt => …, o => …, b => …, strategyAssemblies: typeof(Program).Assembly)`。
改 `Runtime.RunMode` 一个值就切换全部行为——**这就是唯一的开关**。

## 5. 下一步

- [writing-a-strategy-ch.md](writing-a-strategy-ch.md) — 用 `Parameters` 驱动你自己的逻辑。
- [risk-management-ch.md](risk-management-ch.md) — 三个风控旋钮到底在做什么。
- [custom-data-source-ch.md](custom-data-source-ch.md) — `DataSource: Custom` 详解。
- [custom-broker-ch.md](custom-broker-ch.md) — `customBroker` 入口（不是配置字段，是 DI 参数）。
- [faq-ch.md](faq-ch.md) — 漏配凭据会发生什么。
