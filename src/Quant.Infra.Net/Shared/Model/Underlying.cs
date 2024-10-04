namespace Quant.Infra.Net.Shared.Model
{
    public class Underlying
    {
        public Underlying()
        {
        }

        public Underlying(string symbol, AssetType assetType)
        {
            Symbol = symbol;
            AssetType = assetType;
        }

        public string Symbol { get; set; }
        public AssetType AssetType { get; set; }
    }
}