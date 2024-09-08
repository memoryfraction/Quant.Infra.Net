using System;
using System.Collections.Generic;
using Quant.Infra.Net.Shared.Model;


namespace Quant.Infra.Net.Portfolio.Models
{
    public interface IPortfolio
    {
        string Name { get; set; }
        decimal InitCash { get; set; }
        Currency BaseCurrency { get; set; }

        void ExecuteTrade(Trade trade);
        decimal GetHoldings(AssetType assetType);
        decimal GetCashBalance();
        void PrintPortfolioSummary();
    }

    public class Portfolio : IPortfolio
    {
        public string Name { get; set; }
        public decimal InitCash { get; set; }
        public Currency BaseCurrency { get; set; } = Currency.USD;
        private Dictionary<AssetType, decimal> Holdings { get; set; } // 记录各资产的持仓量

        public Portfolio(string name = "",
            decimal initialCashBalance = 10000m,
            Currency baseCurrency = Currency.USD)
        {
            Name = name;
            InitCash = initialCashBalance;
            BaseCurrency = baseCurrency;
            Holdings = new Dictionary<AssetType, decimal>();
        }

        public void ExecuteTrade(Trade trade)
        {
            if (trade == null)
                throw new ArgumentNullException(nameof(trade));

            // 根据交易方向更新投资组合
            switch (trade.Direction)
            {
                case TradeDirection.Buy:
                    HandleBuyTrade(trade);
                    break;
                case TradeDirection.Sell:
                    HandleSellTrade(trade);
                    break;
                default:
                    throw new ArgumentException("Invalid trade direction.");
            }
        }

        private void HandleBuyTrade(Trade trade)
        {
            decimal totalCost = trade.Quantity * trade.Price;

            if (InitCash < totalCost)
                throw new InvalidOperationException("Insufficient cash balance to execute buy trade.");

            // 扣除现金余额
            InitCash -= totalCost;

            // 更新持仓
            UpdateHoldings(trade.AssetType, trade.Quantity);
        }

        private void HandleSellTrade(Trade trade)
        {
            if (!Holdings.ContainsKey(trade.AssetType) || Holdings[trade.AssetType] < trade.Quantity)
                throw new InvalidOperationException("Insufficient holdings to execute sell trade.");

            // 更新持仓
            Holdings[trade.AssetType] -= trade.Quantity;

            // 增加现金余额
            InitCash += trade.Quantity * trade.Price;
        }

        private void UpdateHoldings(AssetType assetType, decimal quantity)
        {
            if (Holdings.ContainsKey(assetType))
            {
                Holdings[assetType] += quantity;
            }
            else
            {
                Holdings[assetType] = quantity;
            }
        }

        public decimal GetHoldings(AssetType assetType)
        {
            return Holdings.ContainsKey(assetType) ? Holdings[assetType] : 0;
        }

        public decimal GetCashBalance()
        {
            return InitCash;
        }

        public void PrintPortfolioSummary()
        {
            Console.WriteLine($"Portfolio: {Name}");
            Console.WriteLine($"Base Currency: {BaseCurrency}");
            Console.WriteLine($"Cash Balance: {InitCash}");

            Console.WriteLine("Holdings:");
            foreach (var holding in Holdings)
            {
                Console.WriteLine($"{holding.Key}: {holding.Value}");
            }
        }
    }
}