namespace Quant.Infra.Net.Orchestration.Models;

/// <summary>
/// 信号方向：做多、做空、空仓（经典 200MA 等单标的策略用 Flat 表示平仓）。
/// Signal direction: Long, Short, or Flat (Flat means closed/no position for single-symbol strategies such as the classic 200MA rule).
/// </summary>
public enum SignalDirection
{
    /// <summary>
    /// 做多 / Long.
    /// </summary>
    Long = 1,

    /// <summary>
    /// 做空 / Short.
    /// </summary>
    Short = 2,

    /// <summary>
    /// 空仓（平仓）/ Flat (close position).
    /// </summary>
    Flat = 3
}

/// <summary>
/// 标准化交易信号：标的、方向、强度与人类可读理由。
/// Normalized trading signal: symbol, direction, strength, and a human-readable reason.
/// </summary>
public class Signal
{
    /// <summary>
    /// 标的代码（如 BTCUSDT、AAPL）。
    /// Trading symbol (e.g., BTCUSDT, AAPL).
    /// </summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>
    /// 生成时间（UTC）。
    /// Generation timestamp (UTC).
    /// </summary>
    public DateTime GeneratedUtc { get; init; }

    /// <summary>
    /// 信号方向。
    /// Signal direction.
    /// </summary>
    public SignalDirection Direction { get; init; } = SignalDirection.Flat;

    /// <summary>
    /// 信号强度（0–1 或更宽域，由生成器定义）。
    /// Signal strength (0–1 or wider, defined by the generator).
    /// </summary>
    public double Strength { get; init; }

    /// <summary>
    /// 人类可读的信号理由（必须说明为什么给出该方向）。
    /// Human-readable reason (must explain why the direction was chosen).
    /// </summary>
    public string Reason { get; init; } = string.Empty;
}
