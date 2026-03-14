using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
		/// 使用时间戳和日志级别格式化日志消息，支持多行自动缩进。
		/// Formats a log message with timestamp and level, supports auto-indent for multiline.
		/// </summary>
		/// <param name="message">要格式化的日志消息。 / The message to format.</param>
		/// <param name="level">日志级别。 / The log level.</param>
		/// <returns>格式化后的日志消息。 / The formatted log message.</returns>
		private static string FormatMessage(string message, LogEventLevel level)
		{
			if (string.IsNullOrWhiteSpace(message))
				throw new ArgumentException("Message must not be empty or whitespace.", nameof(message));

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
		/// <param name="message">日志内容，不能为空或仅包含空白。 / The message to log; cannot be null or whitespace.</param>
		/// <param name="level">日志级别，用于指定消息的严重性。 / The severity level to log the message with.</param>
		/// <exception cref="ArgumentNullException">当 <paramref name="message"/> 为 null 时抛出。 / Thrown when <paramref name="message"/> is null.</exception>
		/// <exception cref="ArgumentException">当 <paramref name="message"/> 为空或仅包含空白时抛出。 / Thrown when <paramref name="message"/> is empty or whitespace.</exception>
		/// <exception cref="ArgumentOutOfRangeException">当 <paramref name="level"/> 不是定义的 <see cref="LogEventLevel"/> 值时抛出。 / Thrown when <paramref name="level"/> is not a defined <see cref="LogEventLevel"/> value.</exception>
		public static void LogAndWriteLine(string message, LogEventLevel level = LogEventLevel.Information)
		{
			// Parameter validation
			if (message is null)
				throw new ArgumentNullException(nameof(message), "Message cannot be null.");

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

        /// <summary>
        /// 获取当前运行时的精确环境类型。
        /// Detects the current runtime environment accurately.
        /// </summary>
        /// <returns>RuntimeEnvironment 枚举值。</returns>
        public static RuntimeEnvironment GetCurrentEnvironment()
        {
            // 1. 检查 Azure Container Apps 特有环境变量 (优先级最高)
            // CONTAINER_APP_NAME 是 ACA 平台强制注入的
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONTAINER_APP_NAME")))
            {
                return RuntimeEnvironment.AzureContainerApps;
            }

            // 2. 检查是否在容器内运行 (无论本地还是云端)
            // DOTNET_RUNNING_IN_CONTAINER 是 .NET 官方镜像默认设置的
            bool isContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true"
                               || File.Exists("/.dockerenv");

            if (isContainer)
            {
                return RuntimeEnvironment.LocalContainer;
            }

            // 3. 检查操作系统平台
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return RuntimeEnvironment.LocalWindows;
            }

            return RuntimeEnvironment.OtherLinux;
        }

        /// <summary>
        /// 根据日志级别获取对应的控制台颜色。
        /// Gets the console color corresponding to the log level.
        /// </summary>
        /// <param name="level">日志级别。 / The log level.</param>
        /// <returns>对应的控制台颜色。 / The corresponding console color.</returns>
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

		/// <summary>
		/// 将消息输出到 Serilog。
		/// Writes the message to Serilog.
		/// </summary>
		/// <param name="message">要记录的消息。 / The message to log.</param>
		/// <param name="level">日志级别。 / The log level.</param>
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

		/// <summary>
		/// 获取日志级别的字符串表示（3个字符的缩写）。
		/// Gets the string representation of the log level (3-character abbreviation).
		/// </summary>
		/// <param name="level">日志级别。 / The log level.</param>
		/// <returns>日志级别的缩写字符串。 / The abbreviated string representation of the log level.</returns>
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
