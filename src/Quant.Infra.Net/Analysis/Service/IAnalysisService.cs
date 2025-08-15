using Quant.Infra.Net.Analysis.Models;
using System.Collections.Generic;

namespace Quant.Infra.Net.Analysis.Service
{
    public interface IAnalysisService
    {
        double CalculateCorrelation(IEnumerable<double> seriesA, IEnumerable<double> seriesB);

        /// <summary>
        /// Stationary Test
        /// </summary>
        /// <param name="timeSeries"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        bool AugmentedDickeyFullerTest(IEnumerable<double> timeSeries, double adfTestStatisticThreshold = -2.86);

        AdfTestResult AugmentedDickeyFullerTestPython(
            IEnumerable<double> timeSeries, 
            string condaVenvHomePath = @"D:\ProgramData\PythonVirtualEnvs\pair_trading", 
            string pythonDllFullPathFileName = "python39.dll");

        public AdfTestResult AugmentedDickeyFullerTest(IEnumerable<double> timeSeries);

        /// <summary>
        /// 线性回归
        /// </summary>
        /// <param name="seriesA"></param>
        /// <param name="seriesB"></param>
        /// <returns>diff = B - Slope * A - Intercept</returns>
        (double Slope, double Intercept) PerformOLSRegression(IEnumerable<double> seriesA, IEnumerable<double> seriesB);

        bool PerformShapiroWilkTest(IEnumerable<double> timeSeries, double threshold = 0.05);

        double CalculateZScores(IEnumerable<double> data, double value);

        double CalculateZScores(double mean, double stdDev, double value);

        
    }
}