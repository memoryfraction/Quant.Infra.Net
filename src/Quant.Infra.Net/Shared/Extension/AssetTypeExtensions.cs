using Quant.Infra.Net.Shared.Model;
using System.Text.RegularExpressions;

namespace Quant.Infra.Net.Shared.Extension
{
    // 注意：静态类
    public static class AssetTypeExtensions
    {
        // 注意：静态方法，第一个参数 this AssetType
        public static string ToSnakeCase(this AssetType assetType)
        {
            var name = assetType.ToString();
            var snake = Regex.Replace(name, @"([a-z])([A-Z])", "$1_$2").ToLower();
            return snake;
        }
    }
}
