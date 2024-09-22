using System.Collections.Generic;

namespace Quant.Infra.Net.Shared.Model
{
    public class OLSRegressionData
    {
        public List<double> SeriesA { get; set; } = new List<double>();
        public List<double> SeriesB { get; set; } = new List<double>();
    }
}