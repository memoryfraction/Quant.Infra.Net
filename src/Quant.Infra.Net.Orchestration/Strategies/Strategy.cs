using System.Globalization;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Signals;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Orchestration.Strategies;

/// <summary>
/// 策略基类（抽象）：所有自定义策略阶段继承本类而非各自实现 <see cref="IPipelineStage"/>（依赖抽象而非具体，SOLID）。
/// 集中"策略阶段"的公共骨架——
///   · 数据装载：统一走 <see cref="SignalDataLoader"/>（context 缓存优先 → 数据源回退 → 空序列），子类不自造装载规则；
///   · 参数读取：Orchestration.Parameters（均带默认值，键比较忽略大小写）；
///   · 事件日志：统一 <see cref="PipelineEvent"/> 记录；
///   · 产出契约：<see cref="Publish"/> 按内置 Risk/Execution/PortfolioState 阶段所读的同槽位（IReadOnlyList&lt;Signal&gt; /
///     IReadOnlyList&lt;TargetPosition&gt;）写入 context，保证信号/目标可被下游四阶段消费。
/// 子类只需实现 <see cref="ExecuteCoreAsync"/> 表达"自己的"信号逻辑；其余骨架由本基类统一提供。
/// Strategy base class (abstract): custom strategy stages inherit this class instead of each implementing IPipelineStage
/// (depend on abstractions, not on concretes — SOLID).
/// It centralizes the shared skeleton of a "strategy stage" —
///   · data loading: single implementation via SignalDataLoader (context cache first → source fallback → empty series);
///   · parameters: Orchestration.Parameters (defaults inline; case-insensitive keys);
///   · event logging: uniform PipelineEvent records;
///   · publish contract: Publish() writes the exact slots the built-in Risk/Execution/PortfolioState stages read.
/// A subclass only implements ExecuteCoreAsync with its own signal logic; the rest is inherited.
/// </summary>
public abstract class Strategy : IPipelineStage
{
    private readonly ITraditionalFinanceSourceDataService? _yahooData;
    private readonly IBinanceUsdFutureService? _binanceService;

    /// <summary>
    /// 初始化策略基类（数据装载依赖均可选：回测下 context 已注入缓存切片，走缓存路径零网络；
    /// 实盘/纸面下提供真实数据源以便回退拉取）。
    /// Initializes the strategy base (loading dependencies optional: under Backtest the context already holds the cached
    /// slice, so the cache path is used with zero network; under Live/Paper supply real sources for the fallback fetch).
    /// </summary>
    /// <param name="yahooData">传统行情数据源（可选）/ Traditional finance data source (optional).</param>
    /// <param name="binanceService">币安合约服务（可选）/ Binance futures service (optional).</param>
    protected Strategy(
        ITraditionalFinanceSourceDataService? yahooData,
        IBinanceUsdFutureService? binanceService)
    {
        _yahooData = yahooData;
        _binanceService = binanceService;
    }

    /// <inheritdoc />
    /// <summary>策略名（子类重写为各自的策略名；不得为空白、不得与管道内其他阶段重名）。
    /// Strategy name (subclass overrides with its own name; must be non-blank and unique in the pipeline).</summary>
    public abstract string StrategyName { get; }

    /// <inheritdoc />
    public string Name => StrategyName;

    /// <inheritdoc />
    /// <summary>
    /// 模板方法：校验上下文后交由子类 <see cref="ExecuteCoreAsync"/> 产出信号（子类只写自己的逻辑）。
    /// Template method: validates the context, then delegates to the subclass ExecuteCoreAsync (subclass writes only its own logic).
    /// </summary>
    /// <param name="context">管道上下文（不得为 null）/ Pipeline context (must not be null).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <exception cref="ArgumentNullException">context 为 null 时抛出 / Thrown when context is null.</exception>
    public async Task ExecuteAsync(IPipelineContext context, CancellationToken ct)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        await ExecuteCoreAsync(context, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 子类实现：读取行情 → 计算 → 调 <see cref="Publish"/>（或自行 <c>context.Set</c>）产出一条信号与目标。
    /// Subclass implements: read data → compute → call Publish() (or context.Set) to emit the signal + target.
    /// </summary>
    /// <param name="context">管道上下文 / Pipeline context.</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    protected abstract Task ExecuteCoreAsync(IPipelineContext context, CancellationToken ct);

    /// <summary>
    /// 读取标的升序收盘价（复用 <see cref="SignalDataLoader"/> 装载规则：缓存优先 → 数据源回退 → 空序列，不自造）。
    /// Reads the ascending close series reusing SignalDataLoader's rule (cache first → source fallback → empty series).
    /// </summary>
    /// <param name="context">管道上下文 / Pipeline context.</param>
    /// <param name="symbol">标的代码 / Trading symbol.</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    protected async Task<IReadOnlyList<double>> LoadClosesAsync(IPipelineContext context, string symbol, CancellationToken ct)
        => await SignalDataLoader.LoadClosesAsync(context, symbol, ct, _yahooData, _binanceService).ConfigureAwait(false);

    /// <summary>
    /// 按内置四阶段的槽位契约产出一条信号 + 一条目标（IReadOnlyList&lt;Signal&gt; 与 IReadOnlyList&lt;TargetPosition&gt; 槽），
    /// 并记录一条事件。数据不足时应不调用本方法（下游阶段自然无动作）。
    /// Emits one signal + one target using the built-in four stages' slot contract
    /// (IReadOnlyList&lt;Signal&gt; and IReadOnlyList&lt;TargetPosition&gt; slots) and records an event.
    /// When data is insufficient, do not call this (downstream stages become no-ops).
    /// </summary>
    /// <param name="context">管道上下文 / Pipeline context.</param>
    /// <param name="signal">信号（不得为 null）/ Signal (must not be null).</param>
    /// <param name="target">目标仓位（不得为 null）/ Target position (must not be null).</param>
    /// <exception cref="ArgumentNullException">signal / target 为 null 时抛出 / Thrown when signal / target is null.</exception>
    protected void Publish(IPipelineContext context, Signal signal, TargetPosition target)
    {
        if (signal == null)
        {
            throw new ArgumentNullException(nameof(signal));
        }

        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        context.Set<IReadOnlyList<Signal>>(new[] { signal });
        context.Set<IReadOnlyList<TargetPosition>>(new[] { target });
        context.AddEvent(PipelineEvent.Create(context.RunId, Name, $"{signal.Symbol} → {signal.Direction} ({signal.Strength:0.00})"));
    }

    /// <summary>读取整型参数（缺省/非法时返回默认值）/ Reads an int parameter (returns the default when missing/invalid).</summary>
    protected static int GetInt(IPipelineContext context, string key, int defaultValue)
        => int.TryParse(context.GetParameter(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;

    /// <summary>读取双精度参数（缺省/非法时返回默认值）/ Reads a double parameter (returns the default when missing/invalid).</summary>
    protected static double GetDouble(IPipelineContext context, string key, double defaultValue)
        => double.TryParse(context.GetParameter(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : defaultValue;

    /// <summary>记录一条普通事件 / Records a normal event.</summary>
    protected void Log(IPipelineContext context, string message)
        => context.AddEvent(PipelineEvent.Create(context.RunId, Name, message));
}