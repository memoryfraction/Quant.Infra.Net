namespace Quant.Infra.Net.Analysis.Models
{
    /// <summary>
    /// ADF 检验结果模型。
    /// ADF (Augmented Dickey-Fuller) test result model.
    /// </summary>
    public class AdfTestResult
    {
        /// <summary>
        /// ADF 统计量。范围约 [-5, 2]，越负越好，接近零或为正表示序列非平稳。
        /// ADF statistic. Range approximately [-5, 2]; more negative is better. Values near zero or positive indicate non-stationarity.
        /// 判断协整的阈值 / Cointegration thresholds:
        /// 1%: -3.43, 5%: -2.86, 10%: -2.57
        /// </summary>
        public double Statistic { get; set; }

        /// <summary>
        /// P 值（C# 计算的值为估计值）。
        /// P-value (calculated value in C# is an approximation).
        /// </summary>
        public double PValue { get; set; } 
    }

    /// <summary>
    /// Engle-Granger 协整检验结果模型。
    /// Engle-Granger cointegration test result model.
    /// </summary>
    public class EngleGrangerResult
    {
        /// <summary>
        /// 检验统计量 / Test statistic.
        /// </summary>
        public double Statistic { get; set; }
        /// <summary>
        /// P 值 / P-value.
        /// </summary>
        public double PValue { get; set; }
        /// <summary>
        /// 回归斜率 / Regression slope.
        /// </summary>
        public double Slope { get; set; }
        /// <summary>
        /// 回归截距 / Regression intercept.
        /// </summary>
        public double Intercept { get; set; }
    }
}
