namespace Quant.Infra.Net.Analysis.Service
{
    public interface IAnalysisService
    {
        double CalculateCorrelation(double[] seriesA, double[] seriesB);
        bool PerformADFTest(double[] timeSeries, double threshold = 0.05);
        (double Slope, double Intercept) PerformLinearRegression(double[] seriesA, double[] seriesB);
    }
}
