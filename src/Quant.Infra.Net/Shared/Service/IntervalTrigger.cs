using Quant.Infra.Net.Shared.Model;
using System;
using System.Timers;

namespace Quant.Infra.Net.Shared.Service
{
    /// <summary>
    /// 用于规律性触发事件，比如：每小时，每天，每分钟等;
    /// 支持: NextSecond, NextMinute, NextHour, NextDay, TodayBeforeUSMarketClose
    /// TodayBeforeUSMarketClose 模式中，DelayTimeSpan 表示相对于美东市场收盘前的提前/延迟时长（正值延后，负值提前）。
    /// </summary>
    public class IntervalTrigger
    {
        public StartMode Mode { get; set; }
        public TimeSpan DelayTimeSpan { get; set; } = TimeSpan.Zero;
        public event EventHandler IntervalTriggered;

        private Timer _timer;
        private DateTime _startDateTime;
        private TimeSpan _triggerInterval;

        /// <summary>
        /// 下次触发的 UTC 时间
        /// </summary>
        public DateTime NextTriggerTime => _startDateTime;

        public IntervalTrigger(StartMode mode, TimeSpan delayTimeSpan)
        {
            Mode = mode;
            DelayTimeSpan = delayTimeSpan;
            _triggerInterval = GetTriggerInterval(mode);
        }

        public void Start()
        {
            _startDateTime = CalculateNextTriggerTime();
            var timeUntilNext = _startDateTime - DateTime.UtcNow;

            _timer = new Timer(Math.Max(timeUntilNext.TotalMilliseconds, 1));
            _timer.Elapsed += OnIntervalTriggered;
            _timer.AutoReset = false;
            _timer.Start();

            LogNextTrigger();
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer?.Dispose();
        }

        private void OnIntervalTriggered(object sender, ElapsedEventArgs e)
        {
            IntervalTriggered?.Invoke(this, EventArgs.Empty);

            _startDateTime = CalculateNextTriggerTime();
            var timeUntilNext = _startDateTime - DateTime.UtcNow;
            _timer.Interval = Math.Max(timeUntilNext.TotalMilliseconds, 1);
            _timer.Start();

            LogNextTrigger();
        }

        private DateTime CalculateNextTriggerTime()
        {
            DateTime utcNow = DateTime.UtcNow;
            DateTime baseNext;

            switch (Mode)
            {
                case StartMode.NextSecond:
                    // 基础触发：下一个整秒
                    baseNext = utcNow.Millisecond == 0
                        ? new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, utcNow.Second, DateTimeKind.Utc)
                        : new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, utcNow.Second, DateTimeKind.Utc).AddSeconds(1);
                    break;

                case StartMode.NextMinute:
                    // 基础触发：下一个整分
                    baseNext = (utcNow.Second == 0 && utcNow.Millisecond == 0)
                        ? new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, 0, DateTimeKind.Utc)
                        : new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, utcNow.Minute, 0, DateTimeKind.Utc).AddMinutes(1);
                    break;

                case StartMode.NextHour:
                    // 基础触发：下一个整点
                    baseNext = (utcNow.Minute == 0 && utcNow.Second == 0 && utcNow.Millisecond == 0)
                        ? new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0, DateTimeKind.Utc)
                        : new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
                    break;

                case StartMode.NextDay:
                    // 基础触发：下一个零点
                    baseNext = (utcNow.Hour == 0 && utcNow.Minute == 0 && utcNow.Second == 0 && utcNow.Millisecond == 0)
                        ? new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc)
                        : new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
                    break;

                case StartMode.TodayBeforeUSMarketClose:
                    // 美东时区自动处理 EST/EDT
                    var estZone = GetEasternTimeZone();
                    var estNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, estZone);
                    // 当日 16:00 收盘（美东时间）
                    var estClose = new DateTime(estNow.Year, estNow.Month, estNow.Day, 16, 0, 0, DateTimeKind.Unspecified);
                    var utcClose = TimeZoneInfo.ConvertTimeToUtc(estClose, estZone);
                    // 基础触发 = 收盘前 DelayTimeSpan
                    var target = utcClose + DelayTimeSpan;

                    if (utcNow < target)
                    {
                        baseNext = target;
                    }
                    else
                    {
                        // 下一个交易日收盘
                        var estCloseTomorrow = estClose.AddDays(1);
                        var utcCloseTomorrow = TimeZoneInfo.ConvertTimeToUtc(estCloseTomorrow, estZone);
                        baseNext = utcCloseTomorrow + DelayTimeSpan;
                    }
                    // 由于已经在这里加了 DelayTimeSpan，所以跳过后续统一加延迟
                    return baseNext;

                default:
                    throw new ArgumentOutOfRangeException(nameof(Mode), Mode, null);
            }

            // 对于 NextSecond/NextMinute/NextHour/NextDay，一律加上统一的 DelayTimeSpan
            return baseNext + DelayTimeSpan;
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
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported mode")
            };
        }

        private void LogNextTrigger()
        {
            var estZone = GetEasternTimeZone();
            var estTime = TimeZoneInfo.ConvertTimeFromUtc(_startDateTime, estZone);
            bool isDst = estZone.IsDaylightSavingTime(estTime);
            string abbr = isDst ? "EDT" : "EST";
            Console.WriteLine($"[IntervalTrigger] Mode={Mode}, NextTrigger (EST/EDT)={estTime:yyyy-MM-dd HH:mm} {abbr}, UTC={_startDateTime:HH:mm:ss}");
        }

        private static TimeZoneInfo GetEasternTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); // Windows
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); // Linux/macOS
            }
        }
    }
}