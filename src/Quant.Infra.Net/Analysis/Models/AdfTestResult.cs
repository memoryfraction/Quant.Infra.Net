namespace Quant.Infra.Net.Analysis.Models
{
    public class AdfTestResult
    {
        /// <summary>
        /// 范围: [-5,2]， 越负越好， 统计量接近零或者为正，说明序列非平稳
        /// 判断协整的阈值是
        /// 1%, -3.43
        /// 5%, -2.86
        /// 10%, -2.57
        /// </summary>
        public double Statistic { get; set; }

        /// <summary>
        /// C# 计算的PVALUE只能是估计值
        /// </summary>
        public double PValue { get; set; } 
    }

    public class EngleGrangerResult
    {
        public double Statistic { get; set; }
        public double PValue { get; set; }
        public double Slope { get; set; }
        public double Intercept { get; set; }
    }
}
