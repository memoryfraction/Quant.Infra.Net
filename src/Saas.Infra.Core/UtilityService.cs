using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace Saas.Infra.Core
{
    /// <summary>
    /// 提供日志工具：输出到控制台及 Serilog，并带有增强格式化。
    /// Provides logging utilities: writes to console and Serilog with enriched formatting.
    /// </summary>
    public class UtilityService
    {

        /// <summary>
        /// Formats a log message with timestamp and level, supports auto-indent for multiline.
        /// </summary>
        private static string FormatMessage(string message, LogEventLevel level)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var levelString = GetLevelString(level);
            var prefix = $"[{timestamp} {levelString}] ";
            var indent = new string(' ', prefix.Length);

            var lines = message.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var sb = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                if (i == 0)
                    sb.AppendLine(prefix + lines[i]);
                else
                    sb.AppendLine(indent + lines[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// 将消息以结构化格式输出到控制台和 Serilog。
        /// Writes the message with structured output to both the console and Serilog.
        /// </summary>
        /// <param name="message">日志内容，不能为空或仅包含空白。 / The message to log; cannot be <c>null</c> or whitespace.</param>
        /// <param name="level">日志级别，用于指定消息的严重性。 / The severity level to log the message with.</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="message"/> 为 <c>null</c> 时抛出。 / Thrown when <paramref name="message"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">当 <paramref name="message"/> 为空或仅包含空白时抛出。 / Thrown when <paramref name="message"/> is empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">当 <paramref name="level"/> 不是定义的 <see cref="LogEventLevel"/> 值时抛出。 / Thrown when <paramref name="level"/> is not a defined <see cref="LogEventLevel"/> value.</exception>
        public static void LogAndWriteLine(string message, LogEventLevel level = LogEventLevel.Information)
        {
            if (message is null)
                throw new ArgumentNullException(nameof(message));

            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message must not be empty or whitespace.", nameof(message));

            if (!Enum.IsDefined(typeof(LogEventLevel), level))
                throw new ArgumentOutOfRangeException(nameof(level), level, "Invalid LogEventLevel value.");

            var formattedMessage = FormatMessage(message, level);

            // Output to console with color
            Console.ForegroundColor = GetConsoleColor(level);
            Console.WriteLine(formattedMessage);
            Console.ResetColor();

            // Output to Serilog (remove formatting for log files)
            LogToSerilog(message, level);
        }


        private static ConsoleColor GetConsoleColor(LogEventLevel level)
        {
            return level switch
            {
                LogEventLevel.Verbose => ConsoleColor.DarkGray,
                LogEventLevel.Debug => ConsoleColor.Gray,
                LogEventLevel.Information => ConsoleColor.Cyan,
                LogEventLevel.Warning => ConsoleColor.Yellow,
                LogEventLevel.Error => ConsoleColor.Red,
                LogEventLevel.Fatal => ConsoleColor.DarkRed,
                _ => ConsoleColor.White
            };
        }

        private static void LogToSerilog(string message, LogEventLevel level)
        {
            switch (level)
            {
                case LogEventLevel.Verbose:
                    Log.Verbose(message);
                    break;
                case LogEventLevel.Debug:
                    Log.Debug(message);
                    break;
                case LogEventLevel.Information:
                    Log.Information(message);
                    break;
                case LogEventLevel.Warning:
                    Log.Warning(message);
                    break;
                case LogEventLevel.Error:
                    Log.Error(message);
                    break;
                case LogEventLevel.Fatal:
                    Log.Fatal(message);
                    break;
            }
        }


        private static string GetLevelString(LogEventLevel level)
        {
            return level switch
            {
                LogEventLevel.Verbose => "VRB",
                LogEventLevel.Debug => "DBG",
                LogEventLevel.Information => "INF",
                LogEventLevel.Warning => "WRN",
                LogEventLevel.Error => "ERR",
                LogEventLevel.Fatal => "FTL",
                _ => "UNK"
            };
        }


    }
}
