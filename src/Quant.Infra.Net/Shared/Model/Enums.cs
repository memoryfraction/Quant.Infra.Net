using System.Runtime.Serialization;

namespace Quant.Infra.Net.Shared.Model
{
    public enum Period
    {
        [EnumMember(Value = "d")]
        Daily,
        [EnumMember(Value = "wk")]
        Weekly,
        [EnumMember(Value = "mo")]
        Monthly,
        [EnumMember(Value = "h")]
        Hourly,
        [EnumMember(Value = "min")]
        Minute,
        [EnumMember(Value = "s")]
        Second,
        [EnumMember(Value = "t")]
        Tick
    }

    public enum DataSource
    {
        YahooFinance
    }


    public enum OrderAction
    {
        Buy,
        Sell
    }

    public enum OrderType
    {
        Market,
        Limit
    }

    public enum ContractSecurityType
    {
        Stock
    }

    public enum Currency
    {
        USD
    }
}
