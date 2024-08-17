using Deedle;
using Quant.Infra.Net.Shared.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Quant.Infra.Net.Analysis
{
    /// <summary>
    /// PairTrading的残差计算器，定长窗体外推法
    /// </summary>
    public class PairTradingDiffCalculator_FixLengthWindow
    {
        /// <summary>
        /// 默认定长183， 默认单位：天
        /// </summary>
        public static int FixedWindowLength { get; set; } = 183;
        public ResolutionLevel Resolution { get; set; } = ResolutionLevel.Daily;
        public string Symbol1 { get; set; }
        public string Symbol2 { get; set; } 
        public HashSet<TimeSeriesElement> TimeSeries1 { get; set; }
        public HashSet<TimeSeriesElement> TimeSeries2 { get; set; }

        private Queue _timeSeriesQueue1;
        private Queue _timeSeriesQueue2;

        public PairTradingDiffCalculator_FixLengthWindow(string symbol1 , string symbol2, ResolutionLevel resolutionLevel = ResolutionLevel.Daily)
        {
            Symbol1 = symbol1;
            Symbol2 = symbol2;
            Resolution = resolutionLevel;
            FixedWindowLength = CalcuWindowLength(Resolution);
            _timeSeriesQueue1 = new Queue(FixedWindowLength);
            _timeSeriesQueue2 = new Queue(FixedWindowLength);
        }


        /// <summary>
        /// 根据输入的ts1，和ts2， 更新TimeSeries1和TimeSeries2
        /// </summary>
        /// <param name="timeSeries1"></param>
        /// <param name="timeSeries2"></param>
        public void UpdateTimerSeries(IEnumerable<TimeSeriesElement> timeSeries1, IEnumerable<TimeSeriesElement> timeSeries2)
        {
            var ts1List = timeSeries1.ToList();
            var ts2List = timeSeries2.ToList();

            // 如果 timeSeries1 和 timeSeries2 的元素数量不同，则报错
            if (ts1List.Count != ts2List.Count)
            {
                throw new ArgumentException("timeSeries1 and timeSeries2 must have the same number of elements.");
            }

            // 遍历并检查每个元素的 DateTime 是否相同
            for (int i = 0; i < ts1List.Count; i++)
            {
                if (ts1List[i].DateTime != ts2List[i].DateTime)
                {
                    throw new ArgumentException($"Element {i} of timeSeries1 and timeSeries2 have different DateTime values.");
                }
            }

            // 更新 TimeSeries1 和 TimeSeries2
            TimeSeries1 = new HashSet<TimeSeriesElement>(ts1List);
            TimeSeries2 = new HashSet<TimeSeriesElement>(ts2List);

            // 更新 _timeSeriesQueue1 和 _timeSeriesQueue2，并保持固定长度, 尽量存储新元素，dequeue老元素
            foreach (var element in ts1List)
            {
                // 如果队列已满，移除最早的元素
                if (_timeSeriesQueue1.Count >= FixedWindowLength)
                {
                    _timeSeriesQueue1.Dequeue();
                }
                // 添加新的元素到队列中
                _timeSeriesQueue1.Enqueue(element);
            }

            foreach (var element in ts2List)
            {
                // 如果队列已满，移除最早的元素
                if (_timeSeriesQueue2.Count >= FixedWindowLength)
                {
                    _timeSeriesQueue2.Dequeue();
                }
                // 添加新的元素到队列中
                _timeSeriesQueue2.Enqueue(element);
            }
        }


        /// <summary>
        /// 根据输入的ts1，和ts2， 更新TimeSeries1和TimeSeries2
        /// </summary>
        /// <param name="timeSeries1"></param>
        /// <param name="timeSeries2"></param>
        public void UpdateTimerSeriesElement(TimeSeriesElement timeSeriesElm1, TimeSeriesElement timeSeriesElm2)
        {
            if (ValidateSourceData() == false)
                throw new ArgumentException("data source are not valid, please execute UpdateTimerSeries() first.");

            // 如果 timeSeriesElm1 和 timeSeriesElm2 的 DateTime 不同，则报错
            if (timeSeriesElm1.DateTime != timeSeriesElm2.DateTime)
            {
                throw new ArgumentException("timeSeriesElm1 and timeSeriesElm2 must have the same DateTime.");
            }

            // 更新 TimeSeries1 和 TimeSeries2
            TimeSeries1.Add(timeSeriesElm1);
            TimeSeries2.Add(timeSeriesElm2);

            // 更新 _timeSeriesQueue1，并保持固定长度
            if (_timeSeriesQueue1.Count >= FixedWindowLength)
            {
                _timeSeriesQueue1.Dequeue(); // 移除最早的元素
            }
            _timeSeriesQueue1.Enqueue(timeSeriesElm1); // 添加新的元素

            // 更新 _timeSeriesQueue2，并保持固定长度
            if (_timeSeriesQueue2.Count >= FixedWindowLength)
            {
                _timeSeriesQueue2.Dequeue(); // 移除最早的元素
            }
            _timeSeriesQueue2.Enqueue(timeSeriesElm2); // 添加新的元素
        }


        /// <summary>
        /// 根据输入的endDateTime,向前FixedWindowLength，计算Diff
        /// </summary>
        /// <param name="endDateTime"></param>
        /// <returns>结束日期endDateTime，如果为null，说明：取数据源最新的日期</returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public double CalculateDiff(DateTime? endDateTime = null)
        {
            if (ValidateSourceData() == false)
                throw new ArgumentException("data source are not valid, please execute UpdateTimerSeries() first.");

            if (endDateTime == null)
                endDateTime = TimeSeries1.OrderBy(x => x.DateTime).Select(x=>x.DateTime).LastOrDefault();

            // 获取时间窗口内的元素
            var seriesA = TimeSeries1
                .Where(x => x.DateTime <= endDateTime)
                .OrderByDescending(x => x.DateTime)
                .Take(FixedWindowLength)
                .Select(x => x.Value)
                .ToList();

            var seriesB = TimeSeries2
                .Where(x => x.DateTime <= endDateTime)
                .OrderByDescending(x => x.DateTime)
                .Take(FixedWindowLength)
                .Select(x => x.Value)
                .ToList();

            // 确保两个时间序列都有足够的数据点
            if (seriesA.Count != FixedWindowLength || seriesB.Count != FixedWindowLength)
            {
                throw new InvalidOperationException("Not enough data points to perform the calculation.");
            }

            // 执行线性回归，获取slope和interception
            var (slope, intercept) = (new Analysis.Service.AnalysisService()).PerformOLSRegression(seriesA, seriesB);

            // 计算diff,注意：此时SeriesA和SeriesB为DateTime倒序;
            double diff = 0;
            var lastElmInSeriesA = seriesA.FirstOrDefault();
            var lastElmInSeriesB = seriesB.FirstOrDefault();
            diff = lastElmInSeriesB - slope * lastElmInSeriesA - intercept;

            return diff;
        }

        /// <summary>
        /// 生成并返回Equation公式
        /// </summary>
        /// <param name="endDateTime"></param>
        /// <returns>结束日期endDateTime，如果为null，说明：取数据源最新的日期</returns>
        /// <exception cref="ArgumentException"></exception>
        public string PrintEquation(DateTime? endDateTime = null)
        {
            if (ValidateSourceData() == false)
                throw new ArgumentException("data source are not valid, please execute UpdateTimerSeries() first.");

            if (endDateTime == null)
                endDateTime = TimeSeries1.OrderBy(x => x.DateTime).Select(x => x.DateTime).LastOrDefault();

            // 获取时间窗口内的元素
            var seriesA = TimeSeries1
                .Where(x => x.DateTime <= endDateTime)
                .OrderByDescending(x => x.DateTime)
                .Take(FixedWindowLength)
                .Select(x => x.Value)
                .ToList();

            var seriesB = TimeSeries2
                .Where(x => x.DateTime <= endDateTime)
                .OrderByDescending(x => x.DateTime)
                .Take(FixedWindowLength)
                .Select(x => x.Value)
                .ToList();

            // 确保两个时间序列都有足够的数据点
            if (seriesA.Count != FixedWindowLength || seriesB.Count != FixedWindowLength)
            {
                throw new InvalidOperationException("Not enough data points to perform the calculation.");
            }

            // 执行线性回归，获取slope和interception
            var (slope, intercept) = (new Analysis.Service.AnalysisService()).PerformOLSRegression(seriesA, seriesB);
            slope = Math.Round(slope, 4);
            intercept = Math.Round(intercept, 4);
            var equation = $"diff = {Symbol2} - {slope} * {Symbol1} - {intercept}";
            return equation;
        }

        /// <summary>
        /// 检验数据源是否合格？
        /// </summary>
        /// <returns></returns>
        private bool ValidateSourceData()
        {
            if (TimeSeries1 == null || TimeSeries2 == null)
                return false;

            if (TimeSeries1 == default || TimeSeries2 == default)
                return false;

            if (TimeSeries1.Count != TimeSeries2.Count)
                return false;

            return true;
        }


        /// <summary>
        /// 根据resolutionLevel计算window的长度;
        /// </summary>
        /// <param name="resolutionLevel"></param>
        /// <returns></returns>
        private int CalcuWindowLength(ResolutionLevel resolutionLevel = ResolutionLevel.Daily)
        {
            switch (resolutionLevel)
            {
                case ResolutionLevel.Daily:
                    return FixedWindowLength;

                case ResolutionLevel.Weekly:
                    return FixedWindowLength * 7;

                case ResolutionLevel.Monthly:
                    return FixedWindowLength * 30; // 平均一个月30天，具体可根据需要调整

                case ResolutionLevel.Hourly:
                    return FixedWindowLength * 24;

                case ResolutionLevel.Minute:
                    return FixedWindowLength * 24 * 60;

                case ResolutionLevel.Second:
                    return FixedWindowLength * 24 * 60 * 60;

                case ResolutionLevel.Tick:
                    return FixedWindowLength * 24 * 60 * 60 * 1000; // 假设每秒1000个tick

                default:
                    throw new ArgumentOutOfRangeException(nameof(resolutionLevel), resolutionLevel, null);
            }

        }

    }
}
