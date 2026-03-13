using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Analysis.Models
{
    /// <summary>
    /// 分析元素模型，包含交易符号、日期时间和值。
    /// Analysis element model containing a trading symbol, DateTime, and value.
    /// </summary>
    public class Element
    {
        /// <summary>
        /// 使用指定的交易符号、日期时间和值初始化元素。
        /// Initializes an element with the specified symbol, DateTime, and value.
        /// </summary>
        /// <param name="symbol">交易符号 / The trading symbol.</param>
        /// <param name="dt">日期时间 / The DateTime.</param>
        /// <param name="value">值 / The value.</param>
        public Element(string symbol, DateTime dt, double value) 
        {
            if (string.IsNullOrEmpty(symbol))
                throw new ArgumentException("Symbol cannot be null or empty.", nameof(symbol));

            Symbol = symbol;
            DateTime = dt;
            Value = value;
        }
        /// <summary>
        /// 交易符号 / The trading symbol.
        /// </summary>
        public string Symbol { get; set; }
        /// <summary>
        /// 日期时间 / The DateTime.
        /// </summary>
        public DateTime DateTime { get; set; }
        /// <summary>
        /// 值 / The value.
        /// </summary>
        public double Value { get; set; }
    }
}
