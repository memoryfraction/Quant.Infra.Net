using Quant.Infra.Net.Shared.Model;
using System.Timers;
using System;

namespace Quant.Infra.Net.Shared.Service
{
    /// <summary>
    /// 用于规律性触发事件， 比如：每小时，每天，每分钟等; 
    /// 使用enum StartMode，可以设置从下一个小时/分钟/天开始; 
    /// 可以设置TimeSpan DelayTimeSpan属性， 延迟时间可以为正或负
    /// </summary>
    public class IntervalTrigger
    {
        public StartMode Mode { get; set; }
        public TimeSpan DelayTimeSpan { get; set; } = TimeSpan.Zero;
        public event EventHandler IntervalTriggered;

        private Timer _timer;
        private DateTime _startDateTime;
        private TimeSpan _triggerInterval;

        public IntervalTrigger(StartMode mode, TimeSpan delayTimeSpan)
        {
            Mode = mode;
            DelayTimeSpan = delayTimeSpan;
            _triggerInterval = GetTriggerInterval(mode);
        }

        // Starts the trigger event at the next whole hour/minute/day/second depending on the mode
        public void Start()
        {
            _startDateTime = CalculateNextTriggerTime();
            TimeSpan initialDelay = _startDateTime - DateTime.UtcNow + DelayTimeSpan;

            // 设置定时器初始间隔为1秒
            _timer = new Timer(1000);
            _timer.Elapsed += OnIntervalTriggered;
            _timer.AutoReset = true; // 确保每次都自动重置
            _timer.Start();
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }

        private void OnIntervalTriggered(object sender, ElapsedEventArgs e)
        {
            IntervalTriggered?.Invoke(this, EventArgs.Empty); // Fire the event

            // Schedule the next trigger
            _timer.Interval = _triggerInterval.TotalMilliseconds;
            _timer.AutoReset = true;
            _timer.Start();
        }

        private DateTime CalculateNextTriggerTime()
        {
            DateTime now = DateTime.UtcNow;

            switch (Mode)
            {
                case StartMode.NextSecond:
                    return new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second).AddSeconds(1);
                case StartMode.NextMinute:
                    return new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0).AddMinutes(1);
                case StartMode.NextHour:
                    return new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(1);
                case StartMode.NextDay:
                    return new DateTime(now.Year, now.Month, now.Day, 0, 0, 0).AddDays(1);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private TimeSpan GetTriggerInterval(StartMode mode)
        {
            return mode switch
            {
                StartMode.NextSecond => TimeSpan.FromSeconds(1),
                StartMode.NextMinute => TimeSpan.FromMinutes(1),
                StartMode.NextHour => TimeSpan.FromHours(1),
                StartMode.NextDay => TimeSpan.FromDays(1),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), "Unsupported mode")
            };
        }
    }
}