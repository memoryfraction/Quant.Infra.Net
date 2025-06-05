using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Analysis.Models
{
    public class Element
    {
        public Element(string symbol, DateTime dt, double value) 
        { 
            Symbol = symbol;
            DateTime = dt;
            Value = value;
        }
        public string Symbol { get; set; }
        public DateTime DateTime { get; set; }
        public double Value { get; set; }
    }
}
