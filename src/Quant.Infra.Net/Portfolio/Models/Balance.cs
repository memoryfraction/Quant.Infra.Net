using System;

namespace Quant.Infra.Net.Portfolio.Models
{
    /// <summary>
    /// 账户余额快照，包含现金、市值和未实现盈亏。
    /// Account balance snapshot with cash, market value, and unrealized PnL.
    /// </summary>
    public class Balance
    {
        /// <summary>
        /// 快照时间（UTC）/ Snapshot timestamp in UTC.
        /// </summary>
        public DateTime DateTime { get; set; }

        /// <summary>
        /// 净值（总资产 - 总负债）/ Net liquidation value (total assets - total liabilities).
        /// </summary>
        public decimal NetLiquidationValue { get; set; }

        /// <summary>
        /// 持仓市值 / Market value of positions.
        /// </summary>
        public decimal MarketValue { get; set; }

        /// <summary>
        /// 账户现金余额 / Cash balance in the account.
        /// </summary>
        public decimal Cash { get; set; }

        /// <summary>
        /// 未实现盈亏 / Unrealized profit and loss on open positions.
        /// </summary>
        public decimal UnrealizedPnL { get; set; }

        /// <summary>
        /// 默认构造函数 / Default constructor.
        /// </summary>
        public Balance()
        {
        }

        /// <summary>
        /// 创建余额快照。
        /// Create a balance snapshot with all fields.
        /// </summary>
        /// <param name="dateTime">快照时间（UTC）/ Snapshot timestamp in UTC.</param>
        /// <param name="netLiquidationValue">净值 / Net liquidation value.</param>
        /// <param name="marketValue">市值 / Market value of positions.</param>
        /// <param name="cash">现金余额 / Cash balance.</param>
        /// <param name="unrealizedPnL">未实现盈亏 / Unrealized PnL.</param>
        public Balance(DateTime dateTime, decimal netLiquidationValue, decimal marketValue, decimal cash, decimal unrealizedPnL)
        {
            DateTime = dateTime;
            NetLiquidationValue = netLiquidationValue;
            MarketValue = marketValue;
            Cash = cash;
            UnrealizedPnL = unrealizedPnL;
        }

        /// <summary>
        /// 返回余额摘要字符串 / Returns a summary string of the balance.
        /// </summary>
        public override string ToString()
        {
            return $"DateTime: {DateTime}, NetLiquidationValue: {NetLiquidationValue}, MarketValue: {MarketValue}, Cash: {Cash}, UnrealizedPnL: {UnrealizedPnL}";
        }
    }
}