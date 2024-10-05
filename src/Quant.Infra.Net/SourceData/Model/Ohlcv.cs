using System;

namespace Quant.Infra.Net.SourceData.Model
{
    public class BasicOhlcv
    {
        public string Symbol { get; set; }
        public DateTime OpenDateTime { get; set; }
        public DateTime CloseDateTime { get; set; }

        public decimal Open { get; set; }

        public decimal High { get; set; }

        public decimal Low { get; set; }

        public decimal Close { get; set; }

        public decimal Volume { get; set; }

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

    public class Ohlcv : BasicOhlcv
    {
        public decimal AdjustedClose { get; set; }

        // Override Equals method
        public override bool Equals(object obj)
        {
            // If the passed object is null or not of type Ohlcv, return false
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            // Cast the object to Ohlcv
            Ohlcv other = (Ohlcv)obj;

            // Check if all relevant properties are equal
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

        // Override GetHashCode method
        public override int GetHashCode()
        {
            // Use two HashCode.Combine calls since there are more than 8 properties
            int hash = HashCode.Combine(Symbol, OpenDateTime, CloseDateTime, Open, High, Low, Close, Volume);
            return HashCode.Combine(hash, AdjustedClose); // Combine the hash of previous values with AdjustedClose
        }
    }
}