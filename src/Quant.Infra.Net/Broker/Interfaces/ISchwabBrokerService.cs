using Quant.Infra.Net.Portfolio.Models;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using Quant.Infra.Net.Broker.CharlesSchwab;

namespace Quant.Infra.Net.Broker.Interfaces
{
    /// <summary>
    /// Interface for Charles Schwab broker services.
    /// Charles Schwab 券商服务接口。
    /// </summary>
    public interface ISchwabBrokerService
    {
        /// <summary>
        /// Gets account information.
        /// 获取账户信息。
        /// </summary>
        Task<SchwabAccount> GetAccountAsync();

        /// <summary>
        /// Gets all positions.
        /// 获取所有持仓信息。
        /// </summary>
        Task<List<Position>> GetPositionsAsync();

        /// <summary>
        /// Gets a position for a specific symbol.
        /// 获取指定标的的持仓信息。
        /// </summary>
        Task<Position?> GetPositionAsync(string symbol);

        /// <summary>
        /// Gets a quote for a single symbol.
        /// 获取单个标的报价。
        /// </summary>
        Task<SchwabQuote> GetQuoteAsync(string symbol);

        /// <summary>
        /// Gets quotes for multiple symbols.
        /// 批量获取多个标的报价。
        /// </summary>
        Task<Dictionary<string, SchwabQuote>> GetQuotesAsync(List<string> symbols);

        /// <summary>
        /// Gets an option chain for a symbol.
        /// 获取指定标的的期权链。
        /// </summary>
        Task<SchwabOptionChain> GetOptionChainAsync(string symbol, string? contractType = null, int? strikeCount = null);

        /// <summary>
        /// Places an order.
        /// 提交订单。
        /// </summary>
        Task<string> PlaceOrderAsync(SchwabOrderRequest order);

        /// <summary>
        /// Gets an order by id.
        /// 根据订单编号获取订单。
        /// </summary>
        Task<SchwabOrder> GetOrderAsync(string orderId);

        /// <summary>
        /// Cancels an order by id.
        /// 根据订单编号取消订单。
        /// </summary>
        Task<bool> CancelOrderAsync(string orderId);

        /// <summary>
        /// Gets recent orders for the account.
        /// 获取账户最近订单。
        /// </summary>
        Task<List<SchwabOrder>> GetOrdersAsync(int maxResults = 100);

        /// <summary>
        /// Checks whether the market is open.
        /// 检查市场是否开盘。
        /// </summary>
        Task<bool> IsMarketOpenAsync();

        /// <summary>
        /// Gets price history (OHLCV candles) for a symbol.
        /// 获取指定标的的历史行情数据（OHLCV K 线）。
        /// </summary>
        /// <param name="symbol">The symbol (e.g., AAPL for equity, AAPL_20260515_185C00 for option).</param>
        /// <param name="startDate">Start date of the history range.</param>
        /// <param name="endDate">End date of the history range.</param>
        /// <param name="frequencyType">Frequency type: minute, daily, weekly, monthly.</param>
        /// <param name="frequency">Frequency multiplier (1, 5, 10, 15, 30 for minute; 1 for others).</param>
        /// <param name="needExtendedHoursData">Whether to include extended hours data.</param>
        /// <returns>Price history with candles array.</returns>
        Task<SchwabPriceHistory> GetPriceHistoryAsync(
            string symbol,
            DateTime startDate,
            DateTime endDate,
            string frequencyType,
            int frequency = 1,
            bool needExtendedHoursData = false);
    }
}
