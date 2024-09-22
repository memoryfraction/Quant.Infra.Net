using System;
using System.Collections.Generic;
using System.Linq;

namespace Quant.Infra.Net.Portfolio.Services
{
    /// <summary>
    /// 提供策略表现分析的方法，包括年化复合增长率 (CAGR)、夏普比率 (Sharpe Ratio)、卡尔玛比率 (Calmar Ratio) 和最大回撤及回撤持续时间的计算。
    /// </summary>
    public class StrategyPerformanceAnalyzer
    {
        /// <summary>
        /// 计算年化复合增长率 (CAGR)。
        /// </summary>
        /// <param name="marketValueDict">一个以日期为键、市场价值为值的字典。</param>
        /// <returns>年化复合增长率 (CAGR)。</returns>
        public static decimal CalculateCAGR(Dictionary<DateTime, decimal> marketValueDict)
        {
            if (marketValueDict.Count < 2) return 0;

            var dates = marketValueDict.Keys.OrderBy(d => d).ToList();
            var initialValue = marketValueDict[dates.First()];
            var finalValue = marketValueDict[dates.Last()];
            var years = (dates.Last() - dates.First()).TotalDays / 365.25;

            // 计算 CAGR
            return (decimal)Math.Pow((double)(finalValue / initialValue), 1.0 / years) - 1;
        }

        /// <summary>
        /// 计算夏普比率 (Sharpe Ratio)。
        /// </summary>
        /// <param name="marketValueDict">一个以日期为键、市场价值为值的字典。</param>
        /// <param name="riskFreeRate">无风险利率。</param>
        /// <returns>夏普比率 (Sharpe Ratio)。</returns>
        public static decimal CalculateSharpeRatio(Dictionary<DateTime, decimal> marketValueDict, decimal riskFreeRate)
        {
            var returns = marketValueDict.Values.Zip(marketValueDict.Values.Skip(1), (prev, curr) => curr - prev).ToList();
            var averageReturn = returns.Average();
            var standardDeviation = (decimal)Math.Sqrt(returns.Select(r => Math.Pow((double)r - (double)averageReturn, 2)).Average());

            return (averageReturn - riskFreeRate) / standardDeviation;
        }

        /// <summary>
        /// 计算卡尔玛比率 (Calmar Ratio)。
        /// </summary>
        /// <param name="marketValueDict">一个以日期为键、市场价值为值的字典。</param>
        /// <returns>卡尔玛比率 (Calmar Ratio)。</returns>
        public static decimal CalculateCalmarRatio(Dictionary<DateTime, decimal> marketValueDict)
        {
            var values = marketValueDict.Values.ToList();
            var annualReturn = CalculateCAGR(marketValueDict);
            var maxDrawdown = CalculateMaximumDrawdown(values);

            return maxDrawdown == 0 ? 0 : annualReturn / maxDrawdown;
        }

        /// <summary>
        /// 计算最大回撤 (Maximum Drawdown)。
        /// </summary>
        /// <param name="values">市场价值的列表。</param>
        /// <returns>最大回撤 (Maximum Drawdown)。</returns>
        public static decimal CalculateMaximumDrawdown(List<decimal> values)
        {
            decimal maxDrawdown = 0;
            decimal peak = values[0];

            foreach (var value in values)
            {
                if (value > peak)
                {
                    peak = value;
                }

                var drawdown = (peak - value) / peak;
                if (drawdown > maxDrawdown)
                {
                    maxDrawdown = drawdown;
                }
            }

            return maxDrawdown;
        }

        /// <summary>
        /// 计算最大回撤及其持续时间。
        /// </summary>
        /// <param name="values">市场价值的列表。</param>
        /// <returns>最大回撤及其持续时间的元组 (MaxDrawdown, MaxDrawdownDuration)。</returns>
        public static (decimal MaxDrawdown, int MaxDrawdownDuration) CalculateMaxDrawdownDuration(List<decimal> values)
        {
            decimal maxDrawdown = 0;
            int maxDrawdownDuration = 0;
            int currentDrawdownDuration = 0;
            decimal peak = values[0];

            foreach (var value in values)
            {
                if (value > peak)
                {
                    peak = value;
                    currentDrawdownDuration = 0;
                }
                else
                {
                    currentDrawdownDuration++;
                    var drawdown = (peak - value) / peak;
                    if (drawdown > maxDrawdown)
                    {
                        maxDrawdown = drawdown;
                        maxDrawdownDuration = currentDrawdownDuration;
                    }
                }
            }

            return (maxDrawdown, maxDrawdownDuration);
        }
    }
}