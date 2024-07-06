using YahooFinanceApi;


namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class YahooFinanceApiUnitTests
    {
        [TestMethod]
        public async Task GetDailyData_Should_Work()
        {
            // 设置时间范围（过去1年）
            var toDate = DateTime.UtcNow;
            var fromDate = toDate.AddMonths(-1);

            // 创建一个标的 VOO并请求历史数据
            var symbol = "VOO";
            var candles = await Yahoo.GetHistoricalAsync(symbol, fromDate, toDate, Period.Daily); // Daily, Weekly, Monthly

            // 访问OHLCV数据
            foreach (var candle in candles)
            {
                Console.WriteLine($"Date: {candle.DateTime}, Open: {candle.Open}, High: {candle.High}, Low: {candle.Low}, Close: {candle.Close}, Volume: {candle.Volume}");
            }
            Assert.IsTrue(candles != null && candles.Any());
        }

        [TestMethod]
        public async Task GetHistoricalAsync_InvalidSymbol_ShouldThrowException()
        {
            // Arrange
            string invalidSymbol = "invalidSymbol";
            DateTime startDate = new DateTime(2017, 1, 3);
            DateTime endDate = new DateTime(2017, 1, 4);

            // Act & Assert
            try
            {
                await Yahoo.GetHistoricalAsync(invalidSymbol, startDate, endDate, Period.Daily);
                // If no exception is thrown, the test will fail
                Assert.Fail("Expected NotFoundException was not thrown.");
            }
            catch (Exception ex)
            {
                Assert.AreEqual("Invalid ticker or endpoint for symbol 'invalidSymbol'.", ex.Message );               
            }
        }



    }
}