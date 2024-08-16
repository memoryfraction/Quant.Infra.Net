using Quant.Infra.Net.Analysis;
using Quant.Infra.Net.Shared.Model;
using System.Globalization;

namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class PairTradingTests
    {
        private List<TimeSeriesElement> LoadTimeSeries(string fullPathFilename)
        {
            var timeSeries = new List<TimeSeriesElement>();

            using (var reader = new StreamReader(fullPathFilename))
            {
                string headerLine = reader.ReadLine(); // Skip the header line
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var values = line.Split(',');

                    var dateTime = DateTime.Parse(values[0]);
                    var close = double.Parse(values[4], CultureInfo.InvariantCulture); // Assuming the Close column is the 5th column

                    timeSeries.Add(new TimeSeriesElement
                    {
                        DateTime = dateTime,
                        Value = close
                    });
                }
            }

            return timeSeries;
        }

        /// <summary>
        /// 对自定义Calculator的测试用例;
        /// </summary>
        [TestMethod]
        public void TestCalculateDiff()
        {
            var symbol1 = "ALGO";
            var symbol2 = "DASH";
            // Load data from CSV files
            var timeSeries1 = LoadTimeSeries(AppDomain.CurrentDomain.BaseDirectory + $"data\\{symbol1}USDT.csv");
            var timeSeries2 = LoadTimeSeries(AppDomain.CurrentDomain.BaseDirectory + $"data\\{symbol2}USDT.csv");

            // Initialize the calculator
            var calculator = new PairTradingDiffCalculator_FixLengthWindow(symbol1, symbol2, ResolutionLevel.Hourly);

            // Update the time series in the calculator
            calculator.UpdateTimerSeries(timeSeries1, timeSeries2);

            // Choose an endDateTime for the calculation, or use null to use the latest available
            DateTime? endDateTime = null;

            // Perform the diff calculation
            double diff = calculator.CalculateDiff(endDateTime);

            // Assert that the diff is within an expected range (for this example, we'll assume 0 is the expected value)
            Assert.AreEqual(1.895, diff, 1e-5); // Adjust tolerance as needed
        }


        /// <summary>
        /// 生成diff公式应该工作
        /// </summary>
        [TestMethod]
        public void Generate_Diff_Equation_Should_Work()
        {
            var symbol1 = "ALGO";
            var symbol2 = "DASH";
            // Load data from CSV files
            var timeSeries1 = LoadTimeSeries(AppDomain.CurrentDomain.BaseDirectory + $"data\\{symbol1}USDT.csv");
            var timeSeries2 = LoadTimeSeries(AppDomain.CurrentDomain.BaseDirectory + $"data\\{symbol2}USDT.csv");

            // Initialize the calculator
            var calculator = new PairTradingDiffCalculator_FixLengthWindow(symbol1, symbol2, ResolutionLevel.Hourly);

            // Update the time series in the calculator
            calculator.UpdateTimerSeries(timeSeries1, timeSeries2);

            // Choose an endDateTime for the calculation, or use null to use the latest available
            DateTime? endDateTime = null;

            // Perform the diff equation generation
            var equation = calculator.PrintEquation();

            Assert.IsNotNull(equation); // Adjust tolerance as needed
            Console.WriteLine(equation);
        }


    }
}
