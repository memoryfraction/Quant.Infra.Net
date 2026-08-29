using Quant.Infra.Net.Orchestration.Abstractions;
using Quant.Infra.Net.Orchestration.Models;

namespace Quant.Infra.Net.Orchestration.Execution;

/// <summary>
/// 目标持仓调仓执行模型：把 TargetPosition 列表与当前持仓的差异转换为券商无关的 IExecutionBroker 调用
/// （SetTargetWeightAsync / LiquidateAsync；具体券商由调用方注入的 IExecutionBroker 实现决定）。
/// Target-position rebalancing model: turns target-vs-actual deltas into broker-agnostic IExecutionBroker
/// calls (SetTargetWeightAsync / LiquidateAsync); the concrete broker is whatever IExecutionBroker the
/// caller injected.
/// </summary>
public sealed class RebalanceExecutionModel : IExecutionModel
{
    private static readonly double FlatEpsilon = 1e-9;

    private readonly IExecutionBroker _broker;
    private readonly double _minRebalanceDelta;

    /// <summary>
    /// 创建调仓执行模型。
    /// Creates a rebalancing execution model.
    /// </summary>
    /// <param name="broker">券商无关的执行接口（Paper 环境下为 BinanceUsdFutureExecutionBrokerAdapter 包装的 PaperBinanceUsdFutureService）/ Broker-agnostic execution surface (wraps PaperBinanceUsdFutureService in Paper mode).</param>
    /// <param name="options">编排配置（提供 MinRebalanceDelta 死区）/ Orchestration options (supply the MinRebalanceDelta dead zone).</param>
    /// <exception cref="ArgumentNullException">任一入参为 null 时抛出 / Thrown when any argument is null.</exception>
    /// <exception cref="ArgumentException">MinRebalanceDelta 为负时抛出 / Thrown when MinRebalanceDelta is negative.</exception>
    public RebalanceExecutionModel(IExecutionBroker broker, OrchestrationOptions options)
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
        var equity = await _broker.GetAccountEquityUsdAsync().ConfigureAwait(false);

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
                    await _broker.LiquidateAsync(target.Symbol).ConfigureAwait(false);
                }
                else
                {
                    await _broker.SetTargetWeightAsync(target.Symbol, target.TargetWeight).ConfigureAwait(false);
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
        var positions = await _broker.GetPositionsAsync().ConfigureAwait(false);
        var position = positions.FirstOrDefault(p => string.Equals(p.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        if (position == null || equity <= 0m)
        {
            return 0d;
        }

        var signedNotional = (double)position.Quantity * (double)position.MarkPrice;
        return signedNotional / (double)equity;
    }
}
