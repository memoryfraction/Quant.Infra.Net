using Quant.Infra.Net.Shared.Model;
using System;
using System.Collections.Generic;

namespace Quant.Infra.Net.SourceData.Model
{
    /// <summary>
    /// OHLCV 数据集合，包含一组 K 线及其元信息。
    /// Container for a collection of OHLCV candlesticks with metadata.
    /// </summary>
    public class Ohlcvs
    {
        /// <summary>
        /// 交易品种代码 / Trading symbol (e.g., AAPL, BTCUSDT).
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// K 线时间粒度（分钟、小时、天等）/ Resolution level of the candles (minute, hour, day, etc.).
        /// </summary>
        public ResolutionLevel ResolutionLevel { get; set; }

        /// <summary>
        /// 数据起始时间（UTC）/ Start of the data range in UTC.
        /// </summary>
        public DateTime StartDateTimeUtc { get; set; }

        /// <summary>
        /// 数据截止时间（UTC）/ End of the data range in UTC.
        /// </summary>
        public DateTime EndDateTimeUtc { get; set; }

        /// <summary>
        /// 对应文件的完整路径（如有）/ Full file path if the data was loaded from disk.
        /// </summary>
        public string FullPathFileName { get; set; }

        /// <summary>
        /// K 线集合（去重后的 HashSet）/ De-duplicated set of OHLCV candles.
        /// </summary>
        public HashSet<Ohlcv> OhlcvSet { get; set; } = new HashSet<Ohlcv>();
    }
}
