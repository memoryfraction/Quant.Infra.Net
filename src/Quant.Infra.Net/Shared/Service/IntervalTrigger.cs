using Quant.Infra.Net.Shared.Model;
using System;
using System.Timers;

namespace Quant.Infra.Net.Shared.Service
{
    /// <summary>
    /// 用于规律性触发事件的定时触发器
    /// 支持模式：NextSecond, NextMinute, NextHour, NextDay, TodayBeforeUSMarketClose
    /// TodayBeforeUSMarketClose 模式中，DelayTimeSpan 表示相对于美东市场收盘时间(16:00)的偏移量（正数延后，负数提前）
    /// </summary>
    public class IntervalTrigger : IDisposable
    {
        /// <summary>
        /// 触发模式
        /// </summary>
        public StartMode Mode { get; }

        /// <summary>
        /// 时间偏移量（正数延后，负数提前）
        /// </summary>
        public TimeSpan DelayTimeSpan { get; }

        /// <summary>
        /// 定时触发事件
        /// </summary>
        public event EventHandler IntervalTriggered;

        /// <summary>
        /// 下次触发时间(UTC)
        /// </summary>
        public DateTime NextTriggerTime => _nextTriggerTime;

        private Timer _timer;
        private DateTime _nextTriggerTime;
        private readonly TimeSpan _triggerInterval;
        private readonly object _syncLock = new object();
        private bool _isDisposed;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="mode">触发模式</param>
        /// <param name="delayTimeSpan">时间偏移量</param>
        /// <exception cref="ArgumentOutOfRangeException">当mode不是有效值时抛出</exception>
        public IntervalTrigger(StartMode mode, TimeSpan delayTimeSpan)
        {
            // 参数校验
            if (!Enum.IsDefined(typeof(StartMode), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode), "Invalid trigger mode");
            }

            Mode = mode;
            DelayTimeSpan = delayTimeSpan;
            _triggerInterval = GetTriggerInterval(mode);
        }

        /// <summary>
        /// 启动定时触发器
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        public void Start()
        {
            lock (_syncLock)
            {
                ThrowIfDisposed();
                StopInternal(); // 确保先停止并清理旧的 Timer

                _nextTriggerTime = CalculateNextTriggerTime();
                var timeUntilNext = _nextTriggerTime - DateTime.UtcNow;

                _timer = new Timer(Math.Max(timeUntilNext.TotalMilliseconds, 100));
                _timer.Elapsed += OnTimerElapsed;
                _timer.AutoReset = false; // 手动重置模式
                _timer.Start();

                LogNextTrigger();
            }
        }

        /// <summary>
        /// 停止定时触发器
        /// </summary>
        public void Stop()
        {
            lock (_syncLock)
            {
                if (_isDisposed) return;
                StopInternal();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            lock (_syncLock)
            {
                if (_isDisposed) return;

                StopInternal();
                _isDisposed = true;
                GC.SuppressFinalize(this);
            }
        }

        // ========== 私有方法 ==========

        /// <summary>
        /// 内部停止方法，不检查释放状态
        /// </summary>
        private void StopInternal()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Elapsed -= OnTimerElapsed;
                _timer.Dispose();
                _timer = null;
            }
        }

        /// <summary>
        /// 定时器触发回调
        /// </summary>
        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // 1. 检查是否已释放（加锁）
            bool shouldProceed;
            lock (_syncLock)
            {
                shouldProceed = !_isDisposed && _timer != null;
            }

            // 2. 如果已释放，直接返回（不在 finally 里）
            if (!shouldProceed) return;

            // 3. 触发事件（不加锁，避免死锁）
            try
            {
                IntervalTriggered?.Invoke(this, EventArgs.Empty);
                // Serilog记录：触发时间和触发的事件,方便调试问题出在哪
                var debugMessage = $"[IntervalTrigger] Event triggered at {DateTime.UtcNow:HH:mm:ss} UTC in the IntervalTrigger instance";
                UtilityService.LogAndWriteLine(debugMessage, Serilog.Events.LogEventLevel.Debug);
            }
            catch (Exception ex)
            {
                var errorMessage = $"[IntervalTrigger] Event handler exception: {ex.Message}";
                UtilityService.LogAndWriteLine(errorMessage, Serilog.Events.LogEventLevel.Error);
            }

            // 4. 计算下次触发时间（加锁）
            lock (_syncLock)
            {
                if (_isDisposed || _timer == null) return;

                try
                {
                    _nextTriggerTime = CalculateNextTriggerTime();
                    var timeUntilNext = _nextTriggerTime - DateTime.UtcNow;
                    _timer.Interval = Math.Max(timeUntilNext.TotalMilliseconds, 100);
                    _timer.Start();
                    LogNextTrigger();
                }
                catch (Exception ex)
                {
                    var errorMessage = $"[IntervalTrigger] Timer reset failed: {ex.Message}";
                    UtilityService.LogAndWriteLine(errorMessage, Serilog.Events.LogEventLevel.Error);
                    StopInternal(); // 发生错误时停止定时器
                }
            }
        }

        /// <summary>
        /// 计算下次触发时间
        /// </summary>
        private DateTime CalculateNextTriggerTime()
        {
            DateTime utcNow = DateTime.UtcNow;
            DateTime baseNext;

            switch (Mode)
            {
                case StartMode.NextSecond:
                    baseNext = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day,
                        utcNow.Hour, utcNow.Minute, utcNow.Second, DateTimeKind.Utc)
                        .AddSeconds(1);
                    break;

                case StartMode.NextMinute:
                    baseNext = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day,
                        utcNow.Hour, utcNow.Minute, 0, DateTimeKind.Utc)
                        .AddMinutes(1);
                    break;

                case StartMode.NextHour:
                    baseNext = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day,
                        utcNow.Hour, 0, 0, DateTimeKind.Utc)
                        .AddHours(1);
                    break;

                case StartMode.NextDay:
                    baseNext = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day,
                        0, 0, 0, DateTimeKind.Utc)
                        .AddDays(1);
                    break;

                case StartMode.TodayBeforeUSMarketClose:
                    var estZone = GetEasternTimeZone();
                    var estNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, estZone);
                    var estClose = new DateTime(estNow.Year, estNow.Month, estNow.Day, 16, 0, 0, DateTimeKind.Unspecified);
                    var utcClose = TimeZoneInfo.ConvertTimeToUtc(estClose, estZone);
                    var target = utcClose + DelayTimeSpan;

                    baseNext = utcNow < target
                        ? target
                        : TimeZoneInfo.ConvertTimeToUtc(estClose.AddDays(1), estZone) + DelayTimeSpan;

                    return baseNext;

                default:
                    throw new ArgumentOutOfRangeException(nameof(Mode), Mode, null);
            }

            // 对于常规间隔，确保不会返回过去的时间
            while (baseNext + DelayTimeSpan <= utcNow)
            {
                baseNext += _triggerInterval;
            }

            return baseNext + DelayTimeSpan;
        }

        /// <summary>
        /// 获取触发间隔
        /// </summary>
        private TimeSpan GetTriggerInterval(StartMode mode)
        {
            return mode switch
            {
                StartMode.NextSecond => TimeSpan.FromSeconds(1),
                StartMode.NextMinute => TimeSpan.FromMinutes(1),
                StartMode.NextHour => TimeSpan.FromHours(1),
                StartMode.NextDay => TimeSpan.FromDays(1),
                StartMode.TodayBeforeUSMarketClose => TimeSpan.FromDays(1),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported trigger mode")
            };
        }

        /// <summary>
        /// 记录下次触发时间（用于调试）
        /// </summary>
        private void LogNextTrigger()
        {
            var estZone = GetEasternTimeZone();
            var estTime = TimeZoneInfo.ConvertTimeFromUtc(_nextTriggerTime, estZone);
            bool isDst = estZone.IsDaylightSavingTime(estTime);
            string abbr = isDst ? "EDT" : "EST";
            var message = $"[IntervalTrigger] Mode={Mode}, Next trigger (ET)={estTime:yyyy-MM-dd HH:mm} {abbr}, UTC={_nextTriggerTime:HH:mm:ss}";
            UtilityService.LogAndWriteLine(message, Serilog.Events.LogEventLevel.Debug);

        }

        /// <summary>
        /// 获取美东时区
        /// </summary>
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

        /// <summary>
        /// 检查对象是否已释放
        /// </summary>
        /// <exception cref="ObjectDisposedException">对象已释放时抛出</exception>
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <summary>
        /// 析构函数（安全备份）
        /// </summary>
        ~IntervalTrigger()
        {
            Dispose();
        }
    }
}