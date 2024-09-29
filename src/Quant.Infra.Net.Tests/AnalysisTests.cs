using CsvHelper.Configuration;
using CsvHelper;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Analysis.Service;
using Quant.Infra.Net.Shared.Service;
using Quant.Infra.Net.SourceData.Service;
using ScottPlot;
using System.Diagnostics;
using System.Globalization;
using Quant.Infra.Net.SourceData.Model;
using Python.Runtime;
using Quant.Infra.Net.Shared.Model;

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
            double[] stationarySeries = { 1, -1, 1, -1, 1, -1, 1, -1 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
            bool isStationary = _analysisService.AugmentedDickeyFullerTest(stationarySeries,0.05);

            // Assert
            Assert.AreEqual(true, isStationary);
        }

        [TestMethod]
        public void NonStationaryTest_Should_Work()
        {
            // Arrange
            double[] nonStationarySeries = { 1, 2, 3, 4, 5, 6, 7, 8 };

            // Act
            var _analysisService = _serviceProvider.GetRequiredService<IAnalysisService>();
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
            var seriesA = ReadCsv(symbol1FullPathFileName);
            var seriesB = ReadCsv(symbol2FullPathFileName);

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


        /// <summary>
        /// 对于Python的Ols结果， $"spread = {symbolA} - ({slope} * {symbolB} + {intercept})";
        /// </summary>
        [TestMethod]
        public void Python_LinerRegression_Should_Work()
        {
            // 初始化变量
            var condaVenvHomePath = @"D:\ProgramData\PythonVirtualEnvs\pair_trading";
            var pythonDllFileName = "python39.dll";
            var pythonFullPathFileName = Path.Combine(condaVenvHomePath, pythonDllFileName);
            PythonInfraModel infra = PythonNetInfra.GetPythonInfra(condaVenvHomePath, "python39.dll");
            if (Runtime.PythonDLL == null || Runtime.PythonDLL != pythonFullPathFileName)
            {
                Runtime.PythonDLL = infra.PythonDLL;
            }
            PythonEngine.PythonHome = infra.PythonHome;
            PythonEngine.PythonPath = infra.PythonPath;
            PythonEngine.Initialize(); // 初始化Python引擎

            // 使用Python GIL
            using (Py.GIL())
            {
                try
                {
                    // Import sys and append all directories including modelDirectory
                    var pythonDirectory = AppDomain.CurrentDomain.BaseDirectory + "Python";
                    PythonEngine.Exec($"import sys; sys.path.append(r'{pythonDirectory}');");
                    OLSRegressionData data = new OLSRegressionData();
                    var symbolA = "DASH";
                    var symbolB = "ALGO";
                    var symbol1FullPathFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", $"{symbolA}USDT.csv");
                    var symbol2FullPathFileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", $"{symbolB}USDT.csv");
                    // 读取ALGOUSDT.csv， 和DASHUSDT.csv作为SeriesA，SeriesB
                    data.SeriesA = ReadCsv(symbol1FullPathFileName);
                    data.SeriesB = ReadCsv(symbol2FullPathFileName);          
                    var pyObjectResponse = RunScript<OLSRegressionData>("MySamplePython", "ols_regression", data);
                    // 从pyObjectResponse中获取回归结果，假设返回对象有属性'a'和'constant'
                    var a = pyObjectResponse.GetItem("a").As<double>();
                    var constant = pyObjectResponse.GetItem("constant").As<double>();
                    
                    string formula = $"spread = {symbolA} - ({a} * {symbolB} + {constant})";
                    Console.WriteLine($"{formula}");
                }
                catch (PythonException ex)
                {
                    Console.WriteLine($"Error importing sys or adding path: {ex.Message}");
                    throw;
                }
            }
        }

        PyObject RunScript<T>(string scriptFileNameWithoutExtension, string methodName, T obj) where T : class
        {
            var pythonScript = Py.Import(scriptFileNameWithoutExtension);
            var pythonObject = obj.ToPython();
            PyObject response = pythonScript.InvokeMethod(methodName, new PyObject[] { pythonObject });
            return response;
        }

        // 对比结果
        // C#
        // diff = DASH - (112.42454874562999 * ALGO) - (8.464050376897873)

        // Python
        // diff = DASH - (112.42454874563064 * ALGO + 8.464050376897694)
        // 说明：Python回归和C#回归，入参顺序相反时，会得出相同的结果;

        static List<double> ReadCsv(string filePath)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            };

            using (var reader = new StreamReader(filePath))
            using (var csv = new CsvReader(reader, config))
            {
                // 读取 CSV 的表头
                csv.Read();
                csv.ReadHeader();

                // 创建一个 List<double> 来保存 Close 列的数据
                var closeValues = new List<double>();

                // 读取每一行数据
                while (csv.Read())
                {
                    // 获取 Close 列的数据并添加到列表中
                    var closeValue = csv.GetField<double>("Close");
                    closeValues.Add(closeValue);
                }

                return closeValues;
            }
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

    }

    
}
