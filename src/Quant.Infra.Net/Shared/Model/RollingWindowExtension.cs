using System;
using System.Linq;

namespace Quant.Infra.Net.Shared.Model
{
    public static class RollingWindowExtensions
    {
        /// <summary>
        /// 计算滑动窗口内元素的标准差
        /// Calculates the standard deviation of the elements within the rolling window.
        /// </summary>
        /// <typeparam name="T">要计算标准差的元素类型</typeparam>
        /// <param name="window">包含元素的滑动窗口</param>
        /// <returns>返回标准差的值</returns>
        /// <exception cref="InvalidOperationException">如果窗口为空，抛出此异常</exception>
        /// <exception cref="ArgumentException">如果元素类型无法转换为 decimal，抛出此异常</exception>
        public static decimal CalculateStandardDeviation<T>(this RollingWindow<T> window)
        {
            // 检查 T 能否被转换为 decimal
            if (!typeof(T).IsValueType && typeof(T) != typeof(decimal))
            {
                throw new ArgumentException("The type T must be a value type or decimal.", nameof(T));
            }

            if (window.Count == 0)
                throw new InvalidOperationException("The window is empty.");

            // 假设 T 是 decimal 或可以转换为 decimal 的类型
            var mean = window.Average(x => Convert.ToDecimal(x));
            var sumOfSquaresOfDifferences = window.Sum(val =>
                (Convert.ToDecimal(val) - mean) * (Convert.ToDecimal(val) - mean));

            return (decimal)Math.Sqrt((double)(sumOfSquaresOfDifferences / window.Count));
        }
    }
}