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

        [TestMethod]
        public void TestCalculateDiff()
        {
            // Load data from CSV files
            var timeSeries1 = LoadTimeSeries(AppDomain.CurrentDomain.BaseDirectory + "data\\ALGOUSDT.csv");
            var timeSeries2 = LoadTimeSeries(AppDomain.CurrentDomain.BaseDirectory + "data\\DASHUSDT.csv");

            // Initialize the calculator
            var calculator = new PairTradingDiffCalculator_FixLengthWindow(ResolutionLevel.Hourly);

            // Update the time series in the calculator
            calculator.UpdateTimerSeries(timeSeries1, timeSeries2);

            // Choose an endDateTime for the calculation, or use null to use the latest available
            DateTime? endDateTime = null;

            // Perform the diff calculation
            double diff = calculator.CalculateDiff(endDateTime);

            // Assert that the diff is within an expected range (for this example, we'll assume 0 is the expected value)
            Assert.AreEqual(1.89501, diff, 1e-5); // Adjust tolerance as needed
        }
    }
}
