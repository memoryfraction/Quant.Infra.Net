using Accord.Statistics.Testing;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearRegression;
using MathNet.Numerics.Statistics;
using Quant.Infra.Net.Analysis.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Quant.Infra.Net.Analysis.Service
{
    public class AnalysisService : IAnalysisService
    {
        /// <summary>
        /// 计算两个时间序列的相关性。
        /// </summary>
        /// <param name="seriesA">时间序列A</param>
        /// <param name="seriesB">时间序列B</param>
        /// <returns>相关性系数</returns>
        public double CalculateCorrelation(IEnumerable<double> seriesA, IEnumerable<double> seriesB)
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
        public bool AugmentedDickeyFullerTest(IEnumerable<double> timeSeries, double adfTestStatisticThreshold = -2.86)
        {
            // Ensure the input time series is not null or empty
            if (timeSeries == null || timeSeries.Count() == 0)
            {
                throw new ArgumentException("The time series data must not be null or empty.");
            }

            // Calculate the ADF statistic
            var adfTestResult = AugmentedDickeyFullerTest(timeSeries);

            // Compare the ADF statistic with the threshold
            // Note: You might need to adjust this part based on the critical values for ADF test
            return adfTestResult.Statistic < adfTestStatisticThreshold;
        }

        public AdfTestResult AugmentedDickeyFullerTest(IEnumerable<double> timeSeries)
        {
            // Ensure the input time series is not null or empty
            if (timeSeries == null || timeSeries.Count() == 0)
            {
                throw new ArgumentException("The time series data must not be null or empty.");
            }

            // Calculate the first difference of the time series
            double[] diffSeries = new double[timeSeries.Count() - 1];
            for (int i = 1; i < timeSeries.Count(); i++)
            {
                diffSeries[i - 1] = timeSeries.ElementAt(i) - timeSeries.ElementAt(i - 1);
            }

            // Build the regression matrix
            var matrixBuilder = Matrix<double>.Build;
            var vectorBuilder = Vector<double>.Build;

            var y = vectorBuilder.DenseOfArray(diffSeries);
            var x = matrixBuilder.Dense(diffSeries.Length, 2, (i, j) => j == 0 ? timeSeries.ElementAt(i) : 1.0);

            // Perform linear regression
            var regression = x.QR().Solve(y);

            // Get the regression coefficients
            double beta = regression[0];
            double intercept = regression[1];

            // Calculate the ADF statistic
            double adfStatistic = beta / (y.StandardDeviation() / Math.Sqrt(diffSeries.Length));

            // 参考: MacKinnon (1994) 近似，注意这只是粗略估计
            var pValue = ApproximateAdfPValue(adfStatistic);

            // Note: You might need to adjust this part based on the critical values for ADF test
            return new AdfTestResult() 
            { 
                PValue = pValue,
                Statistic = adfStatistic
            };
        }

        /// <summary>
        /// 只是模糊准确，e.g. [1,11], 
        /// python tatsmodels.tsa.stattools pvalue: 0.99
        /// CSharp pvalue  0.9
        /// </summary>
        /// <param name="adfStat"></param>
        /// <returns></returns>
        private double ApproximateAdfPValue(double adfStat)
        {
            // 这是一个近似函数示例，仅供参考
            // ADF统计量通常为负，数值越小越显著，p-value越小
            // 简单做线性映射并限制范围
            if (adfStat > -1.0) return 0.9;   // 非平稳可能性大
            if (adfStat < -4.0) return 0.01;  // 非常显著平稳

            // 线性内插
            double slope = (0.01 - 0.9) / (-4.0 + 1.0); // (p2 - p1)/(x2 - x1)
            double intercept = 0.9 - slope * (-1.0);

            double p = slope * adfStat + intercept;
            return Math.Min(Math.Max(p, 0.0), 1.0);
        }

        /// <summary>
        /// 进行最小二乘法(Ordinary Least Squares Regression)回归分析并返回斜率和截距。
        /// </summary>
        /// <param name="seriesA">时间序列A</param>
        /// <param name="seriesB">时间序列B</param>
        /// <returns>斜率和截距</returns>
        public (double Slope, double Intercept) PerformOLSRegression(IEnumerable<double> seriesA, IEnumerable<double> seriesB)
        {
            var regression = SimpleRegression.Fit(seriesA.ToArray(), seriesB.ToArray());
            return (Math.Round(regression.Item2, 6), Math.Round(regression.Item1, 6)); // Slope, Intercept
        }

        /// <summary>
        /// 正态分布检验
        /// </summary>
        /// <param name="timeSeries"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public bool PerformShapiroWilkTest(IEnumerable<double> timeSeries, double threshold = 0.05)
        {
            // 创建Shapiro-Wilk检验对象
            var swTest = new ShapiroWilkTest(timeSeries.ToArray());

            // 获取统计值和p值
            double W = swTest.Statistic;
            double pValue = swTest.PValue;

            // 输出结果
            Console.WriteLine($"Shapiro-Wilk W: {W}");
            Console.WriteLine($"p-value: {pValue}");

            // 根据阈值判断样本是否符合正态分布
            if (pValue > threshold)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static decimal CalculateZScores(IEnumerable<decimal> data, decimal value)
        {
            var doubleData = data.Select(x => (double)x).ToList();
            var doubleValue = (double)value;

            var mean = doubleData.Average();
            var stdDev = Math.Sqrt(doubleData.Average(x => Math.Pow(x - mean, 2)));
            return Convert.ToDecimal((doubleValue - mean) / stdDev);
        }

        public double CalculateZScores(IEnumerable<double> data, double value)
        {
            double mean = data.Average();
            double stdDev = Math.Sqrt(data.Average(x => Math.Pow(x - mean, 2)));
            return (value - mean) / stdDev;
        }


        public double CalculateZScores(double mean, double stdDev, double value)
        {
            return (value - mean) / stdDev;
        }


        
    }
}