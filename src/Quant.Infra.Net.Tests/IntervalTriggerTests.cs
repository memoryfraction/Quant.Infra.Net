using Quant.Infra.Net.Shared.Service;

namespace Quant.Infra.Net.Tests
{
    /// <summary>
    /// 测试区间触发器的单元测试类
    /// Unit test class for interval triggers.
    /// </summary>
    [TestClass]
    public class IntervalTriggerTests
    {
        private DateTime fixedCurrentTime;

        /// <summary>
        /// 设置固定的当前时间，测试开始前调用
        /// Sets a fixed current time, called before the tests.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            fixedCurrentTime = new DateTime(2024, 9, 21, 14, 0, 0, DateTimeKind.Utc); // 2024年9月21日 14:00 UTC
        }

        /// <summary>
        /// 测试加密货币市场闭市触发器
        /// Tests the cryptocurrency market close trigger.
        /// </summary>
        [TestMethod]
        public void TestCryptoMarketCloseTrigger()
        {
            // Arrange
            IntervalTriggerBase trigger = new CryptoMarketCloseIntervalTrigger
            {
                TriggerInterval = TimeSpan.FromHours(24), // 每24小时触发一次
                AdvanceTime = TimeSpan.FromMinutes(1)      // 提前1分钟触发
            };

            bool eventTriggered = false;

            // Act
            trigger.IntervalTriggered += (sender, e) => eventTriggered = true;
            trigger.Start();

            // Assert
            Assert.IsTrue(trigger.StartUtcDateTime <= fixedCurrentTime.AddHours(24).AddMinutes(-1)); // 确保时间计算正确
            Assert.IsFalse(eventTriggered); // 事件不会立即触发，因为触发时间是未来
        }

        /// <summary>
        /// 测试美股开盘时间触发器
        /// Tests the US stock market opening trigger.
        /// </summary>
        [TestMethod]
        public void TestUsStockMarketOpenTrigger()
        {
            // Arrange
            IntervalTriggerBase trigger = new UsStockMarketOpenIntervalTrigger
            {
                TriggerInterval = TimeSpan.FromDays(1), // 每天触发一次
                AdvanceTime = TimeSpan.FromMinutes(1)    // 提前1分钟触发
            };

            bool eventTriggered = false;

            // Act
            trigger.IntervalTriggered += (sender, e) => eventTriggered = true;
            trigger.Start();

            // Assert
            DateTime expectedStartTime = GetNextMarketOpenTime(fixedCurrentTime);
            Assert.IsTrue(trigger.StartUtcDateTime <= expectedStartTime); // 确保时间计算正确
            Assert.IsFalse(eventTriggered); // 事件不会立即触发，因为触发时间是未来
        }

        /// <summary>
        /// 测试美股闭市时间触发器
        /// Tests the US stock market close trigger.
        /// </summary>
        [TestMethod]
        public void TestUsStockMarketCloseTrigger()
        {
            // Arrange
            IntervalTriggerBase trigger = new UsStockMarketCloseIntervalTrigger
            {
                TriggerInterval = TimeSpan.FromDays(1), // 每天触发一次
                AdvanceTime = TimeSpan.FromMinutes(1)    // 提前1分钟触发
            };

            bool eventTriggered = false;

            // Act
            trigger.IntervalTriggered += (sender, e) => eventTriggered = true;
            trigger.Start();

            // Assert
            DateTime nextMarketCloseTime = GetNextMarketCloseTime(fixedCurrentTime);
            Assert.IsTrue(trigger.StartUtcDateTime <= nextMarketCloseTime.AddMinutes(-1)); // 确保时间计算正确
            Assert.IsFalse(eventTriggered); // 事件不会立即触发，因为触发时间是未来
        }


        /// <summary>
        /// 获取下一个美股开盘时间，如果当前时间为周末，则调整到下周一的9:30 AM
        /// Gets the next US stock market open time, adjusting to Monday if it's the weekend.
        /// </summary>
        /// <param name="currentTime">当前时间</param>
        /// <returns>下一个开盘时间</returns>
        private DateTime GetNextMarketOpenTime(DateTime currentTime)
        {
            DateTime marketOpenTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 14, 30, 0, DateTimeKind.Utc); // 9:30 AM EST = UTC 14:30

            // 使用 UtilityService 中的 AdjustToNextWeekday 方法
            marketOpenTime = UtilityService.AdjustToNextWeekday(marketOpenTime);

            // 如果当前时间已经超过开盘时间，则设定为第二天的开盘时间
            if (currentTime >= marketOpenTime)
            {
                marketOpenTime = marketOpenTime.AddDays(1);
            }

            return marketOpenTime;
        }

        /// <summary>
        /// 获取下一个美股闭市时间，如果当前时间为周末，则调整到下周一的4:00 PM
        /// Gets the next US stock market close time, adjusting to Monday if it's the weekend.
        /// </summary>
        /// <param name="currentTime">当前时间</param>
        /// <returns>下一个闭市时间</returns>
        private DateTime GetNextMarketCloseTime(DateTime currentTime)
        {
            DateTime marketCloseTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 20, 0, 0, DateTimeKind.Utc); // 4:00 PM EST = UTC 20:00

            // 使用 UtilityService 中的 AdjustToNextWeekday 方法
            marketCloseTime = UtilityService.AdjustToNextWeekday(marketCloseTime);

            // 如果当前时间已经超过闭市时间，则设定为第二天的闭市时间
            if (currentTime >= marketCloseTime)
            {
                marketCloseTime = marketCloseTime.AddDays(1);
            }

            return marketCloseTime;
        }

    }
}