using CsvHelper;
using CsvHelper.Configuration;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;
using Quant.Infra.Net.SourceData.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Quant.Infra.Net.SourceData.Service
{
    public class IOService
    {
        private readonly ResolutionConversionService _resolutionService;
        public IOService()
        {
            _resolutionService = new ResolutionConversionService();
        }

        /// <summary>
        /// 已知文件名和路径，使用csvHelper获取Ohlcvs
        /// </summary>
        /// <param name="fullPathFilename"></param>
        /// <returns></returns>
        public Ohlcvs ReadCsv(string fullPathFileName)
        {
            // 如果文件{fullPathFileName}不存在，则抛出异常；
            if (!File.Exists(fullPathFileName))
            {
                throw new FileNotFoundException($"The file {fullPathFileName} does not exist.");
            }

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            };

            var records = GetOhlcvs(fullPathFileName); // todo 把这一行手动读取， 不要用csvHelper的自动映射功能， 因为列名不匹配; csv数据是DateTime, Open, High, Low, Close, Volume
                                                       // Ohlcv是OpenDateTime, CloseDateTime, Open, High, Low, Close, Volume
            var ohlcvs = new Ohlcvs
            {
                ResolutionLevel = _resolutionService.GetResolutionLevel(records),
                OhlcvSet = new HashSet<Ohlcv>(records),
                FullPathFileName = fullPathFileName,
                Symbol  = Path.GetFileNameWithoutExtension(fullPathFileName),
                StartDateTimeUtc = records.Select(x => x.OpenDateTime).FirstOrDefault(),
                EndDateTimeUtc = records.Select(x => x.OpenDateTime).LastOrDefault()
            };
            return ohlcvs;
            
        }

        /// <summary>
        /// 读取文件，获取Ohlcv集合, 手动解析csv文件，更加灵活
        /// </summary>
        /// <param name="fullPathFileName"></param>
        /// <returns></returns>
        private IEnumerable<Ohlcv> GetOhlcvs(string fullPathFileName)
        {
            if (!File.Exists(fullPathFileName))
            {
                throw new FileNotFoundException($"The file {fullPathFileName} does not exist.");
            }
            var records = new List<Ohlcv>();
            using (var reader = new StreamReader(fullPathFileName))
            {
                // 1. 跳过Header行：DateTime,Open,High,Low,Close,Volume
                if (reader.Peek() >= 0)
                {
                    reader.ReadLine();
                }

                string line;
                var Separator = ',';
                // 1. 读取所有行，DateTime赋值给OpenDateTime
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] fields = line.Split(Separator);

                    // 确保字段数量足够 (至少6个)
                    if (fields.Length < 6) continue;

                    // 尝试解析日期时间 (索引 0)
                    if (!DateTime.TryParseExact(
                        fields[0].Trim(),
                        "yyyy-MM-dd HH:mm:ss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal,
                        out DateTime openDateTime))
                    {
                        continue;
                    }

                    // 尝试解析 OHLCV (索引 1 到 5)
                    if (!TryParseDecimal(fields[1], out decimal open) ||
                        !TryParseDecimal(fields[2], out decimal high) ||
                        !TryParseDecimal(fields[3], out decimal low) ||
                        !TryParseDecimal(fields[4], out decimal close) ||
                        !TryParseDecimal(fields[5], out decimal volume))
                    {
                        continue;
                    }

                    var ohlcv = new Ohlcv
                    {
                        // 赋值 OpenDateTime
                        OpenDateTime = openDateTime,
                        // CloseDateTime 将在步骤 3 中统一赋值
                        Open = open,
                        High = high,
                        Low = low,
                        Close = close,
                        Volume = volume,
                        Symbol = Path.GetFileNameWithoutExtension(fullPathFileName) // 假设 symbol 来自文件名
                    };
                    records.Add(ohlcv);
                }
            }

            if (!records.Any())
            {
                return Enumerable.Empty<Ohlcv>();
            }

            // 2. 获取resolution Level
            // 假设 _resolutionService.GetResolutionLevel 接受 List<Ohlcv> 并返回 ResolutionLevel
            var resolutionLevel = _resolutionService.GetResolutionLevel(records);

            // 3. 根据ResolutionLevel赋值CloseDateTime
            var timeSpan = UtilityService.ResolutionLevelToTimeSpan(resolutionLevel);

            foreach (var ohlcv in records)
            {
                ohlcv.CloseDateTime = ohlcv.OpenDateTime.Add(timeSpan);
            }

            return records;
        }

        // 辅助方法：安全解析 decimal (因为 Ohlcv 模型使用了 decimal)
        private bool TryParseDecimal(string input, out decimal result)
        {
            return decimal.TryParse(input.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }

        /// <summary>
        /// 根据要求读取给定的csv文件, 如果条件不符合，则返回null
        /// </summary>
        /// <param name="fullPathFileName">， 包括列：DateTime, Open, High, Low, Close, Volume</param>
        /// <param name="requiredStartDt"></param>
        /// <param name="requiredEndDt"></param>
        /// <param name="requiredResolutionLevel"></param>
        /// <returns></returns>
        public Ohlcvs ReadCsv(string fullPathFileName, DateTime requiredStartDt, DateTime requiredEndDt, ResolutionLevel requiredResolutionLevel)
        {
            // 如果文件{fullPathFileName}不存在，则抛出异常；
            if (!File.Exists(fullPathFileName))
            {
                throw new FileNotFoundException($"The file {fullPathFileName} does not exist.");
            }

            var ohlcvs = ReadCsv(fullPathFileName);

            // 判断 ohlcvs是否符合要求的requiredStartDt and requiredEndDt, 如果不符合， 则返回null;具体逻辑:
            // 1 OhlcvSet中最早的记录需要 <= requiredStartDt
            // 2 OhlcvSet中最晚的记录需要 >= requiredEndDt
            // 3 OhlcvSet的CanConvertResolution(currentResolution, requiredResolutionLevel) 需要等于True
            // 三者都符合才继续，否则返回null
            var earliestRecord = ohlcvs.OhlcvSet.Min(x => x.OpenDateTime);
            var latestRecord = ohlcvs.OhlcvSet.Max(x => x.OpenDateTime);
            var isEarlistRecordSatisfying = earliestRecord.Date <= requiredStartDt.Date;
            var isLatestRecordSatisfying = latestRecord.Date >= requiredEndDt.Date;
            var isResolutionLevelSatisfying = _resolutionService.CanConvertResolution(ohlcvs.ResolutionLevel, requiredResolutionLevel);
            if (isEarlistRecordSatisfying && isLatestRecordSatisfying && isResolutionLevelSatisfying)
            {
                var filteredRecords = ohlcvs.OhlcvSet.Where(x => x.OpenDateTime >= requiredStartDt && x.OpenDateTime <= requiredEndDt).ToList();

                // Convert resolution to requiredResolutionLevel
                var convertedOhlcvs = _resolutionService.ConvertResolution(filteredRecords, requiredResolutionLevel);

                // 根据fullPathFileName赋值convertedOhlcvs中的Symbol和FullPathFileName属性
                convertedOhlcvs.Symbol = Path.GetFileNameWithoutExtension(fullPathFileName);
                convertedOhlcvs.FullPathFileName = fullPathFileName;

                // Return
                return convertedOhlcvs;
            }
            else
                return null;
        }


        /// <summary>
        /// 读取文件，转化为Date, Value的组合;
        /// </summary>
        /// <param name="fullPathFileName"></param>
        /// <returns></returns>
        private TimeSeries GetTimeSeriesFromFullPathFileName(string fullPathFileName, DateTime startDt, DateTime endDt, ResolutionLevel resolution = ResolutionLevel.Hourly)
        {
            List<DateTime> dates = new List<DateTime>();
            List<double> values = new List<double>();

            var ohlcvs = ReadCsv(fullPathFileName, startDt, endDt, resolution);
            // 跳过第一行并遍历剩余行
            for (int i = 1; i < ohlcvs.OhlcvSet.Count; i++)
            {
                var dateTime = ohlcvs.OhlcvSet.ElementAt(i).OpenDateTime;
                var value = (double)ohlcvs.OhlcvSet.ElementAt(i).Close;
                if (dateTime >= startDt && dateTime <= endDt)
                {
                    dates.Add(dateTime);
                    values.Add(value);
                }
            }
            var timeSeries = new TimeSeries(dates, values);
            return timeSeries;
        }



        /// 读取文件CSV文件，获取TimeSeries
        public TimeSeries GetDiffTimeSeries(string fullPathFileName1, string fullPathFileName2, double slope, double intercept, DateTime startDt, DateTime endDt, ResolutionLevel resolution = ResolutionLevel.Hourly)
        {
            var timeSeries1 = GetTimeSeriesFromFullPathFileName(fullPathFileName1, startDt, endDt, resolution);
            var timeSeries2 = GetTimeSeriesFromFullPathFileName(fullPathFileName2, startDt, endDt, resolution);
            if (timeSeries1.TimeSeriesElements.Count != timeSeries2.TimeSeriesElements.Count)
                throw new Exception("timeSeries1, timeSeries2 length should be the same.");
            var timeSeries = new TimeSeries();
            for (int i = 0; i < timeSeries1.TimeSeriesElements.Count; i++)
            {
                var diff = timeSeries2.TimeSeriesElements[i].Value - slope * timeSeries1.TimeSeriesElements[i].Value - intercept;
                var elm = new TimeSeriesElement(timeSeries1.TimeSeriesElements[i].DateTime, diff);
                timeSeries.TimeSeriesElements.Add(elm);
            }
            return timeSeries;
        }


        public void WriteCsv(string fullPathFileName, IEnumerable<Ohlcv> ohlcvs)
        {
            var directory = Path.GetDirectoryName(fullPathFileName);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            };

            using (var writer = new StreamWriter(fullPathFileName))
            using (var csv = new CsvWriter(writer, config))
            {
                csv.WriteRecords(ohlcvs);
            }
        }

        /// <summary>
        /// 手动写入 Ohlcv 数据的 CSV 文件，允许自定义字段和标题。
        /// </summary>
        /// <param name="fullPathFileName">CSV 文件的完整路径和名称。</param>
        /// <param name="ohlcvs">要写入的 Ohlcv 记录集合。</param>
        public void WriteCsvManually(string fullPathFileName, IEnumerable<Ohlcv> ohlcvs)
        {
            // 检查参数有效性
            if(string.IsNullOrEmpty(fullPathFileName))
            {
                throw new ArgumentException("The file path cannot be null or empty.", nameof(fullPathFileName));
            }
            if(ohlcvs == null)
            {
                throw new ArgumentNullException(nameof(ohlcvs), "The Ohlcv collection cannot be null.");
            }

            // 如果文件夹不存在，则创建
            var directory = Path.GetDirectoryName(fullPathFileName);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // --- 定义要输出的字段及其格式 ---

            // 定义要输出的字段，它们的标题和提取函数。
            const string delimiter = ",";
            const string dateTimeFormat = "yyyy-MM-dd HH:mm:ss";

            var outputFields = new List<(string Title, Func<Ohlcv, object> Selector)>
            {
                // 注意：由于 BasicOhlcv 中是 OpenDateTime 和 CloseDateTime，
                // 这里我输出了 OpenDateTime，标题为 "DateTime"。
                ("DateTime", x => x.OpenDateTime.ToString(dateTimeFormat, CultureInfo.InvariantCulture)),
                ("Open", x => x.Open.ToString(CultureInfo.InvariantCulture)),
                ("High", x => x.High.ToString(CultureInfo.InvariantCulture)),
                ("Low", x => x.Low.ToString(CultureInfo.InvariantCulture)),
                ("Close", x => x.Close.ToString(CultureInfo.InvariantCulture)),
                ("Volume", x => x.Volume.ToString(CultureInfo.InvariantCulture))
            };

            // 遍历写入文件内容
            try
            {
                // 使用 StreamWriter 写入文件，覆盖现有内容
                using (var writer = new StreamWriter(fullPathFileName, false, Encoding.UTF8))
                {
                    // 1. 写入头部 (Header)
                    // 从 outputFields 中获取所有 Title 并用分隔符连接
                    string header = string.Join(delimiter, outputFields.Select(f => f.Title));
                    writer.WriteLine(header);


                    // 2. 遍历写入数据行 (Data Rows)
                    foreach (var record in ohlcvs)
                    {
                        var lineBuilder = new StringBuilder();

                        for (int i = 0; i < outputFields.Count; i++)
                        {
                            // 使用 Selector 提取值，并确保转换为字符串
                            // 注意：这里假设 numeric/date-time fields produce strings that don't need quoting.
                            string value = outputFields[i].Selector(record).ToString();

                            // 简单的 CSV 规范化处理 (防止值中包含分隔符)
                            if (value.Contains(delimiter) || value.Contains("\"") || value.Contains("\n"))
                            {
                                // 转义内部的双引号，并用双引号包围
                                value = "\"" + value.Replace("\"", "\"\"") + "\"";
                            }

                            lineBuilder.Append(value);

                            // 添加分隔符 (最后一个字段除外)
                            if (i < outputFields.Count - 1)
                            {
                                lineBuilder.Append(delimiter);
                            }
                        }

                        writer.WriteLine(lineBuilder.ToString());
                    }
                }
            }
            catch (IOException ex)
            {
                // 捕获和处理文件I/O错误
                System.Console.WriteLine($"An I/O error occurred while writing to file {fullPathFileName}: {ex.Message}");
                throw;
            }
        }
    }

}
