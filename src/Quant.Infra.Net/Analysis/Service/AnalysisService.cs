using Accord.Statistics.Testing;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearRegression;
using MathNet.Numerics.Statistics;
using Python.Runtime;
using Quant.Infra.Net.Analysis.Models;
using Quant.Infra.Net.Shared.Model;
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

        /// <summary>
        /// todo: 这个方法不准确，还是需要通过Python statsmodels.tsa.stattools 做底层实现;
        /// </summary>
        /// <param name="timeSeries"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public AdfTestResult AugmentedDickeyFullerTest(IEnumerable<double> timeSeries)
        {
            int lag = 1;
            var series = timeSeries.ToArray();
            int n = series.Length;
            if (n <= lag + 1)
                throw new ArgumentException("Time series too short for given lag.");

            // Δy_t
            double[] diff = new double[n - 1];
            for (int i = 1; i < n; i++)
                diff[i - 1] = series[i] - series[i - 1];

            // 构造 y_{t-1}
            double[] yLag1 = new double[n - 1];
            for (int i = 1; i < n; i++)
                yLag1[i - 1] = series[i - 1];

            // 构造回归矩阵 X
            // 第一列: y_{t-1}
            // 后面列: Δy_{t-1}, Δy_{t-2}, ... (根据 lag 决定)
            // 最后一列: 常数项
            int rows = diff.Length - lag;
            int cols = 1 + lag + 1; // yLag1 + Δy lags + const
            var X = Matrix<double>.Build.Dense(rows, cols);
            var Y = Vector<double>.Build.Dense(rows);

            for (int t = lag; t < diff.Length; t++)
            {
                int row = t - lag;
                X[row, 0] = yLag1[t]; // y_{t-1}

                for (int j = 1; j <= lag; j++)
                    X[row, j] = diff[t - j]; // Δy_{t-j}

                X[row, cols - 1] = 1.0; // 常数项
                Y[row] = diff[t];
            }

            // OLS 回归
            var Xt = X.Transpose();
            var XtX = Xt * X;
            var XtY = Xt * Y;
            var betaVec = XtX.Solve(XtY);

            double beta = betaVec[0]; // y_{t-1} 的系数

            // 残差
            var residuals = Y - X * betaVec;
            double s2 = residuals.DotProduct(residuals) / (rows - cols);

            // 协方差矩阵
            var covMatrix = XtX.Inverse() * s2;
            double seBeta = Math.Sqrt(covMatrix[0, 0]);

            // ADF 统计量
            double adfStatistic = beta / seBeta;

            // 近似 p-value（建议用 MacKinnon 表或外部插值）
            double pValue = AdfPValue.ApproximateAdfPValue(adfStatistic);

            return new AdfTestResult
            {
                Statistic = adfStatistic,
                PValue = pValue
            };
        }

        private static bool pythonInitialized = false;
        private static object pythonInitLock = new object();
        public AdfTestResult AugmentedDickeyFullerTestPython(IEnumerable<double> timeSeries, string condaVenvHomePath = @"D:\ProgramData\PythonVirtualEnvs\pair_trading", string pythonDllFullPathFileName = "python39.dll")
        {
            // 初始化 Python 环境
            // 初始化 Python 环境（只执行一次）
            if (!pythonInitialized)
            {
                lock (pythonInitLock)
                {
                    if (!pythonInitialized)
                    {
                        var infra = PythonNetInfra.GetPythonInfra(condaVenvHomePath, pythonDllFullPathFileName);

                        Runtime.PythonDLL = infra.PythonDLL;
                        PythonEngine.PythonHome = infra.PythonHome;
                        PythonEngine.PythonPath = infra.PythonPath;

                        PythonEngine.Initialize();
                        pythonInitialized = true;
                    }
                }
            }

            var resultObj = new AdfTestResult();

            using (Py.GIL())
            {
                dynamic np = Py.Import("numpy");
                dynamic sm = Py.Import("statsmodels.tsa.stattools");

                // 转换 C# 数组为 numpy 数组GetSp500SymbolsAsync
                dynamic npArray = np.array(timeSeries.ToArray());

                // 调用 adfuller
                dynamic result = sm.adfuller(npArray, regression: "c", autolag: "AIC");      // AIC准则选择滞后);
                double adfStat = (double)result[0];
                double pValue = (double)result[1];

                resultObj.Statistic = adfStat;
                resultObj.PValue = pValue;
            }

            return resultObj;
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