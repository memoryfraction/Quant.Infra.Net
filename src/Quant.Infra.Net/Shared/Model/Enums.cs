using System.Runtime.Serialization;

namespace Quant.Infra.Net.Shared.Model
{
    public enum AssetType
    {
        UsEquity = 1,                  // US Equity
        UsOption = 2,                  // US Option
        CryptoSpot = 3,                // Cryptocurrency Spot
        CryptoPerpetualContract = 4,   // Cryptocurrency Perpetual Contract
        CnEquity = 5,                  // China Equity
        HkEquity = 6,                  // Hong Kong Equity
        CryptoOption = 7               // Cryptocurrency Option
    }


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
        YahooFinance,
        Binance
    }


    public enum TradeDirection
    {
        Buy,  // 做多
        Sell  // 做空
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
        USD,  // 美元
        CNY,  // 人民币
        HKD,  // 港币
        USDT, // 泰达币
        USDC  // USD Coin
    }

}
