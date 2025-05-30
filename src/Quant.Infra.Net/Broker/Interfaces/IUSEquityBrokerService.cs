using Alpaca.Markets;
using Quant.Infra.Net.Shared.Model;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Broker.Interfaces
{
    /// <summary>
    /// Interface for U.S. equity broker services.  
    /// 美股经纪服务接口定义。
    /// </summary>
    public interface IUSEquityBrokerService
    {
        /// <summary>
        /// Current exchange environment (e.g., TestNet or Live).  
        /// 当前交易环境（例如：测试网或实盘）。
        /// </summary>
        ExchangeEnvironment ExchangeEnvironment { get; set; }


        /// <summary>
        /// Get the current market value of the portfolio.  
        /// 获取当前投资组合的市值。
        /// </summary>
        /// <returns>The market value as a decimal.  
        /// 返回市值（decimal 类型）。</returns>
        Task<decimal> GetPortfolioMarketValueAsync();

        /// <summary>
        /// Check if the given symbol has an open position.  
        /// 检查指定的标的是否存在持仓。
        /// </summary>
        /// <param name="symbol">The trading symbol.  
        /// 交易标的代码。</param>
        /// <returns>True if there is a position, false otherwise.  
        /// 若存在持仓返回 true，否则返回 false。</returns>
        Task<bool> HasPositionAsync(string symbol);

        /// <summary>
        /// Get the unrealized profit rate
        /// 获取未实现盈亏比率。
        /// </summary>
        /// <returns>The unrealized profit rate as a double.  
        /// 返回未实现收益率（double 类型）。</returns>
        Task<double> GetUnrealizedProfitRateAsync();

        /// <summary>
        /// Liquidate the position of the specified symbol.  
        /// 清仓指定标的的持仓。
        /// </summary>
        /// <param name="symbol">The trading symbol.  
        /// 要清仓的标的代码。</param>
        /// <returns>A task representing the asynchronous operation.  
        /// 表示清仓操作的异步任务。</returns>
        Task LiquidateAsync(string symbol);

        /// <summary>
        /// Set the target holding proportion for the specified symbol.  
        /// 设置指定标的的目标持仓比例。
        /// </summary>
        /// <param name="symbol">The trading symbol.  
        /// 交易标的代码。</param>
        /// <param name="rate">The target position proportion (e.g., 0.1 = 10%).  
        /// 持仓比例（如 0.1 表示 10%）。</param>
        /// <returns>A task representing the asynchronous operation.  
        /// 表示设置持仓的异步任务。</returns>
        Task SetHoldingsAsync(string symbol, double rate);


        Task<string> GetFormattedAccountSummaryAsync();

        Task<bool> IsMarketOpeningAsync();

        // 新增：获取账户信息
        Task<IAccount> GetAccountAsync();


    }
}
