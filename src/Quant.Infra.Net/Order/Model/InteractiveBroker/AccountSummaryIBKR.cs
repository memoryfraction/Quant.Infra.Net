namespace Quant.Infra.Net.Exchange.Model.InteractiveBroker
{
    /// <summary>
    /// Interactive Brokers 账户摘要，包含保证金、资金和持仓信息。
    /// Interactive Brokers account summary with margin, funds, and position details.
    /// </summary>
    public class AccountSummaryIBKR : AccountSummaryAbstract
    {
        /// <summary>
        /// 总持仓市值（获取失败时返回 -1）/ Gross position value (-1 if unavailable).
        /// </summary>
        private double grossPositionValue = -1;

        /// <summary>
        /// 分配次数 / Number of times assigned to managed accounts.
        /// </summary>
        public int AssignedTimes { get; set; }

        /// <summary>
        /// 保证金缓冲比例（剩余流动性 / 净值）/ Cushion (excess liquidity as a percentage of net liquidation).
        /// </summary>
        public double Cushion { get; set; }

        /// <summary>
        /// 日内交易次数剩余（PDT规则）/ Remaining day-trade margin purchases remaining.
        /// </summary>
        public double DayTradesRemaining { get; set; }

        /// <summary>
        /// 预计下一笔交易的保证金影响 / Look-ahead next change in excess liquidity on the next trade.
        /// </summary>
        public double LookAheadNextChange { get; set; }

        /// <summary>
        /// 应计现金 / Accrued cash not yet settled.
        /// </summary>
        public double AccruedCash { get; set; }

        /// <summary>
        /// 可用资金（不考虑日频交易限制）/ Funds available for trading (not accounting for PDT).
        /// </summary>
        public double AvailableFunds { get; set; }

        /// <summary>
        /// 购买力 / Current buying power.
        /// </summary>
        public double BuyingPower { get; set; }

        /// <summary>
        /// 扣除负债后的净值 / Net liquidation value after deducting any debit.
        /// </summary>
        public double EquityWithLoadValue { get; set; }

        /// <summary>
        /// 剩余流动性（净值 - 维持保证金）/ Excess liquidity (net liquidation minus maint margin).
        /// </summary>
        public double ExcessLiquidity { get; set; }

        /// <summary>
        /// 完整可用资金（不考虑 PDT，包含FA分配）/ Full available funds without PDT, including FA allocations.
        /// </summary>
        public double FullAvailableFunds { get; set; }

        /// <summary>
        /// 完整剩余流动性 / Full excess liquidity (including FA allocations).
        /// </summary>
        public double FullExcessLiquidity { get; set; }

        /// <summary>
        /// 完整初始保证金要求 / Full initial margin requirement (including FA allocations).
        /// </summary>
        public double FullInitMarginReq { get; set; }

        /// <summary>
        /// 完整维持保证金要求 / Full maintenance margin requirement (including FA allocations).
        /// </summary>
        public double FullMaintMarginReq { get; set; }

        /// <summary>
        /// 持仓总价值（含期权估值）/ Gross position value including option valuation adjustments.
        /// </summary>
        public double GrossPositionValue { get; set; }

        /// <summary>
        /// 初始保证金要求 / Current initial margin requirement.
        /// </summary>
        public double InitMarginReq { get; set; }

        /// <summary>
        /// 前瞻可用资金（含下一笔交易的影响）/ Look-ahead available funds including the next trade impact.
        /// </summary>
        public double LookAheadAvailableFunds { get; set; }

        /// <summary>
        /// 前瞻剩余流动性 / Look-ahead excess liquidity including the next trade impact.
        /// </summary>
        public double LookAheadExcessLiquidity { get; set; }

        /// <summary>
        /// 前瞻初始保证金要求 / Look-ahead initial margin requirement including the next trade.
        /// </summary>
        public double LookAheadInitMarginReq { get; set; }

        /// <summary>
        /// 前瞻维持保证金要求 / Look-ahead maintenance margin requirement including the next trade.
        /// </summary>
        public double LookAheadMaintMarginReq { get; set; }

        /// <summary>
        /// 当前维持保证金要求 / Current maintenance margin requirement.
        /// </summary>
        public double MaintMarginReq { get; set; }

        /// <summary>
        /// 特殊豁免额（监管净流动资产）/ Special margin account value (SMC) - regulatory net liquidation.
        /// </summary>
        public double SMA { get; set; }
    }
}
