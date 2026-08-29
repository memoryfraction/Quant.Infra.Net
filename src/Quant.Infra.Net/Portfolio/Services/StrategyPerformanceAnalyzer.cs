using System;
using System.Collections.Generic;
using System.Linq;

namespace Quant.Infra.Net.Portfolio.Services
{
    /// <summary>
    /// 提供策略表现分析的方法，包括年化复合增长率 (CAGR)、夏普比率 (Sharpe Ratio)、卡尔玛比率 (Calmar Ratio)、最大回撤及回撤持续时间的计算。
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
        /// 计算年化夏普比率 (Annualized Sharpe Ratio)。
        /// 用百分比收益（相对上一期）而非绝对金额：权益会随本金增长，绝对 $ 的 std 会随规模失真；百分比才与规模无关、可跨年比较。
        /// </summary>
        /// <param name="marketValueDict">一个以日期为键、市场价值为值的字典。</param>
        /// <param name="riskFreeRate">无风险利率（年化，如 0.02 = 2%）。</param>
        /// <param name="frequencyPerYear">每年观测次数（日线=252，月线=12，周线=52），用于年化。</param>
        /// <returns>年化夏普比率。</returns>
        public static decimal CalculateSharpeRatio(Dictionary<DateTime, decimal> marketValueDict, decimal riskFreeRate, int frequencyPerYear = 252)
        {
            if (marketValueDict.Count < 2) return 0;

            var ordered = marketValueDict.OrderBy(kvp => kvp.Key).ToList();
            var returns = new List<decimal>();
            for (var i = 1; i < ordered.Count; i++)
            {
                var prev = ordered[i - 1].Value;
                if (prev != 0) returns.Add(ordered[i].Value / prev - 1);
            }
            if (returns.Count < 2) return 0;

            var averageReturn = returns.Average();
            var standardDeviation = (decimal)Math.Sqrt(returns.Select(r => Math.Pow((double)r - (double)averageReturn, 2)).Average());
            if (standardDeviation == 0) return 0;

            var perPeriodSharpe = (averageReturn - riskFreeRate / frequencyPerYear) / standardDeviation;
            return perPeriodSharpe * (decimal)Math.Sqrt((double)frequencyPerYear); // 年化
        }

        /// <summary>
        /// 计算卡尔玛比率 (Calmar Ratio)。
        /// </summary>
        /// <param name="marketValueDict">一个以日期为键、市场价值为值的字典。</param>
        /// <returns>卡尔玛比率。</returns>
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
        /// <returns>最大回撤（正数，如 0.35 = 35%）。</returns>
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
        /// <returns>(最大回撤，最大回撤持续天数) 元组。</returns>
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
