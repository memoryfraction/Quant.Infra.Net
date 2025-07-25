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
        public override int CointegrationFixedWindowLength { get; set; } = 126;

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
                    return CointegrationFixedWindowLength;

                case ResolutionLevel.Weekly:
                    return CointegrationFixedWindowLength * 7;

                case ResolutionLevel.Monthly:
                    return CointegrationFixedWindowLength * 30;

                case ResolutionLevel.Hourly:
                    return (int)(CointegrationFixedWindowLength * BusinessHoursDaily);

                case ResolutionLevel.Minute:
                    return (int)(CointegrationFixedWindowLength * BusinessHoursDaily * 60);

                case ResolutionLevel.Second:
                    return (int)(CointegrationFixedWindowLength * BusinessHoursDaily * 3600);

                case ResolutionLevel.Tick:
                    throw new NotImplementedException("Tick resolution is not implemented.");
                default:
                    throw new ArgumentException($"Unsupported resolution level: {resolutionLevel}");
            }
        }
    }
}