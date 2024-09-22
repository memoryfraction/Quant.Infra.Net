namespace Quant.Infra.Net.Exchange.Model
{
    public abstract class AccountSummaryAbstract
    {
        public double TotalCashValue { get; set; } // 总现金

        public double NetLiquidation { get; set; } // 总净值
    }
}