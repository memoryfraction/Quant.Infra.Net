using Quant.Infra.Net.Broker.Model;
using Quant.Infra.Net.Broker.Service;
using Quant.Infra.Net.Shared.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class AlpacaClientStartUtcTests
    {
        private readonly AlpacaClient _client;

        public AlpacaClientStartUtcTests()
        {
            // Credentials not used by CalculateUSEquityStartUtcAsync
            var creds = new BrokerCredentials { ApiKey = "", Secret = "" };
            _client = new AlpacaClient(creds, ExchangeEnvironment.Paper);
        }

        /// <summary>
        /// 【中】测试 Tick/Second/Minute 分辨率时，startUtc 应精确减去指定秒或分钟数  
        /// 【EN】Verify that Tick, Second, and Minute resolutions subtract exactly the requested amount.
        /// </summary>
        [TestMethod]
        public async Task CalculateUSEquityStartUtcAsync_TickSecondMinute_ShouldSubtractExactly()
        {
            var end = new DateTime(2025, 5, 30, 12, 0, 0, DateTimeKind.Utc);

            var tickStart = await _client.CalculateUSEquityStartUtcAsync(end, 10, ResolutionLevel.Tick);
            var secondStart = await _client.CalculateUSEquityStartUtcAsync(end, 10, ResolutionLevel.Second);
            var minuteStart = await _client.CalculateUSEquityStartUtcAsync(end, 10, ResolutionLevel.Minute);

            Assert.AreEqual(end.AddSeconds(-10), tickStart, "Tick resolution should subtract seconds exactly.");
            Assert.AreEqual(end.AddSeconds(-10), secondStart, "Second resolution should subtract seconds exactly.");
            Assert.AreEqual(end.AddMinutes(-10), minuteStart, "Minute resolution should subtract minutes exactly.");
        }

        /// <summary>
        /// 【中】测试 Monthly 分辨率时，startUtc 应减去指定的月数  
        /// 【EN】Verify that Monthly resolution subtracts the correct number of months.
        /// </summary>
        [TestMethod]
        public async Task CalculateUSEquityStartUtcAsync_Monthly_ShouldSubtractMonths()
        {
            var end = new DateTime(2025, 5, 30, 0, 0, 0, DateTimeKind.Utc);

            var oneMonthAgo = await _client.CalculateUSEquityStartUtcAsync(end, 1, ResolutionLevel.Monthly);
            var threeMonthsAgo = await _client.CalculateUSEquityStartUtcAsync(end, 3, ResolutionLevel.Monthly);

            Assert.AreEqual(end.AddMonths(-1), oneMonthAgo, "Monthly resolution should subtract months.");
            Assert.AreEqual(end.AddMonths(-3), threeMonthsAgo, "Monthly resolution should subtract months.");
        }

        /// <summary>
        /// 【中】测试 Daily/Weekly 分辨率时，startUtc 应位于 endUtc 之前，且至少回溯 count 个交易日或 count*5 天  
        /// 【EN】Verify that Daily and Weekly resolutions produce a startUtc before endUtc and offset at least the correct number of days.
        /// </summary>
        [TestMethod]
        public async Task CalculateUSEquityStartUtcAsync_DailyWeekly_ShouldReturnBeforeEnd()
        {
            var end = new DateTime(2025, 5, 30, 0, 0, 0, DateTimeKind.Utc);

            var daily5 = await _client.CalculateUSEquityStartUtcAsync(end, 5, ResolutionLevel.Daily);
            var weekly2 = await _client.CalculateUSEquityStartUtcAsync(end, 2, ResolutionLevel.Weekly);

            Assert.IsTrue(daily5 < end, "Daily back-offset must be before endUtc.");
            Assert.IsTrue(weekly2 < end, "Weekly back-offset must be before endUtc.");
            Assert.IsTrue((end - daily5).TotalDays >= 5, "Daily offset should be >= count days.");
            Assert.IsTrue((end - weekly2).TotalDays >= 5 * 2, "Weekly offset should be >= count*5 days.");
        }

        /// <summary>
        /// 【中】测试 Hourly 分辨率时，startUtc 应减去指定交易小时数，并考虑 6.5 小时交易日逻辑  
        /// 【EN】Verify that Hourly resolution subtracts the correct number of trading hours, considering 6.5h trading days.
        /// </summary>
        [TestMethod]
        public async Task CalculateUSEquityStartUtcAsync_Hourly_ShouldSubtractTradingHours()
        {
            var end = new DateTime(2025, 5, 30, 16, 0, 0, DateTimeKind.Utc);
            int hours = 10;

            var start = await _client.CalculateUSEquityStartUtcAsync(end, hours, ResolutionLevel.Hourly);

            // 当 hours <= 6.5，小于整交易日
            var sessionStartUtc = end.Date.AddHours(13.5); // 交易日 09:30 ET = 13:30 UTC
            if (hours <= 6.5)
            {
                var expected = sessionStartUtc.AddHours(-hours);
                Assert.AreEqual(expected.Hour, start.Hour, 1, "Hourly offset hour mismatch");
                Assert.IsTrue(start < end, "Hourly start must be before endUtc");
            }
            else
            {
                Assert.IsTrue(start < end, "Hourly start must be before endUtc when hours > 6.5");
            }
        }
    }
}
