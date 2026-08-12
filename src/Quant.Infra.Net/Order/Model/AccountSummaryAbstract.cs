/// <summary>
/// 账户摘要基类，用于聚合交易账户的现金和净值信息。
/// Base class for account summary, aggregates cash and net liquidation value.
/// </summary>
namespace Quant.Infra.Net.Exchange.Model
{
    /// <summary>
    /// 账户摘要基类，用于聚合交易账户的现金和净值信息。
    /// Base class for account summary, aggregates cash and net liquidation value.
    /// </summary>
    public abstract class AccountSummaryAbstract
    {
        /// <summary>
        /// 总现金价值（美元）/ Total cash value in USD.
        /// </summary>
        public double TotalCashValue { get; set; }

        /// <summary>
        /// 账户净值（总资产 - 总负债）/ Net liquidation value (total assets - total liabilities).
        /// </summary>
        public double NetLiquidation { get; set; }
    }
}
