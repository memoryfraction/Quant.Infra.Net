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

            using (var reader = new StreamReader(fullPathFileName))
            using (var csv = new CsvReader(reader, config))
            {
                var records = csv.GetRecords<Ohlcv>().ToList();
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
    }
}
