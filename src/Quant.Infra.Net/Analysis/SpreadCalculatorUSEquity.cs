using Microsoft.Data.Analysis;
using Quant.Infra.Net.Shared.Model;
using System;

namespace Quant.Infra.Net.Analysis
{
    public class SpreadCalculatorUsEquity : SpreadCalculatorFixLength
    {
 

        /// <summary>
        /// 美股每年交易日252天，半年为126天
        /// </summary>
        public override int FixedWindowDays { get; set; } = 126;

        public override double BusinessHoursDaily { get; set; } = 6.5; // 数字货币市场每天24小时交易

        public override int HalfLifeWindowLength { get; set; } = 20;


        public SpreadCalculatorUsEquity(
            string symbol1,
            string symbol2,
            DataFrame df1,
            DataFrame df2,
            ResolutionLevel resolutionLevel = ResolutionLevel.Daily)
        : base(symbol1, symbol2, df1, df2, resolutionLevel)
        {
        }

        /// <summary>
        /// 根据输入的级别，计算窗口的长度
        /// </summary>
        /// <param name="resolutionLevel"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        protected override int CalcuWindowLength(ResolutionLevel resolutionLevel = ResolutionLevel.Daily)
        {
            switch (resolutionLevel)
            {
                case ResolutionLevel.Daily:
                    return FixedWindowDays;

                case ResolutionLevel.Weekly:
                    return FixedWindowDays * 7;

                case ResolutionLevel.Monthly:
                    return FixedWindowDays * 30;

                case ResolutionLevel.Hourly:
                    return (int)(FixedWindowDays * BusinessHoursDaily);

                case ResolutionLevel.Minute:
                    return (int)(FixedWindowDays * BusinessHoursDaily * 60);

                case ResolutionLevel.Second:
                    return (int)(FixedWindowDays * BusinessHoursDaily * 3600);

                case ResolutionLevel.Tick:
                    throw new NotImplementedException("Tick resolution is not implemented.");
                default:
                    throw new ArgumentException($"Unsupported resolution level: {resolutionLevel}");
            }
        }
    }
}