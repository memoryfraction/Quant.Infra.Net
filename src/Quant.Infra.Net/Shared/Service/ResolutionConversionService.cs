using Quant.Infra.Net.Shared.Extension;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Quant.Infra.Net.Shared.Service
{
    public class ResolutionConversionService
    {
        /// <summary>
        /// 给定ohlcvList,求：OhlcvsResolution
        /// </summary>
        /// <param name="ohlcvs"></param>
        /// <returns></returns>
        public ResolutionLevel GetResolutionLevel(IEnumerable<Ohlcv> ohlcvList)
        {
            if (ohlcvList == null || !ohlcvList.Any())
                return ResolutionLevel.Other;

            var intervals = ohlcvList
                .OrderBy(ohlcv => ohlcv.OpenDateTime)
                .Select((ohlcv, index) => index == 0 ? TimeSpan.Zero : ohlcv.OpenDateTime - ohlcvList.ElementAt(index - 1).OpenDateTime)
                .Where(interval => interval != TimeSpan.Zero)
                .ToList();

            if (!intervals.Any())
                return ResolutionLevel.Other;

            // 找到时间间隔的众数
            var mostCommonInterval = intervals
                .GroupBy(interval => interval)
                .OrderByDescending(group => group.Count())
                .First().Key;

            // 判断众数时间间隔并返回相应的 ResolutionLevel
            if (mostCommonInterval <= TimeSpan.FromSeconds(1))
                return ResolutionLevel.Tick;
            else if (mostCommonInterval < TimeSpan.FromSeconds(60))
                return ResolutionLevel.Second;
            else if (mostCommonInterval < TimeSpan.FromMinutes(60))
                return ResolutionLevel.Minute;
            else if (mostCommonInterval < TimeSpan.FromHours(24))
                return ResolutionLevel.Hourly;
            else if (mostCommonInterval < TimeSpan.FromDays(7))
                return ResolutionLevel.Daily;
            else if (mostCommonInterval < TimeSpan.FromDays(30))
                return ResolutionLevel.Weekly;
            else if (mostCommonInterval < TimeSpan.FromDays(365))
                return ResolutionLevel.Monthly;

            return ResolutionLevel.Other;
        }

        /// <summary>
        /// 转化records的精度，可以从分钟级转化为小时级。 如果转化失败，返回null;
        /// </summary>
        /// <param name="records"></param>
        /// <param name="requiredResolutionLevel"></param>
        /// <returns></returns>
        public Ohlcvs ConvertResolution(IEnumerable<Ohlcv> records, ResolutionLevel requiredResolutionLevel)
        {
            var ohlcvs = new Ohlcvs();

            // 获取当前 records 的分辨率
            var currentResolution = GetResolution(records);

            // 判断当前分辨率是否可以转换为目标分辨率
            if (!CanConvertResolution(currentResolution, requiredResolutionLevel))
            {
                return null;
            }

            // 实现分辨率转换逻辑
            ohlcvs.ResolutionLevel = requiredResolutionLevel;
            ohlcvs.OhlcvSet = ConvertRecords(records, requiredResolutionLevel);

            return ohlcvs;
        }

        public bool CanConvertResolution(ResolutionLevel currentResolution, ResolutionLevel requiredResolution)
        {
            // 假设可以从低分辨率转换为高分辨率，但不能反向转换
            return currentResolution <= requiredResolution;
        }

        private ResolutionLevel GetResolution(IEnumerable<Ohlcv> records)
        {
            // 假设 records 按时间顺序排列，计算相邻记录的时间差来确定分辨率
            var list = records.ToList();
            if (list.Count < 2)
            {
                throw new ArgumentException("Records list must contain at least two elements to determine resolution.");
            }

            var timeDifference = list[1].OpenDateTime - list[0].OpenDateTime;

            // 根据时间差返回相应的分辨率
            if (timeDifference.TotalSeconds < 1)
            {
                return ResolutionLevel.Tick;
            }
            else if (timeDifference.TotalSeconds == 1)
            {
                return ResolutionLevel.Second;
            }
            else if (timeDifference.TotalMinutes == 1)
            {
                return ResolutionLevel.Minute;
            }
            else if (timeDifference.TotalHours == 1)
            {
                return ResolutionLevel.Hourly;
            }
            else if (timeDifference.TotalDays == 1)
            {
                return ResolutionLevel.Daily;
            }
            else if (timeDifference.TotalDays == 7)
            {
                return ResolutionLevel.Weekly;
            }
            else if (timeDifference.TotalDays >= 28 && timeDifference.TotalDays <= 31)
            {
                return ResolutionLevel.Monthly;
            }
            else
            {
                return ResolutionLevel.Other;
            }
        }

        private HashSet<Ohlcv> ConvertRecords(IEnumerable<Ohlcv> records, ResolutionLevel requiredResolution)
        {
            var convertedRecords = new HashSet<Ohlcv>();

            switch (requiredResolution)
            {
                case ResolutionLevel.Tick:
                    // Tick级别不需要转换，直接返回原始记录
                    convertedRecords = new HashSet<Ohlcv>(records);
                    break;

                case ResolutionLevel.Second:
                    // 按秒聚合
                    var groupedBySecond = records.GroupBy(r => new DateTime(r.OpenDateTime.Year, r.OpenDateTime.Month, r.OpenDateTime.Day, r.OpenDateTime.Hour, r.OpenDateTime.Minute, r.OpenDateTime.Second));
                    foreach (var group in groupedBySecond)
                    {
                        var ohlcv = new Ohlcv
                        {
                            OpenDateTime = group.Key,
                            Open = group.First().Open,
                            High = group.Max(r => r.High),
                            Low = group.Min(r => r.Low),
                            Close = group.Last().Close,
                            Volume = group.Sum(r => r.Volume)
                        };
                        convertedRecords.Add(ohlcv);
                    }
                    break;

                case ResolutionLevel.Minute:
                    // 按分钟聚合
                    var groupedByMinute = records.GroupBy(r => new DateTime(r.OpenDateTime.Year, r.OpenDateTime.Month, r.OpenDateTime.Day, r.OpenDateTime.Hour, r.OpenDateTime.Minute, 0));
                    foreach (var group in groupedByMinute)
                    {
                        var ohlcv = new Ohlcv
                        {
                            OpenDateTime = group.Key,
                            Open = group.First().Open,
                            High = group.Max(r => r.High),
                            Low = group.Min(r => r.Low),
                            Close = group.Last().Close,
                            Volume = group.Sum(r => r.Volume)
                        };
                        convertedRecords.Add(ohlcv);
                    }
                    break;

                case ResolutionLevel.Hourly:
                    // 按小时聚合
                    var groupedByHour = records.GroupBy(r => new DateTime(r.OpenDateTime.Year, r.OpenDateTime.Month, r.OpenDateTime.Day, r.OpenDateTime.Hour, 0, 0));
                    foreach (var group in groupedByHour)
                    {
                        var ohlcv = new Ohlcv
                        {
                            OpenDateTime = group.Key,
                            Open = group.First().Open,
                            High = group.Max(r => r.High),
                            Low = group.Min(r => r.Low),
                            Close = group.Last().Close,
                            Volume = group.Sum(r => r.Volume)
                        };
                        convertedRecords.Add(ohlcv);
                    }
                    break;

                case ResolutionLevel.Daily:
                    // 按天聚合
                    var groupedByDay = records.GroupBy(r => new DateTime(r.OpenDateTime.Year, r.OpenDateTime.Month, r.OpenDateTime.Day));
                    foreach (var group in groupedByDay)
                    {
                        var ohlcv = new Ohlcv
                        {
                            OpenDateTime = group.Key,
                            Open = group.First().Open,
                            High = group.Max(r => r.High),
                            Low = group.Min(r => r.Low),
                            Close = group.Last().Close,
                            Volume = group.Sum(r => r.Volume)
                        };
                        convertedRecords.Add(ohlcv);
                    }
                    break;

                case ResolutionLevel.Weekly:
                    // 按周聚合
                    var groupedByWeek = records.GroupBy(r => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(r.OpenDateTime, CalendarWeekRule.FirstDay, DayOfWeek.Monday));
                    foreach (var group in groupedByWeek)
                    {
                        var ohlcv = new Ohlcv
                        {
                            OpenDateTime = group.First().OpenDateTime.StartOfWeek(DayOfWeek.Monday),
                            Open = group.First().Open,
                            High = group.Max(r => r.High),
                            Low = group.Min(r => r.Low),
                            Close = group.Last().Close,
                            Volume = group.Sum(r => r.Volume)
                        };
                        convertedRecords.Add(ohlcv);
                    }
                    break;

                case ResolutionLevel.Monthly:
                    // 按月聚合
                    var groupedByMonth = records.GroupBy(r => new DateTime(r.OpenDateTime.Year, r.OpenDateTime.Month, 1));
                    foreach (var group in groupedByMonth)
                    {
                        var ohlcv = new Ohlcv
                        {
                            OpenDateTime = group.Key,
                            Open = group.First().Open,
                            High = group.Max(r => r.High),
                            Low = group.Min(r => r.Low),
                            Close = group.Last().Close,
                            Volume = group.Sum(r => r.Volume)
                        };
                        convertedRecords.Add(ohlcv);
                    }
                    break;
            }

            return convertedRecords;
        }
    }
}