namespace Quant.Infra.Net.Exchange.Model.InteractiveBroker
{
    public class AccountSummaryIBKR : AccountSummaryAbstract
    {
        // 如果获取不到数值，则返回默认值 -1;
        private double grossPositionValue = -1;

        public int AssignedTimes { get; set; }
        public double Cushion { get; set; }
        public double DayTradesRemaining { get; set; }
        public double LookAheadNextChange { get; set; }
        public double AccruedCash { get; set; }
        public double AvailableFunds { get; set; }
        public double BuyingPower { get; set; }
        public double EquityWithLoadValue { get; set; }
        public double ExcessLiquidity { get; set; }
        public double FullAvailableFunds { get; set; }
        public double FullExcessLiquidity { get; set; }
        public double FullInitMarginReq { get; set; }
        public double FullMaintMarginReq { get; set; }
        public double GrossPositionValue { get; set; }
        public double InitMarginReq { get; set; }
        public double LookAheadAvailableFunds { get; set; }
        public double LookAheadExcessLiquidity { get; set; }
        public double LookAheadInitMarginReq { get; set; }
        public double LookAheadMaintMarginReq { get; set; }
        public double MaintMarginReq { get; set; }
        public double SMA { get; set; }
    }
}