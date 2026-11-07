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
}
