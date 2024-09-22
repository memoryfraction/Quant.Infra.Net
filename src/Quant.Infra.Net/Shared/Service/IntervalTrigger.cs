using System;
using System.Timers;

namespace Quant.Infra.Net.Shared.Service
{
    /// <summary>
    /// 抽象触发器基类，定义了通用的定时触发逻辑。
    /// The abstract base class for triggers, defining the general timed trigger logic.
    /// </summary>
    public abstract class IntervalTriggerBase
    {
        /// <summary>
        /// 下次触发的时间
        /// The next trigger time.
        /// </summary>
        public DateTime StartUtcDateTime { get; protected set; }

        /// <summary>
        /// 定时器的时间间隔
        /// The interval for the timer.
        /// </summary>
        public TimeSpan TriggerInterval { get; set; }

        /// <summary>
        /// 提前时间，触发时间将提前此时间，默认是1分钟，可以修改。
        /// The advance time, by which the trigger time will be moved earlier.
        /// </summary>
        public virtual TimeSpan AdvanceTime { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// 定义触发的事件
        /// The event that is triggered when the interval elapses.
        /// </summary>
        public event EventHandler IntervalTriggered;

        protected Timer _timer;

        /// <summary>
        /// 启动触发器
        /// Starts the trigger.
        /// </summary>
        public void Start()
        {
            CalculateStartDateTime();
            TimeSpan initialInterval = StartUtcDateTime - DateTime.UtcNow;
            _timer = new Timer(initialInterval.TotalMilliseconds);
            _timer.Elapsed += OnInitialIntervalElapsed;
            _timer.Start();
        }

        /// <summary>
        /// 计算下次触发时间的抽象方法
        /// Abstract method to calculate the next trigger time.
        /// </summary>
        protected abstract void CalculateStartDateTime();

        private void OnInitialIntervalElapsed(object sender, ElapsedEventArgs e)
        {
            _timer.Stop();
            _timer.Elapsed -= OnInitialIntervalElapsed;

            _timer = new Timer(TriggerInterval.TotalMilliseconds);
            _timer.Elapsed += OnIntervalTriggered;
            _timer.Start();
        }

        private void OnIntervalTriggered(object sender, ElapsedEventArgs e)
        {
            IntervalTriggered?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 停止触发器
        /// Stops the trigger.
        /// </summary>
        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Elapsed -= OnIntervalTriggered;
                _timer = null;
            }
        }
    }

    /// <summary>
    /// 加密货币市场关闭时间触发器 (24小时交易市场)
    /// Cryptocurrency market close trigger (for 24-hour markets).
    /// </summary>
    public class CryptoMarketCloseIntervalTrigger : IntervalTriggerBase
    {
        /// <summary>
        /// 计算加密货币市场的下一个关仓时间 (假设为24:00)
        /// Calculates the next crypto market close time (assuming 24:00 for a 24-hour market).
        /// </summary>
        protected override void CalculateStartDateTime()
        {
            DateTime now = DateTime.UtcNow;
            DateTime marketClose = new DateTime(now.Year, now.Month, now.Day, 23, 59, 59); // 24:00 UTC

            if (now >= marketClose)
            {
                // 如果当前时间已经超过关仓时间，则设定为第二天的关仓时间
                marketClose = marketClose.AddDays(1);
            }

            StartUtcDateTime = marketClose - AdvanceTime; // 应用提前时间
        }
    }

    /// <summary>
    /// 美股闭市时间触发器 (美东时间16:00)
    /// US stock market close trigger (4:00 PM EST).
    /// </summary>
    public class UsStockMarketCloseIntervalTrigger : IntervalTriggerBase
    {
        /// <summary>
        /// 计算美股的下一个闭市时间 (美东时间16:00)
        /// Calculates the next US stock market close time (4:00 PM EST).
        /// </summary>
        protected override void CalculateStartDateTime()
        {
            DateTime now = DateTime.UtcNow;
            TimeZoneInfo estTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime estNow = TimeZoneInfo.ConvertTimeFromUtc(now, estTimeZone);
            DateTime marketClose = new DateTime(estNow.Year, estNow.Month, estNow.Day, 16, 0, 0); // 16:00 美东时间

            if (estNow >= marketClose)
            {
                // 如果当前时间已经超过闭市时间，则设定为第二天的闭市时间
                marketClose = marketClose.AddDays(1);
            }

            var StartLocalDateTime = UtilityService.AdjustToNextWeekday(marketClose - AdvanceTime); // 应用提前时间，调整到下一个工作日
            StartUtcDateTime = TimeZoneInfo.ConvertTimeToUtc(StartLocalDateTime, estTimeZone); // 并转化为Utc时间
        }
    }

    /// <summary>
    /// 美股开盘时间触发器 (美东时间9:30)
    /// US stock market opening time trigger (9:30 AM EST).
    /// </summary>
    public class UsStockMarketOpenIntervalTrigger : IntervalTriggerBase
    {
        /// <summary>
        /// 设置为不提前触发
        /// Sets the advance time to zero for opening trigger.
        /// </summary>
        public override TimeSpan AdvanceTime { get; set; } = TimeSpan.FromMinutes(0);

        /// <summary>
        /// 计算美股的下一个开盘时间 (美东时间9:30)
        /// Calculates the next US stock market open time (9:30 AM EST).
        /// </summary>
        protected override void CalculateStartDateTime()
        {
            DateTime now = DateTime.UtcNow;
            TimeZoneInfo estTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            DateTime estNow = TimeZoneInfo.ConvertTimeFromUtc(now, estTimeZone);
            DateTime marketOpen = new DateTime(estNow.Year, estNow.Month, estNow.Day, 9, 30, 0); // 9:30 美东时间

            if (estNow >= marketOpen)
            {
                // 如果当前时间已经超过开盘时间，则设定为第二天的开盘时间
                marketOpen = marketOpen.AddDays(1);
            }

            var StartLocalDateTime = UtilityService.AdjustToNextWeekday(marketOpen - AdvanceTime);             // 调整到下一个工作日
            StartUtcDateTime = TimeZoneInfo.ConvertTimeToUtc(StartLocalDateTime, estTimeZone); // 应用提前时间
        }
    }
}