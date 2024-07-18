using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearRegression;
using MathNet.Numerics.Statistics;
using System;

namespace Quant.Infra.Net.Analysis.Service
{
    public class AnalysisService: IAnalysisService
    {
        /// <summary>
        /// 计算两个时间序列的相关性。
        /// </summary>
        /// <param name="seriesA">时间序列A</param>
        /// <param name="seriesB">时间序列B</param>
        /// <returns>相关性系数</returns>
        public double CalculateCorrelation(double[] seriesA, double[] seriesB)
        {
            return Correlation.Pearson(seriesA, seriesB);
        }


        /// <summary>
        /// 进行ADF检验来测试时间序列的平稳性。
        /// </summary>
        /// <param name="series">时间序列</param>
        /// <param name="threshold">显著性水平阈值（例如：0.01, 0.05, 0.1）</param>
        /// <returns>是否拒绝原假设（即时间序列是否平稳）</returns>
        /// <remarks>
        /// 阈值分类：
        /// 0.01 - 非常显著
        /// 0.05 - 显著
        /// 0.1  - 较显著
        /// </remarks>
        public bool PerformADFTest(double[] timeSeries, double threshold = 0.05)
        {
            // Ensure the input time series is not null or empty
            if (timeSeries == null || timeSeries.Length == 0)
            {
                throw new ArgumentException("The time series data must not be null or empty.");
            }

            // Calculate the first difference of the time series
            double[] diffSeries = new double[timeSeries.Length - 1];
            for (int i = 1; i < timeSeries.Length; i++)
            {
                diffSeries[i - 1] = timeSeries[i] - timeSeries[i - 1];
            }

            // Build the regression matrix
            var matrixBuilder = Matrix<double>.Build;
            var vectorBuilder = Vector<double>.Build;

            var y = vectorBuilder.DenseOfArray(diffSeries);
            var x = matrixBuilder.Dense(diffSeries.Length, 2, (i, j) => j == 0 ? timeSeries[i] : 1.0);

            // Perform linear regression
            var regression = x.QR().Solve(y);

            // Get the regression coefficients
            double beta = regression[0];
            double intercept = regression[1];

            // Calculate the ADF statistic
            double adfStatistic = beta / (y.StandardDeviation() / Math.Sqrt(diffSeries.Length));

            // Compare the ADF statistic with the threshold
            // Note: You might need to adjust this part based on the critical values for ADF test
            return adfStatistic < threshold;
        }


        /// <summary>
        /// 进行线性回归分析并返回斜率和截距。
        /// </summary>
        /// <param name="seriesA">时间序列A</param>
        /// <param name="seriesB">时间序列B</param>
        /// <returns>斜率和截距</returns>
        public (double Slope, double Intercept) PerformLinearRegression(double[] seriesA, double[] seriesB)
        {
            var regression = SimpleRegression.Fit(seriesA, seriesB);
            return (regression.Item2, regression.Item1); // Slope, Intercept
        }
    }
}
