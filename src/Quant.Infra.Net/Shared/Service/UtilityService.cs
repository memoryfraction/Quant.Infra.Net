using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Futures;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.Analysis;
using Python.Runtime;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Shared.Service
{
    public class UtilityService
    {
        private const int MessageIndent = 15;
        private const int MaxLineWidth = 80;
        private static readonly object _orderLogLock = new();
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
        /// Enhanced logging with structured output to both console and Serilog
        /// </summary>
        public static void LogAndWriteLine(string message, LogEventLevel level = LogEventLevel.Information)
        {
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



        public static KlineInterval ConvertToKlineInterval(ResolutionLevel resolutionLevel)
        {
            return resolutionLevel switch
            {
                ResolutionLevel.Tick => KlineInterval.OneSecond,           // Assuming "Tick" maps to 1 second
                ResolutionLevel.Second => KlineInterval.OneSecond,
                ResolutionLevel.Minute => KlineInterval.OneMinute,
                ResolutionLevel.Hourly => KlineInterval.OneHour,
                ResolutionLevel.Daily => KlineInterval.OneDay,
                ResolutionLevel.Weekly => KlineInterval.OneWeek,
                ResolutionLevel.Monthly => KlineInterval.OneMonth,
                _ => throw new ArgumentOutOfRangeException(nameof(resolutionLevel), $"Unsupported resolution level: {resolutionLevel}")
            };
        }


        /// <summary>
        /// 结构化输出
        /// </summary>
        /// <param name="dataContent"></param>
        /// <param name="errors"></param>
        /// <returns></returns>
        public static string GenerateMessage(string dataContent, IEnumerable<string> errors = null)
        {
            var sb = new System.Text.StringBuilder();

            // 不再显示时间;防止日志重复

            // Add the data content in the middle
            sb.AppendLine(dataContent);

            // Add errors at the end if any
            if (errors != null && errors.Any() && errors.FirstOrDefault() != null)
            {
                sb.AppendLine();  // Add a blank line before errors section
                sb.AppendLine("Errors:");
                foreach (var error in errors)
                {
                    if(!string.IsNullOrEmpty(error))
                        sb.AppendLine($"- {error}");
                }
            }
            // Add a separator after each complete log message
            sb.AppendLine("------");
            return sb.ToString();
        }


        /// <summary>
        /// 生成日志, 包含以下信息: UsdtBalance， HoldingPositions (不同symbol的持仓数量)， UnRealizedProfit， UnRealizedProfitRate
        /// </summary>
        /// <returns></returns>
        public static string GenerateBinanceAccountSnapShotMessage(decimal usdBalance, IEnumerable<BinancePositionDetailsUsdt> positions)
        {
            // 初始化统计变量
            decimal totalUnRealizedProfit = 0;
            decimal totalMarketValue = usdBalance;
            decimal totalCostBase = 0;
            var holdingSymbols = new HashSet<string>();

            // 定义稳定币的集合
            var stableCoins = new HashSet<string> { "USDT", "USDC", "DAI" };

            // 遍历所有持仓
            foreach (var position in positions)
            {
                // 如果symbol是稳定币，直接将数量加入市场价值
                if (stableCoins.Contains(position.Symbol.ToUpper()))
                {
                    totalMarketValue += position.Quantity;
                    continue;
                }

                // 计算当前市场价值 = MarkPrice * Quantity
                decimal marketValue = position.MarkPrice * position.Quantity;

                // 计算成本基数 = EntryPrice * Quantity
                decimal costBase = position.EntryPrice * position.Quantity;

                // 计算总市场价值和总成本基数
                totalMarketValue += marketValue;
                totalCostBase += costBase;

                // 计算未实现利润
                totalUnRealizedProfit += position.UnrealizedPnl;

                // 统计持有的symbol
                if (position.Quantity != 0)
                {
                    holdingSymbols.Add(position.Symbol); // 通过symbol来判断持仓
                }
            }

            // 计算未实现利润率 = (总市场价值 - 总成本基数) / 总成本基数
            decimal unRealizedProfitRate = totalCostBase != 0 ? (totalMarketValue - totalCostBase) / totalCostBase : 0;

            // 判断是否有持仓符号并生成日志信息
            var holdingSymbolsMessage = holdingSymbols.Count > 0
                ? $"Holding Symbols: {holdingSymbols.Count} ({string.Join(", ", holdingSymbols)})\n"
                : "Holding Symbols: 0\n";

            // 生成最终日志信息
            var message = $"Account Snapshot\n" +
                          $"Total Market Value: {totalMarketValue}\n" +
                          holdingSymbolsMessage +  // 使用生成的持仓符号信息
                          $"Unrealized Profit: {totalUnRealizedProfit}\n" +
                          $"Unrealized Profit Rate: {unRealizedProfitRate:P2}";

            return message;
        }

        /// <summary>
        /// 生成日志, 包含以下信息: UsdtBalance， HoldingPositions (不同symbol的持仓数量)， UnRealizedProfit， UnRealizedProfitRate
        /// </summary>
        /// <returns></returns>
        public static string GenerateAlpacaAccountSnapShotMessage(decimal usdBalance, IEnumerable<BinancePositionDetailsUsdt> positions)
        {
            // 初始化统计变量
            decimal totalUnRealizedProfit = 0;
            decimal totalMarketValue = usdBalance;
            decimal totalCostBase = 0;
            var holdingSymbols = new HashSet<string>();

            // 定义稳定币的集合
            var stableCoins = new HashSet<string> { "USDT", "USDC", "DAI" };

            // 遍历所有持仓
            foreach (var position in positions)
            {
                // 如果symbol是稳定币，直接将数量加入市场价值
                if (stableCoins.Contains(position.Symbol.ToUpper()))
                {
                    totalMarketValue += position.Quantity;
                    continue;
                }

                // 计算当前市场价值 = MarkPrice * Quantity
                decimal marketValue = position.MarkPrice * position.Quantity;

                // 计算成本基数 = EntryPrice * Quantity
                decimal costBase = position.EntryPrice * position.Quantity;

                // 计算总市场价值和总成本基数
                totalMarketValue += marketValue;
                totalCostBase += costBase;

                // 计算未实现利润
                totalUnRealizedProfit += position.UnrealizedPnl;

                // 统计持有的symbol
                if (position.Quantity != 0)
                {
                    holdingSymbols.Add(position.Symbol); // 通过symbol来判断持仓
                }
            }

            // 计算未实现利润率 = (总市场价值 - 总成本基数) / 总成本基数
            decimal unRealizedProfitRate = totalCostBase != 0 ? (totalMarketValue - totalCostBase) / totalCostBase : 0;

            // 判断是否有持仓符号并生成日志信息
            var holdingSymbolsMessage = holdingSymbols.Count > 0
                ? $"Holding Symbols: {holdingSymbols.Count} ({string.Join(", ", holdingSymbols)})\n"
                : "Holding Symbols: 0\n";

            // 生成最终日志信息
            var message = $"Account Snapshot\n" +
                          $"Total Market Value: {totalMarketValue}\n" +
                          holdingSymbolsMessage +  // 使用生成的持仓符号信息
                          $"Unrealized Profit: {totalUnRealizedProfit}\n" +
                          $"Unrealized Profit Rate: {unRealizedProfitRate:P2}";

            return message;
        }

        /// <summary>
        /// 调整时间到下一个工作日，如果为周六或周日，则顺延到下周一。
        /// Adjusts the time to the next weekday; if it's Saturday or Sunday, moves to the following Monday.
        /// </summary>
        /// <param name="dateTime">要调整的日期时间 / The DateTime to adjust.</param>
        /// <returns>调整后的日期时间 / The adjusted DateTime.</returns>
        public static DateTime AdjustToNextWeekday(DateTime dateTime)
        {
            if (dateTime.DayOfWeek == DayOfWeek.Saturday)
            {
                return dateTime.AddDays(2); // 跳过周六到周一 / Skip Saturday to Monday
            }
            else if (dateTime.DayOfWeek == DayOfWeek.Sunday)
            {
                return dateTime.AddDays(1); // 跳过周日到周一 / Skip Sunday to Monday
            }
            return dateTime;
        }

        /// <summary>
        /// 获取指定分辨率级别的时间间隔。
        /// Gets the time interval for the specified resolution level.
        /// </summary>
        /// <param name="resolutionLevel">分辨率级别 / The resolution level.</param>
        /// <returns>时间间隔 / The time interval.</returns>
        public static TimeSpan GetInterval(ResolutionLevel resolutionLevel = ResolutionLevel.Daily)
        {
            switch (resolutionLevel)
            {
                case ResolutionLevel.Tick:
                    return TimeSpan.FromSeconds(1); // 假设每秒更新一次 / Assuming tick level updates every second
                case ResolutionLevel.Second:
                    return TimeSpan.FromSeconds(1);

                case ResolutionLevel.Minute:
                    return TimeSpan.FromMinutes(1);

                case ResolutionLevel.Hourly:
                    return TimeSpan.FromHours(1);

                case ResolutionLevel.Daily:
                    return TimeSpan.FromDays(1);

                case ResolutionLevel.Weekly:
                    return TimeSpan.FromDays(7);

                case ResolutionLevel.Other:
                default:
                    return TimeSpan.FromDays(1); // 默认返回每日 / Default to daily if not specified
            }
        }

        /// <summary>
        /// 异步检查路径是否存在，如果不存在则创建目录。
        /// Asynchronously checks if the path exists; if not, creates the directory.
        /// </summary>
        /// <param name="fullPathFilename">完整路径文件名 / Full path filename.</param>
        public static async Task IsPathExistAsync(string fullPathFilename)
        {
            // 检查入参有效性 / Check parameter validity
            if (string.IsNullOrEmpty(fullPathFilename))
                throw new ArgumentNullException($"Invalid parameter: {fullPathFilename}");

            // 从完整路径中获取目录路径 / Get directory path from the full path
            var directoryPath = Path.GetDirectoryName(fullPathFilename);
            if (directoryPath == null)
            {
                throw new ArgumentException("Invalid path");
            }

            // 检查文件夹是否存在 / Check if the directory exists
            if (!Directory.Exists(directoryPath))
            {
                try
                {
                    // 异步创建文件夹 / Asynchronously create the directory
                    await Task.Run(() => Directory.CreateDirectory(directoryPath));
                    Console.WriteLine("Folder created: " + directoryPath);
                }
                catch (Exception ex)
                {
                    // 处理可能出现的异常（例如权限问题）/ Handle possible exceptions (e.g., permission issues)
                    Console.WriteLine("An error occurred: " + ex.Message);
                    throw;
                }
            }
        }

        /// <summary>
        /// 调用Binance，根据Sumbol获取历史数据，存到指定路径的CSV文件。
        /// Calls Binance to get historical data based on symbol and saves it to a specified CSV file.
        /// </summary>
        /// <param name="symbol">交易对，如 BTCUSDT / The trading pair, e.g., BTCUSDT.</param>
        /// <param name="interval">K线间隔 / The Kline interval.</param>
        /// <param name="startDt">开始时间 / The start date.</param>
        /// <param name="endDt">结束时间 / The end date.</param>
        /// <param name="fullPathFileName">完整路径文件名 / The full path filename.</param>
        /// <param name="overWrite">是否覆盖现有文件 / Whether to overwrite the existing file.</param>
        /// <returns></returns>
        public static async Task SaveOhlcvsToCsv(string symbol, Binance.Net.Enums.KlineInterval interval, DateTime startDt, DateTime endDt, string fullPathFileName, bool overWrite = true)
        {
            if (string.IsNullOrEmpty(symbol))
                throw new ArgumentNullException(symbol);

            if (string.IsNullOrEmpty(fullPathFileName))
                throw new ArgumentNullException(fullPathFileName);

            // 确保路径存在 / Ensure the path exists
            var directory = Path.GetDirectoryName(fullPathFileName);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (overWrite == true)
            {
                if (File.Exists(fullPathFileName))
                    File.Delete(fullPathFileName);
            }

            using (var client = new BinanceRestClient())
            {
                var klinesResult = await client.SpotApi.ExchangeData.GetKlinesAsync(symbol, interval, startDt, endDt); // 获取历史K线数据 / Get historical Kline data

                // 使用 CsvHelper 保存 klinesResult 到 fullPathFileName / Save klinesResult to fullPathFileName using CsvHelper
                if (klinesResult.Success)
                {
                    using (var writer = new StreamWriter(fullPathFileName))
                    using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
                    {
                        // 写入标题行 / Write header
                        csv.WriteHeader<Ohlcv>();
                        csv.NextRecord();

                        foreach (var line in klinesResult.Data)
                        {
                            csv.WriteRecord(new Ohlcv
                            {
                                OpenDateTime = line.OpenTime,
                                Open = line.OpenPrice,
                                High = line.HighPrice,
                                Low = line.LowPrice,
                                Close = line.ClosePrice,
                                Volume = line.Volume
                            });
                            csv.NextRecord();
                        }
                    }
                    Console.WriteLine($"Klines data saved successfully for {symbol}.");
                }
                else
                {
                    Console.WriteLine($"Error: {klinesResult.Error}");
                }
            }
        }

        /// <summary>
        /// 执行Python方法，并返回PyObject。
        /// Executes a Python method and returns a PyObject.
        /// </summary>
        /// <param name="pythonFileName">Python文件名（无扩展名）/ The Python file name (without extension).</param>
        /// <param name="pythonFunctionName">Python函数名 / The Python function name.</param>
        /// <param name="pythonParameterObjs">Python参数对象的集合 / The collection of Python parameter objects.</param>
        /// <param name="pythonDirectories">Python文件所在的路径集合 / The collection of directories where the Python files are located.</param>
        /// <param name="venvPath">虚拟环境的路径 / The path to the virtual environment.</param>
        /// <param name="pythonDll">Python DLL文件名 / The Python DLL file name.</param>
        /// <returns>返回的PyObject / The returned PyObject.</returns>
        public static PyObject ExecutePython(
            string pythonFileName,
            string pythonFunctionName,
            IEnumerable<Object> pythonParameterObjs,
            IEnumerable<string> pythonDirectories,
            string venvPath = @"D:\ProgramData\PythonVirtualEnvs\pair_trading",
            string pythonDll = "python39.dll")
        {
            // 初始化变量 / Initialize variables
            var condaVenvHomePath = venvPath; // @"D:\ProgramData\PythonVirtualEnvs\pair_trading"
            var pythonDllFileName = pythonDll; // "python39.dll";
            var pythonFullPathFileName = Path.Combine(condaVenvHomePath, pythonDllFileName);
            PythonInfraModel infra = PythonNetInfra.GetPythonInfra(condaVenvHomePath, pythonDllFileName);
            if (Runtime.PythonDLL == null || Runtime.PythonDLL != pythonFullPathFileName)
            {
                Runtime.PythonDLL = infra.PythonDLL;
            }
            PythonEngine.PythonHome = infra.PythonHome;
            PythonEngine.PythonPath = infra.PythonPath;
            PythonEngine.Initialize();

            // 使用Python GIL / Use Python GIL
            using (Py.GIL())
            {
                try
                {
                    var code = "import sys;";
                    if (pythonDirectories != null)
                    {
                        var codeStringBuilder = new StringBuilder();
                        codeStringBuilder.Append(code);
                        foreach (var directory in pythonDirectories)
                        {
                            // e.g. directory is Path.Combine(pythonDirectory, "Backtest_Sp500");
                            var str = $"sys.path.append(r'{directory}');";
                            codeStringBuilder.Append(str);
                        }
                        code = codeStringBuilder.ToString();
                    }

                    PythonEngine.Exec(code); // 执行代码 / Execute code

                    // 执行指定的 Python 函数并获取结果 / Execute the specified Python function and get the result
                    var pyParams = new PyList();
                    foreach (var param in pythonParameterObjs)
                    {
                        pyParams.Append(param.ToPython());
                    }

                    var pyResult = PythonEngine.RunString($"{pythonFunctionName}(*{pyParams})"); // 运行Python函数 / Run the Python function
                    return pyResult; // 返回结果 / Return the result
                }
                catch (Exception ex)
                {
                    throw new Exception($"An error occurred while executing Python: {ex.Message}", ex);
                }
                finally
                {
                    // 清理环境 / Clean up the environment
                    PythonEngine.Shutdown();
                }
            }
        }

        /// <summary>
        /// 读取CSV文件，返回DataFrame
        /// Title Row: DateTime, Open, High, Low, Close, Volume
        /// </summary>
        /// <param name="fullPathFileName"></param>
        /// <returns></returns>
        public static DataFrame LoadCsvToDataFrame(string fullPathFileName)
        {
            var dateTimeColumn = new PrimitiveDataFrameColumn<DateTime>("DateTime");
            var closeColumn = new DoubleDataFrameColumn("Close");

            using (var reader = new StreamReader(fullPathFileName))
            {
                var csv = new CsvHelper.CsvReader(reader, CultureInfo.InvariantCulture);
                var records = csv.GetRecords<dynamic>();

                foreach (var record in records)
                {
                    var row = record as IDictionary<string, object>;
                    dateTimeColumn.Append(DateTime.Parse(row["DateTime"].ToString()));
                    closeColumn.Append(double.Parse(row["Close"].ToString()));
                }
            }

            var dataFrame = new DataFrame();
            dataFrame.Columns.Add(dateTimeColumn);
            dataFrame.Columns.Add(closeColumn);

            return dataFrame;
        }


        /// <summary>
        /// 读取Csv文件，返回Close列(List<double>)
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>返回Close列</returns>
        public static List<double> ReadCloseColFromCsv(string fullPathFileName)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            };

            using (var reader = new StreamReader(fullPathFileName))
            using (var csv = new CsvReader(reader, config))
            {
                // 读取 CSV 的表头
                csv.Read();
                csv.ReadHeader();

                // 创建一个 List<double> 来保存 Close 列的数据
                var closeValues = new List<double>();

                // 读取每一行数据
                while (csv.Read())
                {
                    // 获取 Close 列的数据并添加到列表中
                    var closeValue = csv.GetField<double>("Close");
                    closeValues.Add(closeValue);
                }

                return closeValues;
            }
        }
    }
}