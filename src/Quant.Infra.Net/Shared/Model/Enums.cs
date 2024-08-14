using System.Runtime.Serialization;

namespace Quant.Infra.Net.Shared.Model
{
    public enum ResolutionLevel
    {
        [EnumMember(Value = "t")]
        Tick,
        [EnumMember(Value = "s")]
        Second,
        [EnumMember(Value = "min")]
        Minute,
        [EnumMember(Value = "h")]
        Hourly,
        [EnumMember(Value = "d")]
        Daily,
        [EnumMember(Value = "wk")]
        Weekly,
        [EnumMember(Value = "mo")]
        Monthly,
        [EnumMember(Value = "other")]
        Other
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
