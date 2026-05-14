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
    /// <summary>
    /// 分析服务实现，提供相关性计算、ADF 检验、OLS 回归、Z-Score 等分析功能。
    /// Analysis service implementation providing correlation calculation, ADF test, OLS regression, Z-Score, and other analysis features.
    /// </summary>
    public class AnalysisService : IAnalysisService
    {
        /// <summary>
        /// 计算两个时间序列的 Pearson 相关性。
        /// Calculates the Pearson correlation between two time series.
        /// </summary>
        /// <param name="seriesA">时间序列A / Time series A.</param>
        /// <param name="seriesB">时间序列B / Time series B.</param>
        /// <returns>相关性系数 / The correlation coefficient.</returns>
        /// <exception cref="ArgumentNullException">当参数为 null 时抛出 / Thrown when parameters are null.</exception>
        public double CalculateCorrelation(IEnumerable<double> seriesA, IEnumerable<double> seriesB)
        {
            if (seriesA == null) throw new ArgumentNullException(nameof(seriesA));
            if (seriesB == null) throw new ArgumentNullException(nameof(seriesB));
            if (!seriesA.Any() || !seriesB.Any()) throw new ArgumentException("Input series must not be empty.");

            return Correlation.Pearson(seriesA, seriesB);
        }

        /// <summary>
        /// 进行 ADF 检验来测试时间序列的平稳性。
        /// Performs ADF test to check the stationarity of a time series.
        /// </summary>
        /// <param name="timeSeries">时间序列数据 / The time series data.</param>
        /// <param name="adfTestStatisticThreshold">显著性水平阈值 / Significance level threshold.</param>
        /// <returns>是否拒绝原假设（时间序列是否平稳） / Whether to reject the null hypothesis (whether the time series is stationary).</returns>
        /// <exception cref="ArgumentException">当时间序列为 null 或为空时抛出 / Thrown when the time series is null or empty.</exception>
        public bool AugmentedDickeyFullerTest(IEnumerable<double> timeSeries, double adfTestStatisticThreshold = -2.86)
        {
            if (timeSeries == null || !timeSeries.Any())
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
        /// 进行 ADF 检验，返回统计量和 P 值（C# 实现，为近似值）。
        /// Performs ADF test and returns the statistic and P-value (C# implementation, approximate values).
        /// </summary>
        /// <param name="timeSeries">时间序列数据 / The time series data.</param>
        /// <returns>ADF 检验结果 / The ADF test result.</returns>
        /// <exception cref="ArgumentException">当时间序列太短时抛出 / Thrown when the time series is too short.</exception>
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
        /// 进行最小二乘法（OLS）回归分析并返回斜率和截距。
        /// Performs OLS (Ordinary Least Squares) regression and returns the slope and intercept.
        /// </summary>
        /// <param name="seriesA">时间序列A / Time series A.</param>
        /// <param name="seriesB">时间序列B / Time series B.</param>
        /// <returns>斜率和截距的元组 / A tuple of slope and intercept.</returns>
        /// <exception cref="ArgumentNullException">当参数为 null 时抛出 / Thrown when parameters are null.</exception>
        public (double Slope, double Intercept) PerformOLSRegression(IEnumerable<double> seriesA, IEnumerable<double> seriesB)
        {
            if (seriesA == null) throw new ArgumentNullException(nameof(seriesA));
            if (seriesB == null) throw new ArgumentNullException(nameof(seriesB));
            var arrA = seriesA.ToArray();
            var arrB = seriesB.ToArray();
            if (arrA.Length == 0 || arrB.Length == 0) throw new ArgumentException("Input series must not be empty.");
            if (arrA.Length != arrB.Length) throw new ArgumentException("Input series must have the same length.");

            var regression = SimpleRegression.Fit(arrA, arrB);
            return (Math.Round(regression.Item2, 6), Math.Round(regression.Item1, 6)); // Slope, Intercept
        }

        /// <summary>
        /// 正态分布检验（Shapiro-Wilk 检验）。
        /// Normal distribution test (Shapiro-Wilk test).
        /// </summary>
        /// <param name="timeSeries">时间序列数据 / The time series data.</param>
        /// <param name="threshold">显著性水平阈值 / Significance level threshold.</param>
        /// <returns>是否符合正态分布 / Whether the sample conforms to a normal distribution.</returns>
        /// <exception cref="ArgumentNullException">当时间序列为 null 时抛出 / Thrown when the time series is null.</exception>
        public bool PerformShapiroWilkTest(IEnumerable<double> timeSeries, double threshold = 0.05)
        {
            if (timeSeries == null) throw new ArgumentNullException(nameof(timeSeries));
            var arr = timeSeries.ToArray();
            if (arr.Length == 0) throw new ArgumentException("Time series must not be empty.", nameof(timeSeries));

            // 创建Shapiro-Wilk检验对象
            var swTest = new ShapiroWilkTest(arr);

            double W = swTest.Statistic;
            double pValue = swTest.PValue;

            Console.WriteLine($"Shapiro-Wilk W: {W}");
            Console.WriteLine($"p-value: {pValue}");

            return pValue > threshold;
        }

        /// <summary>
        /// 计算 Z-Score（decimal 版本）。
        /// Calculates the Z-Score (decimal version).
        /// </summary>
        /// <param name="data">数据集合 / The data collection.</param>
        /// <param name="value">要计算 Z-Score 的值 / The value to calculate the Z-Score for.</param>
        /// <returns>Z-Score 值 / The Z-Score value.</returns>
        /// <exception cref="ArgumentNullException">当 data 为 null 时抛出 / Thrown when data is null.</exception>
        public static decimal CalculateZScores(IEnumerable<decimal> data, decimal value)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var doubleData = data.Select(x => (double)x).ToList();
            if (!doubleData.Any()) throw new ArgumentException("Data must not be empty.", nameof(data));
            var doubleValue = (double)value;

            var mean = doubleData.Average();
            var stdDev = Math.Sqrt(doubleData.Average(x => Math.Pow(x - mean, 2)));
            if (stdDev == 0) throw new InvalidOperationException("Standard deviation is zero; cannot compute z-score.");
            return Convert.ToDecimal((doubleValue - mean) / stdDev);
        }

        /// <summary>
        /// 计算 Z-Score（double 版本）。
        /// Calculates the Z-Score (double version).
        /// </summary>
        /// <param name="data">数据集合 / The data collection.</param>
        /// <param name="value">要计算 Z-Score 的值 / The value to calculate the Z-Score for.</param>
        /// <returns>Z-Score 值 / The Z-Score value.</returns>
        /// <exception cref="ArgumentNullException">当 data 为 null 时抛出 / Thrown when data is null.</exception>
        public double CalculateZScores(IEnumerable<double> data, double value)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var arr = data.ToArray();
            if (arr.Length == 0) throw new ArgumentException("Data must not be empty.", nameof(data));

            double mean = arr.Average();
            double stdDev = Math.Sqrt(arr.Average(x => Math.Pow(x - mean, 2)));
            if (stdDev == 0) throw new InvalidOperationException("Standard deviation is zero; cannot compute z-score.");
            return (value - mean) / stdDev;
        }

        /// <summary>
        /// 根据已知的均值和标准差计算 Z-Score。
        /// Calculates the Z-Score from the given mean and standard deviation.
        /// </summary>
        /// <param name="mean">均值 / The mean.</param>
        /// <param name="stdDev">标准差 / The standard deviation.</param>
        /// <param name="value">要计算 Z-Score 的值 / The value to calculate the Z-Score for.</param>
        /// <returns>Z-Score 值 / The Z-Score value.</returns>
        public double CalculateZScores(double mean, double stdDev, double value)
        {
            return (value - mean) / stdDev;
        }


        
    }
}