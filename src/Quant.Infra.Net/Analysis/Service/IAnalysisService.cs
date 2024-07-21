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

        /// <summary>
        /// 线性回归
        /// </summary>
        /// <param name="seriesA"></param>
        /// <param name="seriesB"></param>
        /// <returns>diff = B - Slope * A - Intercept</returns>
        (double Slope, double Intercept) PerformLinearRegression(double[] seriesA, double[] seriesB);


        bool PerformShapiroWilkTest(double[] timeSeries, double threshold = 0.05);

        double[] CalculateZScores(double[] data);

    }
}
