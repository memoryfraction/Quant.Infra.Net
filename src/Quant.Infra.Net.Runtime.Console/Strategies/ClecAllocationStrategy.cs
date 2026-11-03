using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Execution;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Notifications;
using Quant.Infra.Net.Orchestration.Stages;
using Quant.Infra.Net.SourceData.Model;

namespace Quant.Infra.Net.Runtime.Console.Strategies;

/// <summary>
/// CLEC 式三桶配置（原型指数 / 二倍杠杆 ETF / 现金）的一个预设，例如 433 = 40% / 30% / 30%。
/// One CLEC-style three-sleeve mix (base index / 2x leveraged ETF / cash), e.g. 433 = 40% / 30% / 30%.
/// </summary>
/// <param name="Code">阵型代码（如 "433"）/ Mix code (e.g. "433").</param>
/// <param name="Base">原型指数权重 / Base-index weight.</param>
/// <param name="Lev">二倍杠杆 ETF 权重 / 2x leveraged ETF weight.</param>
/// <param name="Cash">现金（短期国债）权重 / Cash (T-bill) weight.</param>
/// <param name="UseEn">适用人群（英文）/ Who it is for (English).</param>
/// <param name="UseZh">适用人群（中文）/ Who it is for (Chinese).</param>
public sealed record ClecMix(string Code, double Base, double Lev, double Cash, string UseEn, string UseZh)
{
    /// <summary>组合 Beta = 原型 × 1 + 杠杆 × 2 / Portfolio beta = base × 1 + leveraged × 2.</summary>
    public double Beta => Base + 2 * Lev;

    /// <summary>
    /// 与 alpha-wealth-lab.com/planner/allocation/ 的 engine.js PRESETS 一一对应。
    /// Mirrors PRESETS in alpha-wealth-lab.com/planner/allocation/ (engine.js).
    /// </summary>
    public static readonly IReadOnlyList<ClecMix> Presets = new[]
    {
        new ClecMix("703", .7, 0, .3, "New investors / retirees without leverage", "新手 / 不开杠杆的退休者"),
        new ClecMix("802", .8, 0, .2, "Conservative, no leverage (~2% draw)", "保守，无杠杆（提领约 2%）"),
        new ClecMix("613", .6, .1, .3, "A little leverage", "入门加一点杠杆"),
        new ClecMix("433", .4, .3, .3, "Classic balance", "经典平衡"),
        new ClecMix("442", .4, .4, .2, "Aggressive: under 30, salaried, not living off assets", "积极：30 岁以下、有工资、不靠资产生活"),
        new ClecMix("514", .5, .1, .4, "Retired, 3–4% draw", "退休，提领 3–4%"),
        new ClecMix("424", .4, .2, .4, "Extra cash", "加厚现金"),
        new ClecMix("415", .4, .1, .5, "Retired, 4–5% draw", "退休，提领 4–5%"),
        new ClecMix("505", .5, 0, .5, "Elderly / maximum cash", "高龄 / 极致现金"),
        new ClecMix("226", .2, .2, .6, "Large near-term spending", "近期有大额刚性支出"),
    };
}

/// <summary>
/// CLEC 三桶配置策略阶段：每年第一个交易日（以及回测第一根 bar）把组合再平衡回目标权重；
/// 其余交易日不产生目标仓位（持仓随行情自然漂移）。只负责产出 TargetPosition，
/// 风控 / 执行 / 组合状态 / 通知交给内置阶段。
/// CLEC three-sleeve allocation stage: rebalances back to the target weights on the first trading day of
/// each calendar year (and on the first bar); on every other bar it emits no targets, so holdings drift
/// with the market. It only produces TargetPositions; risk / execution / portfolio state / notification
/// are the built-in stages.
/// </summary>
public sealed class ClecAllocationStage : IPipelineStage
{
    private readonly ClecMix _mix;
    private readonly string _baseSymbol;
    private readonly string _levSymbol;
    private readonly string _cashSymbol;
    private int _lastRebalanceYear = int.MinValue;

    /// <summary>构造 / Constructor.</summary>
    /// <param name="mix">目标阵型 / Target mix.</param>
    /// <param name="baseSymbol">原型指数标的（如 QQQ）/ Base-index symbol (e.g. QQQ).</param>
    /// <param name="levSymbol">二倍杠杆标的（如 QLD）/ 2x leveraged symbol (e.g. QLD).</param>
    /// <param name="cashSymbol">现金标的（如 BIL）/ Cash symbol (e.g. BIL).</param>
    public ClecAllocationStage(ClecMix mix, string baseSymbol, string levSymbol, string cashSymbol)
    {
        _mix = mix ?? throw new ArgumentNullException(nameof(mix));
        _baseSymbol = baseSymbol;
        _levSymbol = levSymbol;
        _cashSymbol = cashSymbol;
    }

    /// <summary>阶段名 / Stage name.</summary>
    public string Name => "ClecAllocation-" + _mix.Code;

    /// <summary>重平衡次数 / Number of rebalances performed.</summary>
    public int Rebalances { get; private set; }

    /// <inheritdoc />
    public Task ExecuteAsync(IPipelineContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        var union = context.Get<HashSet<Ohlcv>>();
        if (union is null || union.Count == 0)
        {
            context.Set<IReadOnlyList<TargetPosition>>(Array.Empty<TargetPosition>());
            return Task.CompletedTask;
        }

        // as-of 当前 bar 的日期 = 截至当前可见数据的最后一根 / the as-of bar = latest visible bar (no look-ahead).
        var asOf = union.Max(b => b.OpenDateTime);
        if (asOf.Year == _lastRebalanceYear)
        {
            context.Set<IReadOnlyList<TargetPosition>>(Array.Empty<TargetPosition>());
            return Task.CompletedTask;
        }

        _lastRebalanceYear = asOf.Year;
        Rebalances++;
        var now = DateTime.UtcNow;
        TargetPosition T(string symbol, double w) => new()
        {
            Symbol = symbol,
            TargetWeight = w,
            OriginSignal = new Signal
            {
                Symbol = symbol,
                GeneratedUtc = now,
                Direction = w > 0 ? SignalDirection.Long : SignalDirection.Flat,
                Strength = w,
                Reason = $"CLEC {_mix.Code} annual rebalance"
            }
        };

        // 权重为 0 的腿也显式下发，以便清仓 / zero-weight legs are sent too so they get flattened.
        var targets = new[] { T(_baseSymbol, _mix.Base), T(_levSymbol, _mix.Lev), T(_cashSymbol, _mix.Cash) };
        context.Set<IReadOnlyList<Signal>>(targets.Select(t => t.OriginSignal!).ToList());
        context.Set<IReadOnlyList<TargetPosition>>(targets);
        context.AddEvent(PipelineEvent.Create(context.RunId, Name, string.Format(CultureInfo.InvariantCulture,
            "{0:yyyy-MM-dd} rebalance → {1}={2:P0} {3}={4:P0} {5}={6:P0}",
            asOf, _baseSymbol, _mix.Base, _levSymbol, _mix.Lev, _cashSymbol, _mix.Cash)));
        return Task.CompletedTask;
    }

    /// <summary>
    /// 组装管道：本阶段 + 内置 Risk / Execution / PortfolioState / Notification。
    /// Builds the pipeline: this stage + the built-in Risk / Execution / PortfolioState / Notification stages.
    /// </summary>
    public static IEnumerable<IPipelineStage> BuildPipeline(IServiceProvider sp, ClecAllocationStage stage)
    {
        ArgumentNullException.ThrowIfNull(sp);
        return new IPipelineStage[]
        {
            stage,
            new RiskStage(sp.GetRequiredService<IRiskManager>(), sp.GetRequiredService<INotificationHub>(), sp.GetRequiredService<IPortfolioStateStore>()),
            new ExecutionStage(sp.GetRequiredService<IExecutionModel>(), sp.GetRequiredService<IExecutionBroker>()),
            new PortfolioStateStage(sp.GetRequiredService<IExecutionBroker>(), sp.GetRequiredService<IPortfolioStateStore>()),
            new NotificationStage(sp.GetRequiredService<INotificationHub>()),
        };
    }
}
