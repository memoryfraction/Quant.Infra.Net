using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using System.Globalization;

namespace Quant.Infra.Net.Orchestration.Stages;

/// <summary>
/// 目标持仓阶段：把标准化信号映射为目标组合权重。
/// Target-position stage: maps normalized signals to target portfolio weights.
/// 映射规则（三策略通用）：|TargetWeight| = 参数 WeightPerSymbol（默认 0.3，且不高于 MaxWeightPerSymbol）；
/// Direction=Long → +w，Short → −w，Flat → 0。
/// Mapping (all strategies): |TargetWeight| = parameter WeightPerSymbol (default 0.3, capped at MaxWeightPerSymbol);
/// Long → +w, Short → -, Flat → 0.
/// </summary>
public sealed class TargetPositionStage : IPipelineStage
{
    private readonly OrchestrationOptions _options;

    /// <summary>
    /// 创建目标持仓阶段。
    /// Creates the target-position stage.
    /// </summary>
    /// <param name="options">编排配置（不得为 null）/ Orchestration options (must not be null).</param>
    /// <exception cref="ArgumentNullException">入参为 null 时抛出 / Thrown when null.</exception>
    public TargetPositionStage(OrchestrationOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string Name => "TargetPosition";

    /// <inheritdoc />
    public Task ExecuteAsync(IPipelineContext context, CancellationToken ct)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        ct.ThrowIfCancellationRequested();
        var signals = context.Get<IReadOnlyList<Signal>>() ?? Array.Empty<Signal>();

        var weightParam = ParseWeight(context);
        var weight = Math.Min(weightParam, Math.Abs(_options.MaxWeightPerSymbol));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new List<TargetPosition>(signals.Count);
        foreach (var signal in signals)
        {
            if (signal == null || string.IsNullOrWhiteSpace(signal.Symbol))
            {
                continue;
            }

            if (!seen.Add(signal.Symbol))
            {
                continue; // 同一标的保留首个信号 / first signal wins per symbol
            }

            var directionWeight = signal.Direction switch
            {
                SignalDirection.Long => weight,
                SignalDirection.Short => -weight,
                _ => 0d
            };

            targets.Add(new TargetPosition
            {
                Symbol = signal.Symbol,
                TargetWeight = Math.Round(directionWeight, 8)
            });
        }

        context.Set<IReadOnlyList<TargetPosition>>(targets);
        var summary = targets.Count == 0
            ? "no targets"
            : string.Join("; ", targets.Select(t => string.Format(CultureInfo.InvariantCulture, "{0}={1:0.00}", t.Symbol, t.TargetWeight)));
        context.AddEvent(PipelineEvent.Create(context.RunId, Name, string.Format(CultureInfo.InvariantCulture, "targets: {0}", summary)));
        return Task.CompletedTask;
    }

    private static double ParseWeight(IPipelineContext context)
    {
        var raw = context.GetParameter("WeightPerSymbol");
        if (raw != null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0d && value <= 1d)
        {
            return value;
        }

        return 0.3d;
    }
}
