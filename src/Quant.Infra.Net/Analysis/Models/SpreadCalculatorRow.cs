using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Analysis.Models
{
    /// <summary>
    /// Spread 计算结果行模型，包含斜率、截距、价差和方程。
    /// Spread calculator row model containing slope, intercept, spread value, and equation.
    /// </summary>
    public class SpreadCalculatorRow
    {
        /// <summary>
        /// 回归斜率 / Regression slope.
        /// </summary>
        public double Slope { get; set; }
        /// <summary>
        /// 回归截距 / Regression intercept.
        /// </summary>
        public double Intercept { get; set; }
        /// <summary>
        /// 价差（残差） / Spread (residual).
        /// </summary>
        public double Spread { get; set; }
        /// <summary>
        /// 价差方程公式 / Spread equation formula.
        /// </summary>
        public string Equation { get; set; }
    }
}
