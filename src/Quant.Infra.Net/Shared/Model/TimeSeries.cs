using System;
using System.Collections.Generic;
using System.Linq;

namespace Quant.Infra.Net.Shared.Model
{
    /// <summary>
    /// 时间序列元素，包含日期时间和值。
    /// A time series element containing a DateTime and a value.
    /// </summary>
    public class TimeSeriesElement
    {
        /// <summary>
        /// 默认构造函数。
        /// Default constructor.
        /// </summary>
        public TimeSeriesElement()
        { }

        /// <summary>
        /// 使用指定的日期时间和值初始化时间序列元素。
        /// Initializes a time series element with the specified DateTime and value.
        /// </summary>
        /// <param name="dt">日期时间 / The DateTime.</param>
        /// <param name="value">值 / The value.</param>
        public TimeSeriesElement(DateTime dt, double value)
        {
            DateTime = dt;
            Value = value;
        }

        /// <summary>
        /// 日期时间 / The DateTime.
        /// </summary>
        public DateTime DateTime { get; set; }
        /// <summary>
        /// 值 / The value.
        /// </summary>
        public double Value { get; set; }

        // Override ToString() method
        public override string ToString()
        {
            return $"DateTime: {DateTime}, Value: {Value}";
        }

        // Override GetHashCode() method
        public override int GetHashCode()
        {
            unchecked // Allow overflow, so it wraps around
            {
                int hash = 17;
                hash = hash * 23 + DateTime.GetHashCode();
                hash = hash * 23 + Value.GetHashCode();
                return hash;
            }
        }

        // Override Equals() method
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            TimeSeriesElement other = (TimeSeriesElement)obj;
            return DateTime == other.DateTime && Value == other.Value;
        }
    }

    /// <summary>
    /// 时间序列集合，包含一组时间序列元素。
    /// A time series collection containing a list of time series elements.
    /// </summary>
    public class TimeSeries
    {
        /// <summary>
        /// 默认构造函数。
        /// Default constructor.
        /// </summary>
        public TimeSeries()
        {
            TimeSeriesElements = new List<TimeSeriesElement>();
        }

        /// <summary>
        /// 时间序列元素列表。
        /// The list of time series elements.
        /// </summary>
        public List<TimeSeriesElement> TimeSeriesElements { get; set; } = new List<TimeSeriesElement>();

        /// <summary>
        /// 使用日期列表和值列表初始化时间序列。
        /// Initializes a time series with the specified date list and value list.
        /// </summary>
        /// <param name="dtList">日期时间列表 / The list of DateTimes.</param>
        /// <param name="values">值列表 / The list of values.</param>
        /// <exception cref="ArgumentNullException">当参数为 null 时抛出 / Thrown when parameters are null.</exception>
        /// <exception cref="ArgumentException">当列表长度不一致时抛出 / Thrown when list lengths do not match.</exception>
        public TimeSeries(IEnumerable<DateTime> dtList, IEnumerable<double> values)
        {
            if (dtList == null)
                throw new ArgumentNullException(nameof(dtList));
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (dtList.Count() != values.Count())
                throw new ArgumentException("dtList length must be the same as values length.");

            TimeSeriesElements = new List<TimeSeriesElement>();
            for (int i = 0; i < dtList.Count(); i++)
            {
                var elm = new TimeSeriesElement();
                elm.DateTime = dtList.ElementAt(i);
                elm.Value = values.ElementAt(i);
                TimeSeriesElements.Add(elm);
            }
        }

        // Override ToString() method
        public override string ToString()
        {
            return $"TimeSeries with {TimeSeriesElements.Count} elements: [{string.Join(", ", TimeSeriesElements)}]";
        }

        // Override GetHashCode() method
        public override int GetHashCode()
        {
            unchecked // Allow overflow, so it wraps around
            {
                int hash = 17;
                foreach (var element in TimeSeriesElements)
                {
                    hash = hash * 23 + (element?.GetHashCode() ?? 0);
                }
                return hash;
            }
        }

        // Override Equals() method
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            TimeSeries other = (TimeSeries)obj;

            if (TimeSeriesElements.Count != other.TimeSeriesElements.Count)
            {
                return false;
            }

            for (int i = 0; i < TimeSeriesElements.Count; i++)
            {
                if (!TimeSeriesElements[i].Equals(other.TimeSeriesElements[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}