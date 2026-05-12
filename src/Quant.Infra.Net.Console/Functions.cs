using CryptoExchange.Net.Authentication;

namespace Quant.Infra.Net.Console
{
    public class Functions
    {

        /// <summary>
        /// Calculates the unrealized profit rate for the current position.
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="apiKey"></param>
        /// <param name="secret"></param>
        /// <returns></returns>
        public static async Task<decimal> CalculateUnrealizedProfitRate(string symbol,string apiKey, string secret)
        {
            Binance.Net.Clients.BinanceRestClient.SetDefaultOptions(options =>
            {
                options.ApiCredentials = new ApiCredentials(apiKey, secret);
            });

            // Create Binance client.
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var account = await client.UsdFuturesApi.Account.GetAccountInfoV3Async();
                var holdingPositions = client.UsdFuturesApi.Account.GetPositionInformationAsync().Result.Data.Where(x => x.Quantity != 0).Select(x => x);
                var position = holdingPositions.Where(x => x.Symbol == symbol).FirstOrDefault();
                if (position == null)
                    return 0m;
                var percentage = position.UnrealizedPnl / (position.EntryPrice * position.Quantity);
                return percentage;
            }
        }
    }
}
