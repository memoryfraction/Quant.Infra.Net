using CsvHelper.Configuration.Attributes;
using System;

namespace Quant.Infra.Net.SourceData.Model
{
    /// <summary>
    /// 基础 OHLCV 蜡烛图数据模型（开盘、最高、最低、收盘、成交量）。
    /// Basic OHLCV candlestick model with open, high, low, close, and volume.
    /// </summary>
    public class BasicOhlcv
    {
        /// <summary>
        /// 交易品种代码（如 AAPL, BTCUSDT）/ Trading symbol (e.g., AAPL, BTCUSDT).
        /// </summary>
        [Ignore]
        public string Symbol { get; set; }

        /// <summary>
        /// K 线开盘时间 / Candle open datetime.
        /// </summary>
        [Name("OpenDateTime")]
        [Index(0)]
        public DateTime OpenDateTime { get; set; }

        /// <summary>
        /// K 线收盘时间 / Candle close datetime.
        /// </summary>
        [Name("CloseDateTime")]
        [Index(1)]
        public DateTime CloseDateTime { get; set; }

        /// <summary>
        /// 开盘价 / Opening price during the period.
        /// </summary>
        [Name("Open")]
        [Index(2)]
        public decimal Open { get; set; }

        /// <summary>
        /// 最高价 / Highest price during the period.
        /// </summary>
        [Name("High")]
        [Index(3)]
        public decimal High { get; set; }

        /// <summary>
        /// 最低价 / Lowest price during the period.
        /// </summary>
        [Name("Low")]
        [Index(4)]
        public decimal Low { get; set; }

        /// <summary>
        /// 收盘价 / Closing price at end of the period.
        /// </summary>
        [Name("Close")]
        [Index(5)]
        public decimal Close { get; set; }

        /// <summary>
        /// 成交量 / Trading volume during the period.
        /// </summary>
        [Name("Volume")]
        [Index(6)]
        public decimal Volume { get; set; }

        /// <summary>
        /// 检查该 K 线数据是否有效（所有字段非默认值）。
        /// Check whether all fields are populated (non-default values).
        /// </summary>
        /// <returns>数据完整时返回 true / Returns true if all required fields have valid values.</returns>
        public bool IsValid()
        {
            return OpenDateTime != default(DateTime) &&
             CloseDateTime != default(DateTime) &&
             Open != default(decimal) &&
             High != default(decimal) &&
             Low != default(decimal) &&
             Close != default(decimal) &&
             Volume != default(decimal);
        }
    }

    /// <summary>
    /// 扩展 OHLCV 模型，在 BasicOhlcv 基础上增加复权收盘价。
    /// Extended OHLCV model with adjusted close price.
    /// </summary>
    public class Ohlcv : BasicOhlcv
    {
        /// <summary>
        /// 复权收盘价（考虑分红拆股）/ Adjusted close price accounting for dividends and splits.
        /// </summary>
        [Ignore]
        public decimal AdjustedClose { get; set; }

        /// <summary>
        /// 比较两个 OHLCV 对象是否相等。
        /// Compare two Ohlcv objects for equality.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            Ohlcv other = (Ohlcv)obj;

            return Symbol == other.Symbol &&
                   OpenDateTime == other.OpenDateTime &&
                   CloseDateTime == other.CloseDateTime &&
                   Open == other.Open &&
                   High == other.High &&
                   Low == other.Low &&
                   Close == other.Close &&
                   Volume == other.Volume &&
                   AdjustedClose == other.AdjustedClose;
        }

        /// <summary>
        /// 返回该 OHLCV 对象的哈希码。
        /// Return the hash code for this Ohlcv object.
        /// </summary>
        public override int GetHashCode()
        {
            int hash = HashCode.Combine(Symbol, OpenDateTime, CloseDateTime, Open, High, Low, Close, Volume);
            return HashCode.Combine(hash, AdjustedClose);
        }
    }
}
