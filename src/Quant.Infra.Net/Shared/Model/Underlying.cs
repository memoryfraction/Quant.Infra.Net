using System;

namespace Quant.Infra.Net.Shared.Model
{
    /// <summary>
    /// 标的资产模型，包含交易符号和资产类型。
    /// Underlying asset model containing the trading symbol and asset type.
    /// </summary>
    public class Underlying
    {
        /// <summary>
        /// 默认构造函数。
        /// Default constructor.
        /// </summary>
        public Underlying()
        {
        }

        /// <summary>
        /// 使用指定的交易符号和资产类型初始化标的资产。
        /// Initializes an underlying asset with the specified symbol and asset type.
        /// </summary>
        /// <param name="symbol">交易符号 / The trading symbol.</param>
        /// <param name="assetType">资产类型 / The asset type.</param>
        public Underlying(string symbol, AssetType assetType)
        {
            if (string.IsNullOrEmpty(symbol))
                throw new ArgumentException("Symbol cannot be null or empty.", nameof(symbol));

            Symbol = symbol;
            AssetType = assetType;
        }

        /// <summary>
        /// 交易符号 / The trading symbol.
        /// </summary>
        public string Symbol { get; set; }
        /// <summary>
        /// 资产类型 / The asset type.
        /// </summary>
        public AssetType AssetType { get; set; }
    }
}