using System;
using System.Collections.Generic;

namespace Quant.Infra.Net.Shared.Model
{
    public class TimeSeriesElement
    {
        public TimeSeriesElement() { }

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
        public TimeSeries() { }
        public List<TimeSeriesElement> Series { get; set; } = new List<TimeSeriesElement>();

        // Override ToString() method
        public override string ToString()
        {
            return $"TimeSeries with {Series.Count} elements: [{string.Join(", ", Series)}]";
        }

        // Override GetHashCode() method
        public override int GetHashCode()
        {
            unchecked // Allow overflow, so it wraps around
            {
                int hash = 17;
                foreach (var element in Series)
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

            if (Series.Count != other.Series.Count)
            {
                return false;
            }

            for (int i = 0; i < Series.Count; i++)
            {
                if (!Series[i].Equals(other.Series[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }

}
