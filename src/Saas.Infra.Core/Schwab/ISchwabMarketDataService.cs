using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Saas.Infra.Core.Schwab
{
    /// <summary>
    /// 嘉信理财市场数据服务接口。
    /// Charles Schwab market data service interface.
    /// </summary>
    public interface ISchwabMarketDataService
    {
        /// <summary>
        /// 获取单个股票报价。
        /// Gets quote for a single symbol.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="symbol">股票代码。 / Symbol.</param>
        /// <returns>报价信息。 / Quote information.</returns>
        Task<SchwabQuote> GetQuoteAsync(Guid userId, string symbol);

        /// <summary>
        /// 获取多个股票报价。
        /// Gets quotes for multiple symbols.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="symbols">股票代码列表。 / List of symbols.</param>
        /// <returns>报价信息字典。 / Dictionary of quote information.</returns>
        Task<Dictionary<string, SchwabQuote>> GetQuotesAsync(Guid userId, IEnumerable<string> symbols);

        /// <summary>
        /// 获取历史价格数据。
        /// Gets historical price data.
        /// </summary>
        /// <param name="userId">用户 ID。 / User ID.</param>
        /// <param name="symbol">股票代码。 / Symbol.</param>
        /// <param name="periodType">周期类型（day, month, year, ytd）。 / Period type (day, month, year, ytd).</param>
        /// <param name="period">周期数量。 / Period count.</param>
        /// <param name="frequencyType">频率类型（minute, daily, weekly, monthly）。 / Frequency type (minute, daily, weekly, monthly).</param>
        /// <param name="frequency">频率。 / Frequency.</param>
        /// <returns>历史价格数据。 / Historical price data.</returns>
        Task<SchwabPriceHistory> GetPriceHistoryAsync(
            Guid userId,
            string symbol,
            string periodType = "month",
            int period = 1,
            string frequencyType = "daily",
            int frequency = 1);
    }

    /// <summary>
    /// 嘉信理财历史价格数据。
    /// Charles Schwab historical price data.
    /// </summary>
    public class SchwabPriceHistory
    {
        /// <summary>
        /// 股票代码。
        /// Symbol.
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// K线数据列表。
        /// Candles list.
        /// </summary>
        public List<SchwabCandle> Candles { get; set; } = new();

        /// <summary>
        /// 是否为空数据。
        /// Whether the data is empty.
        /// </summary>
        public bool IsEmpty { get; set; }
    }

    /// <summary>
    /// 嘉信理财 K 线数据。
    /// Charles Schwab candle data.
    /// </summary>
    public class SchwabCandle
    {
        /// <summary>
        /// 时间戳。
        /// Timestamp.
        /// </summary>
        public DateTimeOffset DateTime { get; set; }

        /// <summary>
        /// 开盘价。
        /// Open price.
        /// </summary>
        public decimal Open { get; set; }

        /// <summary>
        /// 最高价。
        /// High price.
        /// </summary>
        public decimal High { get; set; }

        /// <summary>
        /// 最低价。
        /// Low price.
        /// </summary>
        public decimal Low { get; set; }

        /// <summary>
        /// 收盘价。
        /// Close price.
        /// </summary>
        public decimal Close { get; set; }

        /// <summary>
        /// 成交量。
        /// Volume.
        /// </summary>
        public long Volume { get; set; }
    }
}
