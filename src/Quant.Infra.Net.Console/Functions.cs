using CryptoExchange.Net.Authentication;

namespace Quant.Infra.Net.Console
{
    /// <summary>
    /// Console helper functions for exchange account calculations.
    /// 控制台项目使用的交易所账户计算辅助函数。
    /// </summary>
    public class Functions
    {

        /// <summary>
        /// Calculates the unrealized profit rate for the current futures position.
        /// 计算当前合约持仓的未实现收益率。
        /// </summary>
        /// <param name="symbol">Trading symbol. / 交易标的代码。</param>
        /// <param name="apiKey">Binance API key. / Binance API Key。</param>
        /// <param name="secret">Binance API secret. / Binance API Secret。</param>
        /// <returns>Unrealized profit rate, or zero when no position exists. / 未实现收益率；无持仓时返回零。</returns>
        public static async Task<decimal> CalculateUnrealizedProfitRate(string symbol, string apiKey, string secret)
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
