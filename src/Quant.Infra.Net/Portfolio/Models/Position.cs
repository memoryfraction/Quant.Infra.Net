using Quant.Infra.Net.Shared.Model;
using System;
using System.Collections.Generic;

namespace Quant.Infra.Net.Portfolio.Models
{
    /// <summary>
    /// 持仓类
    /// </summary>
    public class Position
    {
        public DateTime EntryDateTime { get; set; }
        public string Symbol { get; set; }
        public decimal Quantity { get; set; }
        public decimal CostPrice { get; set; }
        public AssetType AssetType { get; set; }
        public decimal? UnrealizedProfitLoss { get; set; }
        public decimal GetUnrealizedPnL(decimal latestPrice)
        {
            return GetMarketValue(latestPrice) - Quantity * CostPrice;
        }

        public decimal GetMarketValue(decimal latestPrice)
        {
            return Quantity * latestPrice;
        }
    }

    public class Positions
    {
        public DateTime DateTime { get; set; }
        public List<Position> PositionList { get; set; } = new List<Position>();
    }
}