using System;
using Quant.Infra.Net.Shared.Model;

namespace Quant.Infra.Net.Portfolio.Models
{
    public abstract class Trade
    {
        public string Symbol { get; set; }
        public DateTime TradeDate { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public Currency BaseCurrency { get; set; } // 交易的货币类型
        public AssetType AssetType { get; set; }
        public TradeDirection Direction { get; set; } // 使用TradeDirection代替IsBusy

        // 其他通用属性
    }
}
