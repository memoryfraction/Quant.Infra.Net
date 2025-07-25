using Microsoft.Data.Analysis;
using Quant.Infra.Net.Shared.Model;
using System;

namespace Quant.Infra.Net.Analysis
{
    public class SpreadCalculatorPerpetualContract : SpreadCalculatorFixLength
    {
        /// <summary>
        /// 永续合约全年无休交易，半年为183天
        /// </summary>
        public override int CointegrationFixedWindowLength { get; set; } = 183;

        public override double BusinessHoursDaily { get; set; } = 24; // 数字货币市场每天24小时交易

        public override int HalfLifeWindowLength { get; set; } = 30;

        public SpreadCalculatorPerpetualContract(
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
                    // 加密货币市场每天24小时交易
                    return (int)Math.Floor(CointegrationFixedWindowLength * BusinessHoursDaily);

                case ResolutionLevel.Minute:
                    // 每天24小时 * 60分钟
                    return (int)Math.Floor(CointegrationFixedWindowLength * BusinessHoursDaily * 60);

                case ResolutionLevel.Second:
                    // 每天24小时 * 3600秒
                    return (int)Math.Floor(CointegrationFixedWindowLength * BusinessHoursDaily * 3600);

                case ResolutionLevel.Tick:
                    // 每次交易的窗口长度取决于实际的实现需求，这里没有具体实现
                    throw new NotImplementedException("Tick resolution is not implemented.");
                default:
                    throw new ArgumentException($"Unsupported resolution level: {resolutionLevel}");
            }
        }
    }
}