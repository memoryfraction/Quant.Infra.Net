namespace Quant.Infra.Net.Broker.CharlesSchwab;

/// <summary>
/// Schwab account summary.
/// Schwab 账户摘要。
/// </summary>
public class SchwabAccount
{
    /// <summary>
    /// Account number.
    /// 账户号码。
    /// </summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>
    /// Account type.
    /// 账户类型。
    /// </summary>
    public string AccountType { get; set; } = string.Empty;

    /// <summary>
    /// Cash balance.
    /// 现金余额。
    /// </summary>
    public decimal CashBalance { get; set; }

    /// <summary>
    /// Long market value.
    /// 多头市值。
    /// </summary>
    public decimal MarketValue { get; set; }

    /// <summary>
    /// NetLiquidateValue.
    /// 净清算价值。
    /// </summary>
    public decimal NetLiquidateValue { get; set; }

    /// <summary>
    /// Backward-compatible alias for NetLiquidateValue.
    /// NetLiquidateValue 的兼容别名。
    /// </summary>
    public decimal TotalEquity
    {
        get => NetLiquidateValue;
        set => NetLiquidateValue = value;
    }

    /// <summary>
    /// Buying power.
    /// 购买力。
    /// </summary>
    public decimal BuyingPower { get; set; }

    /// <summary>
    /// Unrealized profit or loss.
    /// 未实现盈亏。
    /// </summary>
    public decimal UnrealizedPnL { get; set; }

    /// <summary>
    /// Realized profit or loss.
    /// 已实现盈亏。
    /// </summary>
    public decimal RealizedPnL { get; set; }
}
