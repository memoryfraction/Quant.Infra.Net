using Quant.Infra.Net.Shared.Model;
using System;
using System.Collections.Generic;


namespace Quant.Infra.Net.Portfolio.Models
{
    public abstract class PortfolioBase
    {
        public decimal InitCapital { get; set; } = 10000m;
        public abstract Currency BaseCurrency { get; set; }
        public List<OrderBase> Orders { get; set; } = new List<OrderBase>();
        public Dictionary<DateTime, PortfolioSnapshot> PortfolioSnapshots = new Dictionary<DateTime, PortfolioSnapshot>();

        // 用于存储每个时间点的市场价值
        public Dictionary<DateTime, decimal> MarketValueDic = new Dictionary<DateTime, decimal>();

        protected PortfolioBase()
        {
            Orders = new List<OrderBase>();
            PortfolioSnapshots = new Dictionary<DateTime, PortfolioSnapshot>();
            MarketValueDic = new Dictionary<DateTime, decimal>();
        }

        public void UpsertSnapshot(DateTime dateTime, Balance balance, Positions positions)
        {
            if (PortfolioSnapshots.ContainsKey(dateTime))
            {
                PortfolioSnapshots[dateTime].Balance = balance;
                PortfolioSnapshots[dateTime].Positions = positions;
            }
            else
            {
                PortfolioSnapshots.Add(dateTime, new PortfolioSnapshot
                {
                    DateTime = dateTime,
                    Balance = balance,
                    Positions = positions
                });
            }

            // 计算 MarketValue 并更新 marketValueDic
            decimal marketValue = balance.MarketValue;
            MarketValueDic[dateTime] = marketValue;
        }



    }

    // 股票投资组合
    public class StockPortfolio : PortfolioBase
    {
        public override Currency BaseCurrency { get; set; } = Currency.USD;


    }



    // 加密货币现货投资组合
    public class CryptoSpotPortfolio : PortfolioBase
    {
        public override Currency BaseCurrency { get; set; } = Currency.USDT;


    }

    // 永续合约投资组合
    public class PerpetualContractPortfolio : PortfolioBase
    {
        public override Currency BaseCurrency { get; set; } = Currency.USDT;

    }
}