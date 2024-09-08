using System;

namespace Quant.Infra.Net.Portfolio.Models
{
    public class PortfolioSnapshot
    {
        public DateTime DateTime { get; set; }
        public Balance Balance { get; set; } = new Balance();
        public Positions Positions { get; set; } = new Positions();
    }

}
