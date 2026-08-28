using Quant.Infra.Net.Orchestration.Models;

namespace Quant.Infra.Net.Orchestration.Abstractions;

/// <summary>
/// 信号生成器抽象：根据上下文中的行情数据产出标准化信号。
/// Signal generator abstraction: produces normalized signals from the market data present in the context.
/// </summary>
public interface ISignalGenerator
{
    /// <summary>
    /// 策略 ID（"PairTradingZScore" | "MaCross" | "MeanReversion" | 自定义）。
    /// Strategy id ("PairTradingZScore" | "MaCross" | "MeanReversion" | custom).
    /// </summary>
    string Id { get; }

    /// <summary>
    /// 生成信号；数据不足时返回空集并记录事件，不得抛业务异常。
    /// Generates signals; when data is insufficient, returns an empty set and records an event instead of throwing business exceptions.
    /// </summary>
    /// <param name="context">管道上下文（不得为 null）/ Pipeline context (must not be null).</param>
    /// <param name="ct">取消令牌 / Cancellation token.</param>
    /// <returns>信号列表（可能为空）/ List of signals (possibly empty).</returns>
    Task<IReadOnlyList<Signal>> GenerateSignalsAsync(IPipelineContext context, CancellationToken ct);
}
