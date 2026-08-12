namespace Quant.Infra.Net.Exchange.Model.InteractiveBroker
{
    /// <summary>
    /// Interactive Brokers 持仓信息模型。
    /// Interactive Brokers position information model.
    /// </summary>
    public class PositionIBKR
    {
        /// <summary>
        /// IB 账户ID / The IBKR account identifier.
        /// </summary>
        public string Account { get; }

        /// <summary>
        /// 持仓数量（正数为多，负数为空）/ Position quantity (positive for long, negative for short).
        /// </summary>
        public decimal Quantity { get; }

        /// <summary>
        /// 平均建仓成本 / Average entry cost per unit.
        /// </summary>
        public double AverageCost { get; }
    }
}
