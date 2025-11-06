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

namespace Quant.Infra.Net.SourceData.Service
{
    public class IOService
    {
        private readonly ResolutionConversionService _resolutionService;

        // ----------------------------------------------------
        // 关键辅助类：私有嵌套类，用于在运行时配置 CsvHelper 映射
        // ----------------------------------------------------
        /// <summary>
        /// 继承 ClassMap<T>，通过 Action 委托在运行时配置映射
        /// </summary>
        private class ActionClassMap<T> : ClassMap<T> where T : new()
        {
            public ActionClassMap(Action<ClassMap<T>> mapAction)
            {
                mapAction(this);
            }
        }

        public IOService()
        {
            _resolutionService = new ResolutionConversionService();
        }

        /// <summary>
        /// 已知文件名和路径，使用csvHelper获取Ohlcvs
        /// </summary>
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
                // **核心修改 1: 禁用头部验证，以忽略 CloseDateTime 和旧的 OpenDateTime 列名**
                HeaderValidated = null
            };

            using (var reader = new StreamReader(fullPathFileName))
            using (var csv = new CsvReader(reader, config))
            {
                // **核心修改 2: 注册运行时映射配置，以覆盖 Ohlcv 类上的 [Name] 属性**
                csv.Context.RegisterClassMap(new ActionClassMap<Ohlcv>(m =>
                {
                    // 1. 映射 OpenDateTime：将 CSV 中的 "DateTime" 列映射到 Ohlcv 的 OpenDateTime 属性
                    m.Map(x => x.OpenDateTime).Name("DateTime");

                    // 2. 显式忽略 CloseDateTime：覆盖 Ohlcv 类上的 [Name("CloseDateTime")] 注解
                    m.Map(x => x.CloseDateTime).Ignore();

                    // 3. 映射其他列 (覆盖 [Index] 属性)
                    m.Map(x => x.Open).Name("Open");
                    m.Map(x => x.High).Name("High");
                    m.Map(x => x.Low).Name("Low");
                    m.Map(x => x.Close).Name("Close");
                    m.Map(x => x.Volume).Name("Volume");

                    // 忽略 Symbol
                    m.Map(x => x.Symbol).Ignore();
                }));

                var records = csv.GetRecords<Ohlcv>().ToList();
                var ohlcvs = new Ohlcvs
                {
                    ResolutionLevel = _resolutionService.GetResolutionLevel(records),
                    OhlcvSet = new HashSet<Ohlcv>(records),
                    FullPathFileName = fullPathFileName,
                    Symbol = Path.GetFileNameWithoutExtension(fullPathFileName),
                    StartDateTimeUtc = records.Select(x => x.OpenDateTime).FirstOrDefault(),
                    EndDateTimeUtc = records.Select(x => x.OpenDateTime).LastOrDefault()
                };
                return ohlcvs;
            }
        }

        /// <summary>
        /// 根据要求读取给定的csv文件, 如果条件不符合，则返回null
        /// </summary>
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
                // 手动写入头部记录 (Header)
                csv.WriteField("DateTime"); // <-- 写入新的列名
                csv.WriteField("Open");
                csv.WriteField("High");
                csv.WriteField("Low");
                csv.WriteField("Close");
                csv.WriteField("Volume");
                csv.NextRecord(); // 结束头部行

                // 手动写入数据记录 (Records)
                foreach (var record in ohlcvs)
                {
                    csv.WriteField(record.OpenDateTime); // <-- 映射到新的 "DateTime"
                    csv.WriteField(record.Open);
                    csv.WriteField(record.High);
                    csv.WriteField(record.Low);
                    csv.WriteField(record.Close);
                    csv.WriteField(record.Volume);
                    csv.NextRecord(); // 结束数据行
                }
            }
        }
    }
}