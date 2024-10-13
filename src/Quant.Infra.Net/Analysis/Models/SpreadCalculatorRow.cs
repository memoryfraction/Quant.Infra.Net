using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Analysis.Models
{
    public class SpreadCalculatorRow
    {
        public double Slope { get; set; }
        public double Intercept { get; set; }
        public double Spread { get; set; }
        public string Equation { get; set; }
    }
}
