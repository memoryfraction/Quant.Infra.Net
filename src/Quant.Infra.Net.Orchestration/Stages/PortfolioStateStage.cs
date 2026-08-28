using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;
using Quant.Infra.Net.Orchestration.Pipeline;
using System.Globalization;

namespace Quant.Infra.Net.Orchestration.Stages;

/// <summary>
/// 组合状态阶段：执行后读取持仓与余额，组装快照并保存。
/// Portfolio-state stage: after execution, reads positions and balance, builds the snapshot and saves it.
/// </summary>
public sealed class PortfolioStateStage : IPipelineStage
{
    private readonly IBinanceUsdFutureService _broker;
    private readonly IPortfolioStateStore _store;

    /// <summary>
    /// 创建组合状态阶段。
    /// Creates the portfolio-state stage.
    /// </summary>
    /// <param name="broker">券商服务（不得为 null）/ Broker service (must not be null).</param>
    /// <param name="store">状态存储（不得为 null）/ State store (must not be null).</param>
    /// <exception cref="ArgumentNullException">入参为 null 时抛出 / Thrown when null.</exception>
    public PortfolioStateStage(IBinanceUsdFutureService broker, IPortfolioStateStore store)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public string Name => "PortfolioState";

    /// <inheritdoc />
    public async Task ExecuteAsync(IPipelineContext context, CancellationToken ct)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        ct.ThrowIfCancellationRequested();

        StageMarketData.ApplyPaperMarks(context, _broker);

        var positions = (await _broker.GetHoldingPositionAsync().ConfigureAwait(false)).ToList();
        var equity = await _broker.GetusdFutureAccountBalanceAsync().ConfigureAwait(false);
        var unrealizedRate = await _broker.GetusdFutureUnrealizedProfitRateAsync().ConfigureAwait(false);

        var actualWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var position in positions)
        {
            if (string.IsNullOrWhiteSpace(position.Symbol))
            {
                continue;
            }

            if (equity <= 0m)
            {
                continue;
            }

            var signedNotional = (double)position.Quantity * (double)position.MarkPrice;
            actualWeights[position.Symbol] = signedNotional / (double)equity;
        }

        var targetWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var target in context.Get<IReadOnlyList<TargetPosition>>() ?? Array.Empty<TargetPosition>())
        {
            if (target != null && !string.IsNullOrWhiteSpace(target.Symbol))
            {
                targetWeights[target.Symbol] = target.TargetWeight;
            }
        }

        var snapshot = new PortfolioSnapshot
        {
            SnapshotUtc = DateTime.UtcNow,
            AccountEquityUsd = equity,
            ActualWeights = actualWeights,
            TargetWeights = targetWeights,
            UnrealizedProfitRate = unrealizedRate
        };

        await _store.SaveSnapshotAsync(snapshot, ct).ConfigureAwait(false);
        context.Set<PortfolioSnapshot>(snapshot);
        context.AddEvent(PipelineEvent.Create(context.RunId, Name,
            string.Format(CultureInfo.InvariantCulture, "snapshot saved: equity={0:0.00} positions={1}", equity, actualWeights.Count)));
    }
}
