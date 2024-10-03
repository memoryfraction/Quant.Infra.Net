using System;
using System.Collections.Generic;
using System.Linq;

namespace Quant.Infra.Net.Shared.Model
{
    public class TimeSeriesElement
    {
        public TimeSeriesElement()
        { }

        public TimeSeriesElement(DateTime dt, double value)
        {
            DateTime = dt;
            Value = value;
        }

        public DateTime DateTime { get; set; }
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

    public class TimeSeries
    {
        public TimeSeries()
        {
            TimeSeriesElements = new List<TimeSeriesElement>();
        }

        public List<TimeSeriesElement> TimeSeriesElements { get; set; } = new List<TimeSeriesElement>();

        public TimeSeries(IEnumerable<DateTime> dtList, IEnumerable<double> values)
        {
            if (dtList.Count() != values.Count())
                throw new ArgumentException("dtList length should be the same with values length");

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