# 完整教程 — 从"几行代码"到一次真实的 QQQM 回测

> 配套文档：[回测引擎快速上手（中文）](BacktestQuickStart-ch.md) / [EN](BacktestQuickStart-en.md)、[统一运行时快速上手（中文）](UnifiedRuntimeQuickStart-ch.md) / [EN](UnifiedRuntimeQuickStart-en.md)、[编排层快速上手（中文）](OrchestrationQuickStart-ch.md) / [EN](OrchestrationQuickStart-en.md)。本文带你从**最小的代码**出发，跑完一次**真实回测**（真实 QQQM 日线收盘价，Warmup = 200 根），贴出**真实控制台输出**、**权益曲线图**，解读**指标含义**，并给出**刻意的后续步骤**（Paper → Testnet/Live）——全部在一篇里。
>
> **English version**: [CompleteWalkthrough-en.md](CompleteWalkthrough-en.md)

---

## 1. "几行代码"——你需要写的全部

这就是仓库里 QQQM 逆向 MA200 定投示例的**完整策略接线**。**这是你需要写的全部代码。**其余一切——数据装载、风控闸门、执行、组合状态、通知——都由框架提供。

```csharp
// src/Quant.Infra.Net.Runtime.Console/Strategies/QqqmReverseDcaStrategy.cs
// （自定义阶段——你唯一要写的策略逻辑）

protected override async Task ExecuteCoreAsync(IPipelineContext context, CancellationToken ct)
{
    var symbol      = context.GetParameter("Symbol") ?? "QQQM";
    var maPeriod    = Math.Max(2, GetInt(context, "MaPeriod", 200));
    var baseWeight  = GetDouble(context, "BaseWeight", 0.5);
    var addIntensity= GetDouble(context, "AddIntensity", 1.5);
    var trimIntensity = GetDouble(context, "TrimIntensity", 1.0);
    var maxWeight   = GetDouble(context, "MaxWeight", 1.0);
    var minWeight   = GetDouble(context, "MinWeight", 0.0);

    var closes = await LoadClosesAsync(context, symbol, ct);   // 基类替你加载
    if (closes.Count < maPeriod)
    {
        Log(context, $"insufficient data for '{symbol}': {closes.Count} < {maPeriod} (no signal)");
        return;
    }

    var close = closes[^1];
    var sma   = closes.TakeLast(maPeriod).Average();
    var targetWeight = QqqmReverseDcaStrategy.ComputeTargetWeight(
        close, sma, baseWeight, addIntensity, trimIntensity, minWeight, maxWeight);

    var signal = new Signal { Symbol = symbol, GeneratedUtc = DateTime.UtcNow,
                              Direction = targetWeight > 0 ? SignalDirection.Long : SignalDirection.Flat,
                              Strength = targetWeight,
                              Reason = $"close={close} SMA{maPeriod}={sma:F4} targetWeight={targetWeight:F4}" };
    var target = new TargetPosition { Symbol = symbol, TargetWeight = targetWeight, OriginSignal = signal };
    Publish(context, signal, target);   // 基类统一执行槽位契约
}
```

以及**宿主接线**（同样在仓库里，约 20 行，见 [QqqmReverseDcaStrategy.RunExampleAsync](../src/Quant.Infra.Net.Runtime.Console/Strategies/QqqmReverseDcaStrategy.cs)）：

```csharp
var services = new ServiceCollection();
services.AddQuantInfraNet(
    rt => { rt.RunMode = RunMode.Backtest; rt.DataSource = DataSourceKind.Stooq; },
    o  => { foreach (var (k, v) in QqqmReverseDcaStrategy.DefaultParameters) o.Parameters[k] = v;
            o.MaxWeightPerSymbol = 1.0; o.MaxGrossExposure = 1.0; },
    b  => { b.WarmupBars = 200; });
using var sp = services.BuildServiceProvider();

var t0 = new DateTime(2021, 1, 4, 0, 0, 0, DateTimeKind.Utc);
var ohlcvs = sp.GetRequiredService<ITraditionalFinanceSourceDataService>()
    .DownloadOhlcvListAsync("QQQM", t0, DateTime.UtcNow).GetAwaiter().GetResult();
var data = new HistoricalDataSet(new Dictionary<string, IReadOnlyList<Ohlcv>> {
    ["QQQM"] = ohlcvs.OhlcvSet.OrderBy(b => b.OpenDateTime).ToList() });

var pipeline = new StrategyPipeline(QqqmReverseDcaStrategy.BuildPipeline(sp));
var broker   = (IBacktestBroker)sp.GetRequiredService<IBinanceUsdFutureService>();
var runner   = new BacktestRunner(pipeline, broker,
    sp.GetRequiredService<OrchestrationOptions>(), sp.GetRequiredService<BacktestOptions>());
var result   = await runner.RunAsync(data, new[] { "QQQM" });

Console.WriteLine($"CAGR={result.Metrics.Cagr:P2}  Sharpe={result.Metrics.SharpeRatio:F2}  MaxDrawdown={result.Metrics.MaxDrawdown:P2}");
```

**就这些。**不需要手写逐根 K 线的循环，不需要自己拼权益曲线，没有前视风险——runner 逐根 K 线回放，走的就是 Paper 模式所用的同一条 8 阶段管道。

---

## 2. 策略一句话

**QQQM 逆向 MA200 定投**：每天算 QQQM 收盘价的 `SMA200`。价格**低于**均线（弱势）时，策略**上调**目标权重（越跌买得越多）；价格**高于**均线（强势）时，策略**下调**目标权重（越涨越少持有）。公式：

```
ratio      = close / SMA200
deviation  = 1 − ratio                       // > 0 表示低于均线，< 0 表示高于均线
targetWeight = deviation >= 0 ? BaseWeight + AddIntensity × deviation
                             : BaseWeight + TrimIntensity × deviation
targetWeight = clamp(targetWeight, MinWeight, MaxWeight)
```

默认参数：`BaseWeight=0.5, AddIntensity=1.5, TrimIntensity=1.0, MinWeight=0.0, MaxWeight=1.0`。权重从 **0 %（价格远高于均线）**到 **100 %（价格远低于均线）**，在均线处经过 **50 %**。这就是典型的"逆向定投"——市场便宜时多买，贵时少买（或减仓）。

---

## 3. 真实运行——控制台实际打印了什么

运行命令：

```bash
cd Quant.Infra.Net
dotnet run --project src/Quant.Infra.Net.Runtime.Console -- QqqmDoc
```

> **数据源说明（读下面的数字之前请先读这段）。**运行**优先尝试 Stooq**（`stooq.com/q/d/l/?s=qqqm.us&i=d`，免费公开日线）。本文定稿时，`stooq.com` 对该接口返回的是 JavaScript 浏览器验证页（stooq.com 侧的反爬变更，**不是**本仓库的 bug——同一个 `StooqTraditionalFinanceSourceDataService` 用 fake `HttpMessageHandler` 写的单元测试仍然通过）。因此本次运行**回退到一份真实 QQQM 日线收盘价缓存快照**，取自 **Yahoo Finance 公开图表 API**，存于 `docs/assets/_qqqm_yfinance.json`（随时可用 `node docs/assets/qqqm_fetch_data.js` 重新拉取刷新）。两个数据源都是**无 SLA 保证的免费公开数据**，此处**仅用于研究/回测**。**当 stooq.com 恢复可达时，重新运行同一条命令会自动改用 Stooq，下面的数字会由该数据源重新生成。**本文中的数字全部来自下方展示的那次真实运行的 transcript，**没有编造任何数字**。

**真实控制台输出（逐字，2026-08-28/29 实际运行）：**

```
=== Quant.Infra.Net CompleteWalkthrough — QQQM reverse-MA200 DCA (real data backtest) ===
data source: Stooq (stooq.com free public daily bars; window 2021-01-04 -> now)
data source: stooq.com unreachable/blocked for this run — Stooq request timed out for QQQM at https://stooq.com/q/d/l/?s=qqqm.us&i=d.
data source (resolved): cached real QQQM daily closes (docs/assets/_qqqm_yfinance.json; refresh with docs/assets/qqqm_fetch_data.js)
bars loaded: 1420  (2021-01-04 .. 2026-08-28)
initial equity: 10000 USD | warmup bars: 200 | commission/slippage: 0 bps | fill: SameBarClose
=== backtest metrics (real run) ===
bars=1220  trades=673
CAGR=7.73%  Sharpe=0.04  Calmar=0.41
MaxDrawdown=-18.97%  WinRate=53.3%  ProfitFactor=1.16  Commission=0 USD
=== equity curve (every 20 bars) ===
2021-10-19  equity=10000.00 USD
2021-11-16  equity=10230.63 USD
2021-12-15  equity=10253.87 USD
2022-01-13  equity=10070.29 USD
2022-02-11  equity=9700.95 USD
2022-03-14  equity=9231.89 USD
2022-04-11  equity=9710.47 USD
2022-05-10  equity=8996.85 USD
2022-06-08  equity=9202.16 USD
2022-07-08  equity=8984.68 USD
2022-08-05  equity=9553.68 USD
2022-09-02  equity=9085.03 USD
2022-10-03  equity=8653.60 USD
2022-10-31  equity=8803.08 USD
2022-11-29  equity=8918.24 USD
2022-12-28  equity=8520.77 USD
2023-01-27  equity=9231.93 USD
2023-02-27  equity=9231.98 USD
2023-03-27  equity=9462.43 USD
2023-04-25  equity=9488.64 USD
2023-05-23  equity=9788.72 USD
2023-06-22  equity=10108.62 USD
2023-07-21  equity=10193.81 USD
2023-08-18  equity=10040.40 USD
2023-09-18  equity=10179.83 USD
2023-10-16  equity=10187.21 USD
2023-11-13  equity=10305.89 USD
2023-12-12  equity=10533.71 USD
2024-01-11  equity=10648.73 USD
2024-02-09  equity=10909.91 USD
2024-03-11  equity=10937.07 USD
2024-04-09  equity=10985.85 USD
2024-05-07  equity=10993.76 USD
2024-06-05  equity=11218.31 USD
2024-07-05  equity=11492.32 USD
2024-08-02  equity=11092.40 USD
2024-08-30  equity=11414.90 USD
2024-09-30  equity=11555.86 USD
2024-10-28  equity=11640.36 USD
2024-11-25  equity=11770.24 USD
2024-12-24  equity=12006.90 USD
2025-01-27  equity=11887.52 USD
2025-02-25  equity=11885.73 USD
2025-03-25  equity=11714.63 USD
2025-04-23  equity=11405.06 USD
2025-05-21  equity=12196.89 USD
2025-06-20  equity=12352.34 USD
2025-07-21  equity=12717.71 USD
2025-08-18  equity=12840.60 USD
2025-09-16  equity=12970.82 USD
2025-10-14  equity=13044.63 USD
2025-11-11  equity=13242.76 USD
2025-12-10  equity=13316.26 USD
2026-01-09  equity=13328.73 USD
2026-02-09  equity=13241.50 USD
2026-03-10  equity=13196.04 USD
2026-04-08  equity=13229.08 USD
2026-05-06  equity=14036.33 USD
2026-06-04  equity=14345.45 USD
2026-07-06  equity=14326.05 USD
2026-08-03  equity=14200.35 USD
2026-08-28  equity=14356.25 USD
=== 2022 drawdown window: strategy activity (real numbers) ===
QQQM 2022: first close=165.2400  last close=109.5300  change=-33.71%
per-bar target weight (first 12 + last 12 of the window, MA200 formula):
2022-01-03  close=165.2400  SMA200= 148.7604  targetWeight=0.3892
2022-01-04  close=163.0700  SMA200= 148.9208  targetWeight=0.4050
2022-01-05  close=158.1500  SMA200= 149.0601  targetWeight=0.4390
2022-01-06  close=157.9600  SMA200= 149.2089  targetWeight=0.4413
2022-01-07  close=156.2700  SMA200= 149.3503  targetWeight=0.4537
2022-01-10  close=156.4200  SMA200= 149.4827  targetWeight=0.4536
2022-01-11  close=158.6100  SMA200= 149.6275  targetWeight=0.4400
2022-01-12  close=159.3600  SMA200= 149.7782  targetWeight=0.4360
2022-01-13  close=155.4700  SMA200= 149.8995  targetWeight=0.4628
2022-01-14  close=156.2800  SMA200= 150.0139  targetWeight=0.4582
2022-01-18  close=152.6200  SMA200= 150.0965  targetWeight=0.4832
2022-01-19  close=150.7800  SMA200= 150.1709  targetWeight=0.4959
  ...
2022-12-30  close=109.5300  SMA200= 123.2522  targetWeight=0.6670
2022-12-29  close=109.6500  SMA200= 123.4042  targetWeight=0.6672
2022-12-28  close=107.0000  SMA200= 123.5310  targetWeight=0.7007
2022-12-27  close=108.3900  SMA200= 123.6506  targetWeight=0.6851
2022-12-23  close=110.0300  SMA200= 123.7761  targetWeight=0.6666
2022-12-22  close=109.7900  SMA200= 123.9073  targetWeight=0.6709
2022-12-21  close=112.5000  SMA200= 124.0469  targetWeight=0.6396
2022-12-20  close=110.8900  SMA200= 124.1496  targetWeight=0.6602
2022-12-19  close=111.0100  SMA200= 124.2634  targetWeight=0.6600
2022-12-16  close=112.8600  SMA200= 124.4023  targetWeight=0.6392
2022-12-15  close=114.0000  SMA200= 124.5421  targetWeight=0.6270
2022-12-14  close=117.7700  SMA200= 124.6863  targetWeight=0.5832
=== trades (real run) ===
2021-10-19  QQQM  side=Long  fill=154.26  notional=4067.02672384092 USD  commission=0 USD
2021-10-27  QQQM  side=Long  fill=156.21  notional=4010.56582387212 USD  commission=0 USD
2021-10-28  QQQM  side=Long  fill=157.91  notional=3918.12634476699 USD  commission=0 USD
2021-11-02  QQQM  side=Long  fill=159.89  notional=3832.07602645901 USD  commission=0 USD
2021-11-03  QQQM  side=Long  fill=161.63  notional=3735.00215171384 USD  commission=0 USD
2021-11-04  QQQM  side=Long  fill=163.68  notional=3617.42459128801 USD  commission=0 USD
2021-11-09  QQQM  side=Long  fill=162.51  notional=3727.03441723319 USD  commission=0 USD
2021-11-10  QQQM  side=Long  fill=160.12  notional=3887.82341081315 USD  commission=0 USD
2021-11-12  QQQM  side=Long  fill=162.23  notional=3781.89302134053 USD  commission=0 USD
2021-11-18  QQQM  side=Long  fill=165.11  notional=3648.73393099761 USD  commission=0 USD
2021-11-23  QQQM  side=Long  fill=163.39  notional=3790.67959568685 USD  commission=0 USD
2021-11-26  QQQM  side=Long  fill=160.8  notional=3970.31132120662 USD  commission=0 USD
2021-11-29  QQQM  side=Long  fill=164.26  notional=3769.11547668786 USD  commission=0 USD
2021-11-30  QQQM  side=Long  fill=161.89  notional=3925.28735157482 USD  commission=0 USD
2021-12-01  QQQM  side=Long  fill=159.22  notional=4095.30711395394 USD  commission=0 USD
2021-12-03  QQQM  side=Long  fill=157.54  notional=4211.6404398266 USD  commission=0 USD
2021-12-07  QQQM  side=Long  fill=163.62  notional=3873.55988444866 USD  commission=0 USD
2021-12-09  QQQM  side=Long  fill=161.96  notional=4000.14846912618 USD  commission=0 USD
2021-12-10  QQQM  side=Long  fill=163.65  notional=3911.12428384873 USD  commission=0 USD
2021-12-13  QQQM  side=Long  fill=161.27  notional=4066.35558969953 USD  commission=0 USD
2021-12-14  QQQM  side=Long  fill=159.67  notional=4172.26753041826 USD  commission=0 USD
2021-12-15  QQQM  side=Long  fill=163.12  notional=3981.9183340925 USD  commission=0 USD
2021-12-16  QQQM  side=Long  fill=159.12  notional=4233.68327314488 USD  commission=0 USD
2021-12-20  QQQM  side=Long  fill=156.59  notional=4402.96952447761 USD  commission=0 USD
2021-12-21  QQQM  side=Long  fill=160  notional=4220.8467726416 USD  commission=0 USD
2021-12-22  QQQM  side=Long  fill=161.96  notional=4119.20547359527 USD  commission=0 USD
2021-12-27  QQQM  side=Long  fill=165.9  notional=3910.53722548804 USD  commission=0 USD
2021-12-30  QQQM  side=Long  fill=164.51  notional=4033.52419080485 USD  commission=0 USD
2022-01-04  QQQM  side=Long  fill=163.07  notional=4158.67949430113 USD  commission=0 USD
2022-01-05  QQQM  side=Long  fill=158.15  notional=4453.03436721985 USD  commission=0 USD
2022-01-07  QQQM  side=Long  fill=156.27  notional=4577.61450442172 USD  commission=0 USD
2022-01-11  QQQM  side=Long  fill=158.61  notional=4469.52858761775 USD  commission=0 USD
2022-01-13  QQQM  side=Long  fill=155.47  notional=4660.91502027942 USD  commission=0 USD
2022-01-18  QQQM  side=Long  fill=152.62  notional=4824.55497440935 USD  commission=0 USD
2022-01-19  QQQM  side=Long  fill=150.78  notional=4923.08135048357 USD  commission=0 USD
2022-01-20  QQQM  side=Long  fill=148.81  notional=5071.36694867108 USD  commission=0 USD
2022-01-21  QQQM  side=Long  fill=144.64  notional=5406.24617793676 USD  commission=0 USD
2022-01-25  QQQM  side=Long  fill=141.82  notional=5622.8855503212 USD  commission=0 USD
2022-01-27  QQQM  side=Long  fill=140.36  notional=5730.02220355245 USD  commission=0 USD
2022-01-28  QQQM  side=Long  fill=144.68  notional=5418.25735339062 USD  commission=0 USD
chart written: <repo>/docs/assets/qqqm-reverse-dca-equity-curve.png
chart written: <repo>/docs/assets/qqqm-reverse-dca-target-weight.png
done.
```

*上方交易列表在输出中截断为 40 条；本次运行共产生 **673 笔交易**。完整 transcript：重新运行同一条命令即可（同一份缓存数据下结果确定）。*

---

## 4. 权益曲线（ScottPlot PNG）

运行会在本文旁写出两张 PNG（使用与 [Portfolio.DrawChart](../src/Quant.Infra.Net/Portfolio/Models/Portfolio.cs) 相同的 ScottPlot 5.0 画图模式）：

![QQQM 逆向 MA200 定投——回测权益曲线](assets/qqqm-reverse-dca-equity-curve.png)

*图 1 — 权益曲线，$10,000 起步，2021-10 → 现在（回放 1,220 根 K 线）。2022 熊市段（权益从 ≈$10,000 跌至 2022-12-28 的 ≈$8,521，2023 年底才重新站上 $10,000）正是策略逆向权重最高的时段。*

因为 2022 年的权重抬升是一个**仓位**效应（QQQM 越跌、组合里 QQQM 占比越高），而不是盈亏效应，在权益曲线上视觉上并不突出。运行还写出一张：

![QQQM 逆向 MA200 定投——目标权重随时间](assets/qqqm-reverse-dca-target-weight.png)

*图 2 — 目标权重 vs. 时间。权重从 ≈0.40（2022-01）爬升到 ≈0.85（2022 年中，回撤最深点），2022 其余时间维持在 0.6–0.7 区间，直到 QQQM 已明显收复 2022 低点后才回落至 0.4 以下。*

> 两张 PNG 均由 [QqqmDocWalkthrough.cs](../src/Quant.Infra.Net.Runtime.Console/QqqmDocWalkthrough.cs) 在运行时生成并提交到 `docs/assets/`。重新运行命令即可刷新。

---

## 5. 指标解读（这些数字到底是什么意思）

| 指标 | 本次运行值 | 衡量什么 | 对这个策略怎么读 |
|---|---|---|---|
| **CAGR**（年化复合收益率） | **7.73 %** | 权益曲线年化复合收益。`CAGR = (End/Start)^(1/years) − 1`。 | 逆向定投策略在**强牛年跑输 buy-and-hold**（持有较少），在**回撤段跑赢**（持有更多）。7.73 %/年（横跨 2022 熊市 + 2023–2026 复苏）是这组参数的真实年化。 |
| **Sharpe**（夏普比率） | **0.04** | 单位波动率对应的超额收益（此处为日收益年化）。 | 逆向定投系统性地在下跌市场加仓，**波动通常低于 buy-and-hold**，但**均值收益也更低**——Sharpe 才是诚实的对比。多年窗口下 Sharpe 接近 0，意味着这个窗口内该策略的风险调整后收益**仅略优于持币**；它不是"很好"的风险调整表现，要和 MaxDrawdown 一起读，不能孤立看。 |
| **MaxDrawdown**（最大回撤） | **−18.97 %** | 权益曲线峰谷最大跌幅。 | 这是策略"设计目标"要赢的指标：逆向定投的回撤应**浅于**同窗口 QQQM buy-and-hold。QQQM 自身从 ≈165.24（2022-01-03）跌到 ≈107.00（2022-12-28），峰谷 ≈ **−35.3 %**；策略权益在同一窗口峰谷 **−18.97 %**。这就是公式在卖的回撤保护——**真实存在，大约减半**。 |
| **WinRate**（胜率） | **53.3 %** | 单笔交易（开/平腿）盈利的占比。 | 日度再平衡的 DCA，多数"交易"是小额加减仓。WinRate 本身**不是**绩效指标——要和 **ProfitFactor = 1.16**（毛盈 ÷ 毛亏）一起读。53 % 胜率 + ProfitFactor ≈ 1.2 说明**按笔数看赢面一般**，收益来自**大波动时点的择时**，而非"笔笔小赢"。 |

### 2022 回撤期间策略实际在做什么（真实数字）

2022 是逆向 MA200 策略的**定义性考验**。从上文 transcript：

- **QQQM 2022 价格路径**：年初 **165.24**（2022-01-03），年末 **109.53**（2022-12-30），全年 **−33.71 %**（峰谷在 2022-12-28 的 107.00，约 **−35.3 %**）。
- **2022 目标权重**：年初 **0.3892**（2022-01-03，价格仍高于 SMA200=148.76），到 2022-01-19 已升至 **0.4959**，随后随 QQQM 跌破均线继续抬升——在**回撤最深点（2022 年中）触及 ≈0.85**（见图 2），2022 年末落在 **0.62–0.70** 区间（如 2022-12-28 收盘 107.00 时 **0.7007**）。换句话说，**在底部，策略想持有 ~70 % 的 QQQM**——接近基础 50 % 权重的 1.4 倍。
- **2022 权益路径**：$10,000 → **$8,520.77**（2022-12-28），日历年度 −14.8 %（MaxDrawdown −18.97 % 横跨 2021 年初峰到这一谷底）。
- **诚实的解读**：逆向定投**并不能躲过** 2022 回撤——它比固定权重**更深度参与**了回撤，赌的是随后的复苏（2023）足够大来补偿。2023 复苏中，策略权益曲线**重新站上 $10,000**（2023-06-22：$10,108.62），随后一路复利到 2026-08-28 的 **$14,356.25**。这笔交易的代价与收益都写在数字里：**更浅的回撤（−18.97 % vs −35.3 %）换来更低的 Sharpe（0.04）和低于标的 ETF 全周期收益的 CAGR。**

> **不要用单年盈亏给策略下结论。**策略的优劣（如果有）是**全周期**属性：回撤行为 + 复苏行为 + 换手成本，三者合一。

---

## 6. 后续步骤——先 Paper，再 Testnet/Live（刻意地做）

上面的运行是 **Backtest** 模式：初始拉取数据后零网络、内存 `BacktestBrokerService`、无真实订单。同样的代码、不改一行，就是 **Paper** 模式的运行方式——只有 `RunMode` 和 broker 注册变了。

### 6.1 切到 Paper（内存、实时时钟）

宿主 `appsettings.json`：

```json
{
  "Runtime": {
    "RunMode": "Paper",
    "DataSource": "Stooq"
  }
}
```

`Paper` 模式驱动**同一条** 8 阶段管道，每个墙钟周期跑一次，对接 `PaperBinanceUsdFutureService`（纯内存，执行时零网络）。策略代码、风控闸门、执行模型与 Backtest **逐字节一致**——这是 R4 parity 保证，由 `ParityRegressionTests` 钉住。

### 6.2 切到 Testnet 或 Live（两个必需条件）

本仓库默认不碰任何真实交易所。要上真实场所，必须**显式**同时满足以下两条；统一运行时入口（`AddQuantInfraNet`）在你设置了非 Paper 环境却没注册真实 broker 时会在启动时抛 `NotSupportedException`——**绝不静默退化为 Paper**：

1. **在调用 `AddQuantInfraNet` 之前注册真实 broker**（入口只会自动注册 Paper broker）：

   ```csharp
   builder.Services.AddSingleton<IBinanceUsdFutureService>(
       sp => new BinanceUsdFutureService(/* 真实 API key/secret，Testnet 或 Live */));
   builder.Services.AddQuantInfraNet(/* 你的配置 */);
   ```

2. **在 `RuntimeOptions` 里提供真实凭证**（`BinanceApiKey` / `BinanceApiSecret`）。`RunMode = Testnet` 或 `Live` 时凭证为空**启动即失败（fail-fast）**——这是护栏：演示绝不能碰真实资金。

```json
{
  "Runtime": {
    "RunMode": "Testnet",
    "BinanceApiKey": "<你的-testnet-api-key>",
    "BinanceApiSecret": "<你的-testnet-api-secret>"
  }
}
```

> ⚠️ **凭证绝不能提交进仓库**（代码规范 #9）。真实 key 只能放在你私有的本地配置或你自己宿主内的 secrets manager。

### 6.3 "fail-fast"长什么样

`RunMode = "Live"` 且凭证为空时，启动即打印：

```
System.NotSupportedException:
  RunMode 'Live' requires real broker credentials.
  Set Runtime:BinanceApiKey and Runtime:BinanceApiSecret,
  or pre-register a real IBinanceUsdFutureService before calling AddQuantInfraNet.
  (By design: the demo never silently degrades to Paper.)
```

这个异常**就是功能本身**——它是配置错误的宿主不可能误下真实订单的唯一原因。

---

## 7. 免责声明

- **本教程中的历史数据来自免费公开数据源**（stooq.com 可达时用 stooq.com；否则为取自 Yahoo Finance 公开图表 API 的缓存快照，存于 `docs/assets/_qqqm_yfinance.json`）。这些都是**无 SLA 保证的社区数据源**，此处**仅用于研究/回测**，**不是**生产用投资数据源。
- **本文不构成任何投资建议。**QQQM 逆向 MA200 定投策略是框架扩展点（自定义阶段、自定义管道、回测与纸面 parity）的**教学示例**。其历史回测表现**不预示未来结果**。
- **回测结果只取决于假设**：默认运行零佣金/零滑点、`SameBarClose` 成交、无市场冲击模型、无流动性约束。真实执行会更差。信任任何数字之前，先用 `CommissionBps` / `SlippageBps` / `FillTiming = NextBarOpen` 重跑。
- **2022 回撤是真实市场事件。**任何"2022 年全程做多 QQQM"的策略都亏了钱。逆向定投在该窗口的具体行为（在赌更大复苏的前提下*更深度*持有亏损）是**仓位选择**，不是预测。

---

*配套文件：[CompleteWalkthrough-en.md](CompleteWalkthrough-en.md) · [回测引擎快速上手（中文）](BacktestQuickStart-ch.md) · [编排层快速上手（中文）](OrchestrationQuickStart-ch.md) · [assets/qqqm-reverse-dca-equity-curve.png](assets/qqqm-reverse-dca-equity-curve.png) · [assets/qqqm-reverse-dca-target-weight.png](assets/qqqm-reverse-dca-target-weight.png) · [assets/qqqm_fetch_data.js](assets/qqqm_fetch_data.js)*