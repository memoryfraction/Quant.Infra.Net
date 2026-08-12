using Quant.Infra.Net.Shared.Model;
using System.Text.RegularExpressions;

namespace Quant.Infra.Net.Shared.Extension
{
    /// <summary>
    /// AssetType 枚举的扩展方法集合。
    /// Extension methods for the AssetType enum.
    /// </summary>
    public static class AssetTypeExtensions
    {
        /// <summary>
        /// 将 AssetType 转换为蛇形命名（如 CryptoPerpetualContract → crypto_perpetual_contract）。
        /// Convert an AssetType value to snake_case string (e.g., CryptoPerpetualContract → crypto_perpetual_contract).
        /// </summary>
        /// <param name="assetType">要转换的资产类型 / The asset type to convert.</param>
        /// <returns>蛇形命名的字符串 / Snake-cased string representation.</returns>
        public static string ToSnakeCase(this AssetType assetType)
        {
            var name = assetType.ToString();
            var snake = Regex.Replace(name, @"([a-z])([A-Z])", "$1_$2").ToLower();
            return snake;
        }
    }
}
