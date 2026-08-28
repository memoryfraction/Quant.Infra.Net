using Binance.Net.Enums;

namespace Quant.Infra.Net.Backtest.Models;

/// <summary>
/// 单条回测成交记录（§7.2）。
/// A single backtest trade record (§7.2).
/// </summary>
/// <remarks>
/// Side 为"开仓腿方向"（开仓时）或"平仓动作方向"（平至空仓时）；
/// NotionalUsd = 0 表示平至空仓；FillPrice = 0 表示该腿无可用标记价（未成交估值）。
/// Side is the open leg's side (when opening) or the close action's side (when flattening);
/// NotionalUsd = 0 means flattened to zero; FillPrice = 0 means no mark existed (no fill valuation).
/// </remarks>
public sealed record BacktestTrade
{
    /// <summary>
    /// 成交时刻（模拟时间，UTC）。
    /// Fill instant (simulated time, UTC).
    /// </summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>
    /// 标的代码。
    /// Trading symbol.
    /// </summary>
    public string Symbol { get; init; } = "";

    /// <summary>
    /// 成交方向（Long/Short）。
    /// Trade direction (Long/Short).
    /// </summary>
    public PositionSide Side { get; init; }

    /// <summary>
    /// 成交价（含滑点偏移）。
    /// Fill price (slippage-adjusted).
    /// </summary>
    public decimal FillPrice { get; init; }

    /// <summary>
    /// 成交名义价值（USD；平至空仓时为 0）。
    /// Traded notional value in USD (0 when flattening to nothing).
    /// </summary>
    public decimal NotionalUsd { get; init; }

    /// <summary>
    /// 本腿手续费（USD）。
    /// Commission for this leg in USD.
    /// </summary>
    public decimal CommissionUsd { get; init; }
}
