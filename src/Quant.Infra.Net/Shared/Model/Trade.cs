using System;
using Quant.Infra.Net.Shared.Model;

namespace AlphaEngine.BLL.Models
{
    public abstract class Trade
    {
        public DateTime TradeDate { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } // 交易的货币类型
        public AssetType AssetType { get; set; }
        public TradeDirection Direction { get; set; } // 使用TradeDirection代替IsBusy

        // 其他通用属性
    }


    public class UsEquityTrade : Trade
    {
        // 其他属性和方法
    }

    public class UsOptionTrade : Trade
    {
        // 其他属性和方法
    }

    public class CryptoSpotTrade : Trade
    {
        // 其他属性和方法
    }

    public class CryptoPerpetualContractTrade : Trade
    {
        // 其他属性和方法
    }


}
