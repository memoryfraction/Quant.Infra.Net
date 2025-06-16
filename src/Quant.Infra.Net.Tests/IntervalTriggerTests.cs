using Quant.Infra.Net.Shared.Model;
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
        private bool _eventTriggered;

        [TestInitialize]
        public void Setup()
        {
            _eventTriggered = false;
        }

        /// <summary>
        /// 测试从下一个小时开始触发事件，并设置一个小的延迟时间。
        /// </summary>
        [TestMethod]
        public void TestNextHourTriggerWithDelay()
        {
            IntervalTrigger trigger = new IntervalTrigger(StartMode.NextHour, TimeSpan.FromMinutes(-1));
            trigger.IntervalTriggered += OnIntervalTriggered;
            trigger.Start();

            // 等待2秒钟以确保触发器启动并计算了正确的时间
            Thread.Sleep(2000);

            Assert.IsFalse(_eventTriggered, "事件不应该马上触发");
        }


        /// <summary>
        /// 美股日级别测试TodayBeforeUSMarketClose：DelayTimeSpan = -2 分钟时，下次触发应为 15:58 EST/EDT。
        /// when Delay = -2 min, next trigger should be 15:58 EST/EDT.
        /// </summary>
        [TestMethod]
        public void TestTodayBeforeUSMarketCloseWithNegativeDelay()
        {
            IntervalTrigger trigger = new IntervalTrigger(StartMode.TodayBeforeUSMarketClose, TimeSpan.FromMinutes(-2));
            trigger.IntervalTriggered += OnIntervalTriggered;
            trigger.Start();

            // 等待2秒钟以确保触发器启动并计算了正确的时间
            Thread.Sleep(2000);

            Assert.IsFalse(_eventTriggered, "事件不应该立即触发，因为设置为下一分钟");
        }




        /// <summary>
        /// 测试从下一个分钟开始触发事件，延迟为0。
        /// </summary>
        [TestMethod]
        public void TestNextMinuteTriggerWithoutDelay()
        {
            IntervalTrigger trigger = new IntervalTrigger(StartMode.NextMinute, TimeSpan.Zero);
            trigger.IntervalTriggered += OnIntervalTriggered;
            trigger.Start();

            // 等待2秒钟以确保触发器启动并计算了正确的时间
            Thread.Sleep(2000);

            Assert.IsFalse(_eventTriggered, "事件不应该立即触发，因为设置为下一分钟");
        }

        /// <summary>
        /// 测试从下一个秒开始触发事件。
        /// </summary>
        [TestMethod]
        public void TestNextSecondTrigger()
        {
            IntervalTrigger trigger = new IntervalTrigger(StartMode.NextSecond, TimeSpan.Zero);
            trigger.IntervalTriggered += OnIntervalTriggered;
            trigger.Start();

            // 等待2秒钟确保触发事件
            Thread.Sleep(1500);

            Assert.IsTrue(_eventTriggered, "事件应该在1秒后触发");
        }

        /// <summary>
        /// 测试从下一个天开始触发事件。
        /// </summary>
        [TestMethod]
        public void TestNextDayTrigger()
        {
            IntervalTrigger trigger = new IntervalTrigger(StartMode.NextDay, TimeSpan.Zero);
            trigger.IntervalTriggered += OnIntervalTriggered;
            trigger.Start();

            // 等待2秒钟以确保触发器启动
            Thread.Sleep(2000);

            Assert.IsFalse(_eventTriggered, "事件不应该立即触发，因为设置为下一天");
        }

        

        private void OnIntervalTriggered(object sender, EventArgs e)
        {
            _eventTriggered = true;
        }

    }
}