using CsvHelper;
using CsvHelper.Configuration;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Python.Runtime;
using Quant.Infra.Net.Analysis.Models;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service;
using ScottPlot;
using System.Diagnostics;
using System.Globalization;

namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class AnalysisTests
    {
        private readonly ServiceProvider _serviceProvider;
        public AnalysisTests()
        {
            ServiceCollection _serviceCollection = new ServiceCollection();
            _serviceCollection.AddScoped<ITraditionalFinanceSourceDataService, TraditionalFinanceSourceDataService>();
            _serviceCollection.AddScoped<IAnalysisService, AnalysisService>();
            _serviceCollection.AddScoped<UtilityService>();
            _serviceProvider = _serviceCollection.BuildServiceProvider();
        }

        [TestMethod]
        public void CorrelationTest_Should_Work()
        {
            // Arrange
            double[] seriesA = { 1, 2, 3, 4, 5 };
            double[] seriesB = { 2, 4, 6, 8, 10 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            double correlation = _analysisService.CalculateCorrelation(seriesA, seriesB);

            // Assert
            Assert.AreEqual(1.0, correlation);
        }

        [TestMethod]
        public void StationaryTest_Should_Work()
        {
            // Arrange
            double[] stationarySeries = { 0.5, 0.6, 0.4, 0.7, 0.5, 0.3, 0.6, 0.4 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            bool isStationary = _analysisService.AugmentedDickeyFullerTest(stationarySeries, adfTestStatisticThreshold:-2.846);

            var res = _analysisService.AugmentedDickeyFullerTest(stationarySeries);
            Console.WriteLine($"pvalue:{res.PValue}");
            Console.WriteLine($"statistic:{res.Statistic}");

            // Assert
            Assert.AreEqual(true, isStationary);
        }

        [TestMethod]
        public void PythonStationaryTest_Should_Work()
        {
            // Arrange
            double[] stationarySeries = { 0.5, 0.6, 0.4, 0.7, 0.5, 0.3, 0.6, 0.4 };

            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            var evenPath = @"D:\\ProgramData\\PythonVirtualEnvs\\pair_trading";
            var pythonDll = "python39.dll";
            var adfTestRes = _analysisService.AugmentedDickeyFullerTestPython(stationarySeries, evenPath, pythonDll);

            Console.WriteLine($"pvalue:{adfTestRes.PValue}");
            Console.WriteLine($"statistic:{adfTestRes.Statistic}");

            // Assert
            Assert.AreEqual(true, adfTestRes.PValue < 0.05);
        }


        [TestMethod]
        public void NonStationaryTest_Should_Work()
        {
            // Arrange
            double[] nonStationarySeries = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            var res = _analysisService.AugmentedDickeyFullerTest(nonStationarySeries);
            Console.WriteLine($"pvalue:{res.PValue}");
            Console.WriteLine($"statistic:{res.Statistic}");

            // 期待输出：
            // ADF Statistic: 1.0458250331675945
            // p - value: 0.9947266780527716

            bool isStationary = _analysisService.AugmentedDickeyFullerTest(nonStationarySeries, 0.05);

            // Assert
            Assert.AreEqual(false, isStationary);
        }


        [TestMethod]
        public void LinearRegression1_Should_Work()
        {
            // Arrange
            double[] seriesA = { 1, 2, 3, 4, 5 };
            double[] seriesB = { 2, 4, 6, 8, 10 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            var (slope, intercept) = _analysisService.PerformOLSRegression(seriesA, seriesB);
            var diffSeries = new List<double>();
            for (int i = 0; i < seriesA.Count(); i++)
            {
                var diff = seriesB[i] - slope * seriesA[i] - intercept;
                diffSeries.Add(diff);
            }
            Console.WriteLine($"diff Series average: {diffSeries.Average()}");

            // Assert
            // diff = B - 2 * A - 0
            Assert.AreEqual(2.0, slope);
            Assert.AreEqual(0.0, intercept);
            // Console equation
            Console.WriteLine($"diff = B - ({slope} * A) - ({intercept})");

        }

        [TestMethod]
        public void LinearRegression2_Should_Work()
        {
            // Arrange
            double[] seriesA = { 100, 102, 104, 108, 110, 115, 120, 125, 130, 135 };
            double[] seriesB = { 200, 205, 210, 220, 225, 235, 245, 255, 265, 275 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            var (slope, intercept) = _analysisService.PerformOLSRegression(seriesA, seriesB);
            var diffSeries = new List<double>();
            for (int i = 0; i < seriesA.Count(); i++)
            {
                var diff = seriesB[i] - slope * seriesA[i] - intercept;
                diffSeries.Add(diff);
            }
            Console.WriteLine($"diff Series average: {diffSeries.Average()}");

            // Assert
            // diff = B - 2.1237583090596757 * A - (-10.519829710956742)
            Assert.AreEqual(2.1237583090596757, slope);
            Assert.AreEqual(-10.519829710956742, intercept);
            // Console equation
            Console.WriteLine($"diff = B - ({slope} * A) - ({intercept})");
        }

        [TestMethod]
        public void CSharp_LinearRegression3_Should_Work()
        {
            // Arrange
            // 读取ALGOUSDT.csv， 和DASHUSDT.csv作为SeriesA，SeriesB， 执行OLS Regression， 求Diff，计算平均值
            var symbolA = "ALGO";
            var symbolB = "DASH";
            var symbol1FullPathFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", $"{symbolA}USDT.csv");
            var symbol2FullPathFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", $"{symbolB}USDT.csv");
            // 读取ALGOUSDT.csv， 和DASHUSDT.csv作为SeriesA，SeriesB
            var seriesA = UtilityService.ReadCloseColFromCsv(symbol1FullPathFileName);
            var seriesB = UtilityService.ReadCloseColFromCsv(symbol2FullPathFileName);

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            var (slope, intercept) = _analysisService.PerformOLSRegression(seriesA.ToArray(), seriesB.ToArray());
            var diffSeries = new List<double>();
            for (int i = 0; i < seriesA.Count(); i++)
            {
                var diff = seriesB[i] - slope * seriesA[i] - intercept;
                diffSeries.Add(diff);
            }
            Console.WriteLine($"diff Series average: {diffSeries.Average()}");

            // Assert

            // Console equation
            Console.WriteLine($"diff = {symbolB} - ({slope} * {symbolA}) - ({intercept})");
        }


        

        


        [TestMethod]
        public async Task LinearRegressionResultShowChart_Should_Work()
        {
            // Generate simulated stock price data
            // Fixed simulated stock price data for PepsiCo and Coca-Cola
            double[] pricesA = new double[] { 169.89, 170.0, 169.5, 170.2, 169.8, 170.1, 169.9, 170.3, 169.7, 170.4, 169.6, 170.5, 169.8, 170.2, 169.9, 170.1, 169.7, 170.3, 169.8, 170.0, 169.9, 170.2, 169.7, 170.4, 169.6, 170.5, 169.8, 170.2, 169.9, 170.1 };
            double[] pricesB = new double[] { 65.21, 65.25, 65.1, 65.3, 65.2, 65.4, 65.15, 65.35, 65.2, 65.4, 65.1, 65.3, 65.2, 65.35, 65.15, 65.4, 65.2, 65.35, 65.1, 65.3, 65.2, 65.4, 65.15, 65.35, 65.2, 65.4, 65.1, 65.3, 65.2, 65.35 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            var (slope, intercept) = _analysisService.PerformOLSRegression(pricesA, pricesB);

            var diffSeries = new List<double>();
            for (int i = 0; i < pricesA.Count(); i++)
            {
                var diff = pricesB[i] - slope * pricesA[i] - intercept;
                diffSeries.Add(diff);
            }
            Console.WriteLine($"diff Series average: {diffSeries.Average()}");

            // Assert
            Assert.AreEqual(0.31196395040950814, slope);
            Assert.AreEqual(12.221565751700417, intercept);
            Console.WriteLine($"diff = B - ({slope} * A) - ({intercept})");

            // Calculate diff
            List<double> diffList = new List<double>();
            for (int i = 0; i < pricesA.Length; i++)
            {
                var tmpDiff = pricesB[i] - (slope * pricesA[i] + intercept);
                diffList.Add(tmpDiff);
            }
            
            //  生成相同数量的元素到dateList
            List<DateTime> dateList = Enumerable.Range(0, pricesA.Length)
                                    .Select(i => new DateTime(2023, 1, 1).AddDays(i))
                                    .ToList();

            // Plot diff using ScottPlot
            var plt = new Plot();
            plt.Add.Scatter<DateTime,double>(dateList, diffList);
            plt.Axes.DateTimeTicksBottom();
            plt.Title($"diff = B - {slope} * A - {intercept}");
            plt.XLabel("Date");
            plt.YLabel("Difference");
            string fullPathFilename = Path.Combine(AppContext.BaseDirectory, "output", "diff_bar_chart.png");
            await UtilityService.IsPathExistAsync(fullPathFilename);
            plt.SaveJpeg(fullPathFilename, 600, 400);

            // Open the generated image using the default program
            Process.Start(new ProcessStartInfo(fullPathFilename) { UseShellExecute = true });
        }

        /// <summary>
        /// scotplot 5 cookbook: https://scottplot.net/cookbook/5.0/DateTimeAxes/
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task ShowChartAndStdLines_Should_Work()
        {
            // Fixed simulated stock price data for PepsiCo and Coca-Cola
            double[] pricesA = new double[] { 169.89, 170.0, 169.5, 170.2, 169.8, 170.1, 169.9, 170.3, 169.7, 170.4, 169.6, 170.5, 169.8, 170.2, 169.9, 170.1, 169.7, 170.3, 169.8, 170.0, 169.9, 170.2, 169.7, 170.4, 169.6, 170.5, 169.8, 170.2, 169.9, 170.1 };
            double[] pricesB = new double[] { 65.21, 65.25, 65.1, 65.3, 65.2, 65.4, 65.15, 65.35, 65.2, 65.4, 65.1, 65.3, 65.2, 65.35, 65.15, 65.4, 65.2, 65.35, 65.1, 65.3, 65.2, 65.4, 65.15, 65.35, 65.2, 65.4, 65.1, 65.3, 65.2, 65.35 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            var (slope, intercept) = _analysisService.PerformOLSRegression(pricesA, pricesB);

            // Assert
            Assert.AreEqual(0.31196395040950814, slope);
            Assert.AreEqual(12.221565751700417, intercept);
            Console.WriteLine($"diff = B - ({slope} * A) - ({intercept})");

            // Calculate diff
            List<double> diffList = new List<double>();
            for (int i = 0; i < pricesA.Length; i++)
            {
                var tmpDiff = pricesB[i] - (slope * pricesA[i] + intercept);
                diffList.Add(tmpDiff);
            }

            //  生成相同数量的元素到dateList
            List<DateTime> dateList = Enumerable.Range(0, pricesA.Length)
                                    .Select(i => new DateTime(2023, 1, 1).AddDays(i))
                                    .ToList();

            // 根据diffList生成1x, 2x, 3x, -1x,-2x, -3x的6根标准差虚线，也绘制在chart中
            // Calculate standard deviations
            double mean = diffList.Average();
            double stdDev = Math.Sqrt(diffList.Average(v => Math.Pow(v - mean, 2)));
            double[] stdLines = new double[] { mean + stdDev, mean + 2 * stdDev, mean + 3 * stdDev, mean - stdDev, mean - 2 * stdDev, mean - 3 * stdDev };

            // Plot diff using ScottPlot
            var plt = new Plot();
            plt.Add.Scatter<DateTime, double>(dateList, diffList);
            plt.Axes.DateTimeTicksBottom();
            plt.Title($"diff = B - {slope} * A - {intercept}");
            plt.XLabel("Date");
            plt.YLabel("Difference");

            // Add standard deviation lines, 设计6根，只显示了4根。
            foreach (var line in stdLines)
            {
                plt.Add.HorizontalLine(line, pattern:LinePattern.Dashed);
            }

            string fullPathFilename = Path.Combine(AppContext.BaseDirectory, "output", "diff_bar_chart.png");
            await UtilityService.IsPathExistAsync(fullPathFilename);
            plt.SaveJpeg(fullPathFilename, 600, 400);

            // Open the generated image using the default program
            Process.Start(new ProcessStartInfo(fullPathFilename) { UseShellExecute = true });
        }


        [TestMethod]
        public void NormalDistributionTest_Should_Work()
        {
            // Arrange
            double[] normalDistribution = { 10.0, 12.0, 11.0, 13.0, 10.5, 12.5, 11.5, 13.5, 10.2, 12.2, 11.2, 13.2 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            bool isNormal = _analysisService.PerformShapiroWilkTest(normalDistribution, 0.05);

            // Assert
            Assert.AreEqual(true, isNormal);
        }

        [TestMethod]
        public void NonNormalDistributionTest_Should_Work()
        {
            // Arrange
            double[] nonNormalDistribution = { 1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            bool isNormal = _analysisService.PerformShapiroWilkTest(nonNormalDistribution, 0.05);

            // Assert
            Assert.AreEqual(false, isNormal);
        }


        [TestMethod]
        public void TestCalculateZScores_WithValueBelowMean_ReturnsNegativeZScore()
        {
            // Arrange
            var data = new List<double> { 2.0, 4.0, 6.0, 8.0, 10.0 };
            double value = 2.0;
            double mean = data.Average();
            double stdDev = Math.Sqrt(data.Average(x => Math.Pow(x - mean, 2)));
            double expectedZScore = (value - mean) / stdDev;

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            double actualZScore = _analysisService.CalculateZScores(data, value);

            // Assert
            Assert.AreEqual(expectedZScore, actualZScore, 1e-10);
        }

        [TestMethod]
        public void TestCalculateZScores_WithValueAboveMean_ReturnsPositiveZScore()
        {
            // Arrange
            var data = new List<double> { 2.0, 4.0, 6.0, 8.0, 10.0 };
            double value = 10.0;
            double mean = data.Average();
            double stdDev = Math.Sqrt(data.Average(x => Math.Pow(x - mean, 2)));
            double expectedZScore = (value - mean) / stdDev;

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            double actualZScore = _analysisService.CalculateZScores(data, value);

            // Assert
            Assert.AreEqual(expectedZScore, actualZScore, 1e-10);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestCalculateZScores_WithEmptyData_ThrowsInvalidOperationException()
        {
            // Arrange
            var data = new List<double>();
            double value = 10.0;

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            _analysisService.CalculateZScores(data, value);

            // Assert is handled by ExpectedException
        }


        [TestMethod]
        public void CalculateHalfLife_Should_Work()
        {
            // Arrange
            var spreads = new List<Element>();
            var startDate = new DateTime(2025, 1, 1);
            var length = 100; // 模拟100天

            var rand = new Random(42);
            double previous = 0.0;

            for (int i = 0; i < length; i++)
            {
                double noise = rand.NextDouble() * 0.5 - 0.25; // [-0.25, 0.25]
                double value = 0.9 * previous + noise;
                spreads.Add(new Element("SPREAD", startDate.AddDays(i), value));
                previous = value;
            }

            // Act
            var halfLife = UtilityService.CalculateHalfLife(spreads, 60); // 使用60-bar窗口

            // 输出 spreads 序列
            Console.WriteLine("Date\t\tSpread");
            foreach (var s in spreads)
            {
                Console.WriteLine($"{s.DateTime:yyyy-MM-dd}\t{s.Value:F4}");
            }

            Console.WriteLine($"\nCalculated Half-Life: {halfLife:F4}");

            // Assert
            Assert.IsTrue(halfLife > 0 && halfLife < 100, $"Half-life result seems incorrect: {halfLife}");
        }



    }


}
