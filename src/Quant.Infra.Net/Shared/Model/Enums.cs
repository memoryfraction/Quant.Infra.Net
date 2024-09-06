using System.Runtime.Serialization;

namespace Quant.Infra.Net.Shared.Model
{
    public enum AssetType
    {
        UsEquity = 1,  // 美国股票
        UsOption = 2,  // 美国期权
        CryptoSpot = 3,  // 数字货币现货
        CryptoPerpetualContract = 4,  // 数字货币永续合约
        CnEquity = 5,  // 中国股票
        HkEquity = 6,  // 香港股票
        CryptoOption = 7  // 数字货币期权
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
        YahooFinance
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
