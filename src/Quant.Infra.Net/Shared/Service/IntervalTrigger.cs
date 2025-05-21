using Quant.Infra.Net.Shared.Model;
using System;
using System.Timers;

namespace Quant.Infra.Net.Shared.Service
{
    /// <summary>
    /// 用于规律性触发事件，比如：每小时，每天，每分钟等;
    /// 使用enum StartMode，可以设置从下一个小时/分钟/天开始;
    /// 可以设置TimeSpan DelayTimeSpan属性，延迟时间可以为正或负
    /// </summary>
    public class IntervalTrigger
    {
        public StartMode Mode { get; set; }
        public TimeSpan DelayTimeSpan { get; set; } = TimeSpan.Zero;
        public event EventHandler IntervalTriggered;

        private Timer _timer;
        private DateTime _startDateTime;
        private TimeSpan _triggerInterval;

        // 只读属性，用于获取下次触发的时间
        public DateTime NextTriggerTime => _startDateTime;

        public IntervalTrigger(StartMode mode, TimeSpan delayTimeSpan)
        {
            Mode = mode;
            DelayTimeSpan = delayTimeSpan;
            _triggerInterval = GetTriggerInterval(mode);
        }

        // Starts the trigger event at the next whole hour/minute/day/second depending on the mode
        public void Start()
        {
            _startDateTime = CalculateNextTriggerTime() + DelayTimeSpan;

            // 初次定时器的间隔为当前时间到下一个触发时间的差值
            TimeSpan timeUntilNextTrigger = _startDateTime - DateTime.UtcNow;

            // 设置定时器的间隔
            _timer = new Timer(timeUntilNextTrigger.TotalMilliseconds);
            _timer.Elapsed += OnIntervalTriggered;
            _timer.AutoReset = false; // 确保每次触发后重新设置时间间隔
            _timer.Start();

            // Display StartMode in English to prevent user confusion
            System.Console.WriteLine($"Start(). StartMode: {Mode}; The next trigger Utc time: {this.NextTriggerTime}");
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }

        private void OnIntervalTriggered(object sender, ElapsedEventArgs e)
        {
            // 触发事件
            IntervalTriggered?.Invoke(this, EventArgs.Empty);

            // 重新计算下一个触发时间
            _startDateTime = CalculateNextTriggerTime() + DelayTimeSpan;

            // 再次计算下一次触发时间间隔
            TimeSpan timeUntilNextTrigger = _startDateTime - DateTime.UtcNow;

            // 更新定时器的间隔，确保每次都精确计算
            if (timeUntilNextTrigger.TotalMilliseconds > 0)
            {
                _timer.Interval = timeUntilNextTrigger.TotalMilliseconds;
            }
            else
            {
                _timer.Interval = 1; // 立即触发
            }

            // 重启定时器
            _timer.Start();

            // Display StartMode in English to prevent user confusion
            System.Console.WriteLine($"OnIntervalTriggered(). StartMode: {Mode}; The next trigger Utc time: {this.NextTriggerTime}");
        }

        /// <summary>
        /// 根据当前时间计算并返回下一个整点时间
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private DateTime CalculateNextTriggerTime()
        {
            DateTime utcNow = DateTime.UtcNow;

            switch (Mode)
            {
                case StartMode.NextSecond:
                    if (utcNow.Millisecond == 0)
                    {
                        return new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, utcNow.Second);
                    }
                    else
                    {
                        return new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, utcNow.Second).AddSeconds(1);
                    }

                case StartMode.NextMinute:
                    if (utcNow.Second == 0 && utcNow.Millisecond == 0)
                    {
                        return new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, 0);
                    }
                    else
                    {
                        return new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, 0).AddMinutes(1);
                    }

                case StartMode.NextHour:
                    if (utcNow.Minute == 0 && utcNow.Second == 0 && utcNow.Millisecond == 0)
                    {
                        return new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0);
                    }
                    else
                    {
                        return new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0).AddHours(1);
                    }

                case StartMode.NextDay:
                    if (utcNow.Hour == 0 && utcNow.Minute == 0 && utcNow.Second == 0 && utcNow.Millisecond == 0)
                    {
                        return new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0);
                    }
                    else
                    {
                        return new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0).AddDays(1);
                    }
                case StartMode.TodayBeforeUSMarketClose:
                    {
                        // 1. 定位到美东时区（含夏令时）
                        TimeZoneInfo easternZone;
                        try
                        {
                            easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); // Windows
                        }
                        catch (TimeZoneNotFoundException)
                        {
                            easternZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); // Linux/macOS
                        }

                        // 2. 将当前 UTC 转为美东时间
                        DateTime estNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, easternZone);

                        // 3. 当日美东 16:00
                        DateTime todayCloseEst = new DateTime(
                            estNow.Year, estNow.Month, estNow.Day,
                            16, 0, 0, DateTimeKind.Unspecified);

                        // 4. 判断是今天还是明天
                        DateTime targetCloseEst = estNow < todayCloseEst
                            ? todayCloseEst
                            : todayCloseEst.AddDays(1);

                        // 5. **不要在这里加 DelayTimeSpan**，只返回基准关闭时间的 UTC
                        return TimeZoneInfo.ConvertTimeToUtc(targetCloseEst, easternZone);
                    }

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
                StartMode.TodayBeforeUSMarketClose => TimeSpan.FromDays(1),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), "Unsupported mode")
            };
        }
    }
}