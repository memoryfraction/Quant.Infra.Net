namespace Quant.Infra.Net.Backtest.Models;

/// <summary>
/// 成交价时机：同一 bar 收盘价，或下一 bar 开盘价。
/// Fill timing: the signal bar's close, or the next bar's open.
/// </summary>
/// <remarks>
/// 回测特有概念，不得泄漏进 IPipelineContext / OrchestrationOptions。
/// A backtest-only concept; it must not leak into IPipelineContext / OrchestrationOptions.
/// </remarks>
public enum FillTiming
{
    /// <summary>
    /// 信号 bar 的收盘价成交（默认）。
    /// Fill at the signal bar's close price (default).
    /// </summary>
    SameBarClose = 0,

    /// <summary>
    /// 信号 bar 之后一根 bar 的开盘价成交。
    /// Fill at the next bar's open price after the signal bar.
    /// </summary>
    NextBarOpen = 1,
}

/// <summary>
/// 回测参数（阶段一新增参数，§7.1）。
/// Backtest parameters (phase-1 additions, §7.1).
/// </summary>
public sealed class BacktestOptions
{
    /// <summary>
    /// 初始权益（USD）。
    /// Initial equity in USD.
    /// </summary>
    public decimal InitialEquityUsd { get; set; } = 10000m;

    /// <summary>
    /// 预热 bar 数（前 WarmupBars 根不交易，仅用于指标预热）。
    /// Warm-up bars (no trading; reserved for indicator warm-up).
    /// </summary>
    public int WarmupBars { get; set; } = 0;

    /// <summary>
    /// 手续费（基点，按成交名义价值从权益扣减）。
    /// Commission in basis points, deducted from equity on traded notional.
    /// </summary>
    public decimal CommissionBps { get; set; } = 0m;

    /// <summary>
    /// 滑点（基点，成交价在标记价上按不利方向偏移）。
    /// Slippage in basis points; the fill price is offset from the mark against the trade direction.
    /// </summary>
    public decimal SlippageBps { get; set; } = 0m;

    /// <summary>
    /// 成交价时机。
    /// Fill timing.
    /// </summary>
    public FillTiming FillTiming { get; set; } = FillTiming.SameBarClose;
}
