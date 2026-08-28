using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Execution;
using Quant.Infra.Net.Orchestration.Models;
using Binance.Net.Enums;

namespace Quant.Infra.Net.Orchestration.Execution;

/// <summary>
/// 目标持仓调仓执行模型：把 TargetPosition 列表与当前持仓的差异转换为券商调用
/// （复用 IBinanceUsdFutureService.SetUsdFutureHoldingsAsync / LiquidateUsdFutureAsync 语义）。
/// Target-position rebalancing model: turns target-vs-actual deltas into broker calls
/// (reusing IBinanceUsdFutureService.SetUsdFutureHoldingsAsync / LiquidateUsdFutureAsync semantics).
/// </summary>
public sealed class RebalanceExecutionModel : IExecutionModel
{
    private static readonly double FlatEpsilon = 1e-9;

    private readonly IBinanceUsdFutureService _broker;
    private readonly double _minRebalanceDelta;

    /// <summary>
    /// 创建调仓执行模型。
    /// Creates a rebalancing execution model.
    /// </summary>
    /// <param name="broker">券商服务（Paper 环境下为 PaperBinanceUsdFutureService）/ Broker service (PaperBinanceUsdFutureService in Paper mode).</param>
    /// <param name="options">编排配置（提供 MinRebalanceDelta 死区）/ Orchestration options (supply the MinRebalanceDelta dead zone).</param>
    /// <exception cref="ArgumentNullException">任一入参为 null 时抛出 / Thrown when any argument is null.</exception>
    /// <exception cref="ArgumentException">MinRebalanceDelta 为负时抛出 / Thrown when MinRebalanceDelta is negative.</exception>
    public RebalanceExecutionModel(IBinanceUsdFutureService broker, OrchestrationOptions options)
    {
        _broker = broker ?? throw new ArgumentNullException(nameof(broker));
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        if (options.MinRebalanceDelta < 0d)
        {
            throw new ArgumentException("MinRebalanceDelta must not be negative.", nameof(options));
        }

        _minRebalanceDelta = options.MinRebalanceDelta;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExecutionReport>> RebalanceAsync(IReadOnlyList<TargetPosition> targets, CancellationToken ct)
    {
        if (targets == null)
        {
            throw new ArgumentNullException(nameof(targets));
        }

        var reports = new List<ExecutionReport>(targets.Count);
        var equity = await _broker.GetusdFutureAccountBalanceAsync().ConfigureAwait(false);

        foreach (var target in targets)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(targets), "targets must not contain null entries.");
            }

            if (string.IsNullOrWhiteSpace(target.Symbol))
            {
                throw new ArgumentException("Target symbol must not be blank.", nameof(targets));
            }

            ct.ThrowIfCancellationRequested();
            var previousWeight = await GetActualWeightAsync(target.Symbol, equity).ConfigureAwait(false);
            var delta = target.TargetWeight - previousWeight;

            try
            {
                if (Math.Abs(delta) < _minRebalanceDelta)
                {
                    // 死区内：不调仓（保持当前持仓）/ inside dead zone: no trade
                    reports.Add(new ExecutionReport
                    {
                        Symbol = target.Symbol,
                        PreviousWeight = previousWeight,
                        CurrentWeight = previousWeight,
                        Success = true,
                        ErrorMessage = null
                    });
                    continue;
                }

                if (Math.Abs(target.TargetWeight) < FlatEpsilon)
                {
                    await _broker.LiquidateUsdFutureAsync(target.Symbol).ConfigureAwait(false);
                }
                else
                {
                    await _broker.SetUsdFutureHoldingsAsync(
                        target.Symbol,
                        Math.Abs(target.TargetWeight),
                        target.TargetWeight >= 0d ? PositionSide.Long : PositionSide.Short)
                        .ConfigureAwait(false);
                }

                var currentWeight = await GetActualWeightAsync(target.Symbol, equity).ConfigureAwait(false);
                reports.Add(new ExecutionReport
                {
                    Symbol = target.Symbol,
                    PreviousWeight = previousWeight,
                    CurrentWeight = currentWeight,
                    Success = true,
                    ErrorMessage = null
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                reports.Add(new ExecutionReport
                {
                    Symbol = target.Symbol,
                    PreviousWeight = previousWeight,
                    CurrentWeight = previousWeight,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        return reports;
    }

    private async Task<double> GetActualWeightAsync(string symbol, decimal equity)
    {
        var positions = await _broker.GetHoldingPositionAsync().ConfigureAwait(false);
        var position = positions.FirstOrDefault(p => string.Equals(p.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        if (position == null || equity <= 0m)
        {
            return 0d;
        }

        var signedNotional = (double)position.Quantity * (double)position.MarkPrice;
        return signedNotional / (double)equity;
    }
}
