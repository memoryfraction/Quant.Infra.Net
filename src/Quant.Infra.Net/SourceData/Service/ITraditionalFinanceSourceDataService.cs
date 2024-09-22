using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Quant.Infra.Net.SourceData.Service
{
    /// <summary>
    /// 原始数据接口， 同步原始数据;
    /// </summary>
    public interface ITraditionalFinanceSourceDataService
    {
        /// <summary>
        /// 开始同步数据，在指定路径形成数据文件， 返回Ohlcvs
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="startDt"></param>
        /// <param name="endDt"></param>
        /// <param name="Period"></param>
        /// <returns></returns>
        Task<Ohlcvs> BeginSyncSourceDailyDataAsync(string symbol, DateTime startDt, DateTime endDt, string fullPathFileName, ResolutionLevel Period = ResolutionLevel.Daily);

        /// <summary>
        /// download financial OHLCV data from Yahoo api
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="startDt"></param>
        /// <param name="endDt"></param>
        /// <param name="Period"></param>
        /// <param name="dataSource"></param>
        /// <returns></returns>
        Task<Ohlcvs> DownloadOhlcvListAsync(string symbol, DateTime startDt, DateTime endDt, ResolutionLevel Period = ResolutionLevel.Daily, DataSource dataSource = DataSource.YahooFinance);

        Task<List<Ohlcv>> GetOhlcvListAsync(string fullPathFilename);

        Task SaveOhlcvListAsync(IEnumerable<Ohlcv> ohlcvList, string fullPathFileName);

        /// <summary>
        /// get sp500 symbols from wiki
        /// </summary>
        /// <returns></returns>
        Task<IEnumerable<string>> GetSp500SymbolsAsync(int number = 500);
    }
}