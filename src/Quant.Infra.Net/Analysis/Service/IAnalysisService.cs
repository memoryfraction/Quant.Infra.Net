namespace Quant.Infra.Net.Analysis.Service
{
    public interface IAnalysisService
    {
        double CalculateCorrelation(double[] seriesA, double[] seriesB);

        /// <summary>
        /// Stationary Test
        /// </summary>
        /// <param name="timeSeries"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        bool AugmentedDickeyFullerTest(double[] timeSeries, double threshold = 0.05);


        (double Slope, double Intercept) PerformLinearRegression(double[] seriesA, double[] seriesB);


        bool PerformShapiroWilkTest(double[] timeSeries, double threshold = 0.05);

        double[] CalculateZScores(double[] data);

    }
}
