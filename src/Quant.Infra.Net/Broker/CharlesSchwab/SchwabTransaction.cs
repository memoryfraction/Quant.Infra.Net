using System;

namespace Quant.Infra.Net.Broker.CharlesSchwab;

/// <summary>
/// One account activity row from the Schwab transactions endpoint.
/// Schwab 交易记录接口返回的一条账户活动。
/// </summary>
public class SchwabTransaction
{
    /// <summary>Activity id. 活动编号。</summary>
    public string ActivityId { get; set; } = string.Empty;

    /// <summary>When the activity happened (UTC). 发生时间（UTC）。</summary>
    public DateTime TimeUtc { get; set; }

    /// <summary>Schwab type, e.g. ACH_RECEIPT, WIRE_OUT, JOURNAL. Schwab 类型。</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Status, e.g. VALID or CANCELED. 状态。</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Cash effect on the account: positive = money in, negative = money out. 对账户现金的影响：正数入金，负数出金。</summary>
    public decimal NetAmount { get; set; }

    /// <summary>Schwab description. 描述。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Instrument legs (shares moved, fees, cash). 交易明细腿（股数变动、费用、现金）。</summary>
    public System.Collections.Generic.List<SchwabTransferItem> TransferItems { get; set; } = new();
}

/// <summary>
/// One leg of a Schwab transaction.
/// Schwab 交易记录中的一条明细。
/// </summary>
public class SchwabTransferItem
{
    /// <summary>Instrument symbol, e.g. SPY or CURRENCY_USD. 证券代码。</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Instrument asset type, e.g. EQUITY, COLLECTIVE_INVESTMENT, OPTION, CURRENCY. 资产类型。</summary>
    public string AssetType { get; set; } = string.Empty;

    /// <summary>Quantity moved (Schwab "amount"). 变动数量。</summary>
    public decimal Amount { get; set; }

    /// <summary>Cash cost of this leg (negative = paid). 本腿现金成本（负数为支出）。</summary>
    public decimal Cost { get; set; }

    /// <summary>Price per unit. 单价。</summary>
    public decimal Price { get; set; }

    /// <summary>Fee type when this leg is a fee. 费用类型。</summary>
    public string FeeType { get; set; } = string.Empty;

    /// <summary>OPENING / CLOSING. 开仓/平仓。</summary>
    public string PositionEffect { get; set; } = string.Empty;
}
