using Binance.Net.Clients;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Data.Analysis;
using Python.Runtime;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
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

        /// <summary>
        /// 调整时间到下一个工作日，如果为周六或周日，则顺延到下周一
        /// Adjusts the time to the next weekday; if it's Saturday or Sunday, moves to the following Monday.
        /// </summary>
        /// <param name="dateTime">要调整的日期时间</param>
        /// <returns>调整后的日期时间</returns>
        public static DateTime AdjustToNextWeekday(DateTime dateTime)
        {
            if (dateTime.DayOfWeek == DayOfWeek.Saturday)
            {
                return dateTime.AddDays(2); // 跳过周六到周一
            }
            else if (dateTime.DayOfWeek == DayOfWeek.Sunday)
            {
                return dateTime.AddDays(1); // 跳过周日到周一
            }
            return dateTime;
        }

        public static TimeSpan GetInterval(ResolutionLevel resolutionLevel = ResolutionLevel.Daily)
        {
            switch (resolutionLevel)
            {
                case ResolutionLevel.Tick:
                    return TimeSpan.FromSeconds(1); // Assuming tick level updates every second
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
                    return TimeSpan.FromDays(1); // Default to daily if not specified
            }
        }
        public static async Task IsPathExistAsync(string fullPathFilename)
        {
            // 检查入参有效性
            if (string.IsNullOrEmpty(fullPathFilename))
                throw new ArgumentNullException($"Invalid parameter:{fullPathFilename}");

            // 从完整路径中获取目录路径
            var directoryPath = Path.GetDirectoryName(fullPathFilename);
            if (directoryPath == null)
            {
                throw new ArgumentException("Invalid path");
            }

            // 检查文件夹是否存在
            if (!Directory.Exists(directoryPath))
            {
                try
                {
                    // 异步创建文件夹
                    await Task.Run(() => Directory.CreateDirectory(directoryPath));
                    Console.WriteLine("Folder created: " + directoryPath);
                }
                catch (Exception ex)
                {
                    // 处理可能出现的异常（例如权限问题）
                    Console.WriteLine("An error occurred: " + ex.Message);
                    throw;
                }
            }
        }


        /// <summary>
        /// 调用Binance，根据Sumbol获取历史数据，存到指定路径的csv文件
        /// </summary>
        /// <param name="symbol">BTCUSDT</param>
        /// <param name="interval"></param>
        /// <param name="startDt"></param>
        /// <param name="endDt"></param>
        /// <param name="fullPathFileName"></param>
        /// <returns></returns>
        public static async Task SaveOhlcvsToCsv(string symbol, Binance.Net.Enums.KlineInterval interval, DateTime startDt, DateTime endDt, string fullPathFileName,bool overWrite = true)
        {
            if(string.IsNullOrEmpty(symbol))
                throw new ArgumentNullException(symbol);

            if (string.IsNullOrEmpty(fullPathFileName))
                throw new ArgumentNullException(fullPathFileName);

            // 确保路径存在
            var directory = Path.GetDirectoryName(fullPathFileName);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if(overWrite == true)
            { 
                if (File.Exists(fullPathFileName))
                    File.Delete(fullPathFileName);
            }

            using (var client = new BinanceRestClient())
            {
                var klinesResult = await client.SpotApi.ExchangeData.GetKlinesAsync(symbol, interval, startDt, endDt); // 获取历史K线数据

                // Save klinesResult to fullPathFileName using csvHelper
                if (klinesResult.Success)
                {
                    using (var writer = new StreamWriter(fullPathFileName))
                    using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
                    {
                        // Write header
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
        /// 执行Python方法，并返回PyObject
        /// </summary>
        /// <param name="venvPath">虚拟环境的路径，比如：@"D:\ProgramData\PythonVirtualEnvs\pair_trading"</param>
        /// <param name="pythonDll">比如:  "python39.dll"</param>
        /// <param name="directories">Python文件所处的路径，比如："Python文件夹"</param>
        /// <param name="pythonFileName">Without Extension</param>
        /// <param name="pythonFunctionName"></param>
        public static PyObject ExecutePython(
            string pythonFileName, 
            string pythonFunctionName,
            IEnumerable<Object> pythonParameterObjs,
            IEnumerable<string> pythonDirectories,
            string venvPath = @"D:\ProgramData\PythonVirtualEnvs\pair_trading",
            string pythonDll = "python39.dll")
        {
            // 初始化变量
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

            // 使用Python GIL
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
                    PythonEngine.Exec(code);
                    var pythonScript = Py.Import(pythonFileName);

                    var pythonObjectArray = pythonParameterObjs.Select(x => x.ToPython()).ToArray();
                    PyObject response = pythonScript.InvokeMethod(pythonFunctionName, pythonObjectArray);
                    return response;
                }
                catch (PythonException ex)
                {
                    Console.WriteLine($"Error importing sys or adding path: {ex.Message}");
                    throw;
                }
            }
        }


        /// <summary>
        /// Async 版本的ExecutePython()
        /// </summary>
        /// <param name="pythonFileName"></param>
        /// <param name="pythonFunctionName"></param>
        /// <param name="pythonParameterObjs"></param>
        /// <param name="pythonDirectories"></param>
        /// <param name="venvPath"></param>
        /// <param name="pythonDll"></param>
        /// <returns></returns>
        public static async Task<PyObject> ExecutePythonAsync(string pythonFileName,
            string pythonFunctionName,
            IEnumerable<Object> pythonParameterObjs,
            IEnumerable<string> pythonDirectories,
            string venvPath = @"D:\ProgramData\PythonVirtualEnvs\pair_trading",
            string pythonDll = "python39.dll")
        {
            return await Task.Run(() =>
            {
                return ExecutePython(
                    pythonFileName,
                    pythonFunctionName,
                    pythonParameterObjs,
                    pythonDirectories,
                    venvPath,
                    pythonDll
                );
            });
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
    }
}
