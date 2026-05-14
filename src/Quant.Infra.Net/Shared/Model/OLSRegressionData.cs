using System.Collections.Generic;

namespace Quant.Infra.Net.Shared.Model
{
    /// <summary>
    /// OLS 线性回归数据模型，包含两组数据序列。
    /// OLS (Ordinary Least Squares) regression data model containing two data series.
    /// </summary>
    public class OLSRegressionData
    {
        /// <summary>
        /// 数据序列A / Data series A.
        /// </summary>
        public List<double> SeriesA { get; set; } = new List<double>();
        /// <summary>
        /// 数据序列B / Data series B.
        /// </summary>
        public List<double> SeriesB { get; set; } = new List<double>();
    }
}