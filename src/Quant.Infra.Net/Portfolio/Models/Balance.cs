using System;

namespace Quant.Infra.Net.Portfolio.Models
{
    public class Balance
    {
        public DateTime DateTime { get; set; }
        public decimal NetLiquidationValue { get; set; }
        public decimal MarketValue { get; set; }
        public decimal Cash { get; set; }
        public decimal UnrealizedPnL { get; set; }

        public Balance()
        {
        }

        public Balance(DateTime dateTime, decimal netLiquidationValue, decimal marketValue, decimal cash, decimal unrealizedPnL)
        {
            DateTime = dateTime;
            NetLiquidationValue = netLiquidationValue;
            MarketValue = marketValue;
            Cash = cash;
            UnrealizedPnL = unrealizedPnL;
        }

        public override string ToString()
        {
            return $"DateTime: {DateTime}, NetLiquidationValue: {NetLiquidationValue}, MarketValue: {MarketValue}, Cash: {Cash}, UnrealizedPnL: {UnrealizedPnL}";
        }
    }
}