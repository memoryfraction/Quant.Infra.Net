using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.CommonObjects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Quant.Infra.Net.Console
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            // Build configuration from appsettings.json and user secrets.
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddUserSecrets<Program>() 
                .Build();

            #region Dependency Injection
            var serviceCollection = new ServiceCollection();

            #region Scoped
            serviceCollection.AddScoped<IBinanceOrderService, BinanceOrderService>();
            #endregion

            #region Singleton
            serviceCollection.AddSingleton<IConfiguration>(configuration);  // Injection IConfiguration to the container

            #endregion
            #endregion

            var sp = serviceCollection.BuildServiceProvider();
            var _configuration = sp.GetService<IConfiguration>();
            var _apiKey =  _configuration["Binance:ApiKey"];
            var _apiSecret = _configuration["Binance:ApiSecret"];

            Binance.Net.Clients.BinanceRestClient.SetDefaultOptions(options =>
            {
                options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
            });

                // Create Binance client.
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                // Margin account balance.
                var account = await client.UsdFuturesApi.Account.GetAccountInfoV3Async();
                System.Console.WriteLine($"UsdFuturesApi Available Balance: {account.Data.AvailableBalance}.");


                // Perpetual contract sample: open a short position.
                //var enterShortResponse = await client.UsdFuturesApi.Trading.PlaceOrderAsync(
                //symbol: "ALGOUSDT",
                //   side: Binance.Net.Enums.OrderSide.Sell,
                //   type: Binance.Net.Enums.FuturesOrderType.Market,
                //   quantity: 40,
                //   positionSide: Binance.Net.Enums.PositionSide.Short
                //   // closePosition: true
                //   );


                // Get current position quantity.
                account = await client.UsdFuturesApi.Account.GetAccountInfoV3Async();
                var position = await client.UsdFuturesApi.Account.GetPositionInformationAsync();
                var holdingPositions = position.Data.Where(x => x.Quantity != 0).Select(x => x);
                var algoPosition = holdingPositions.Where(x => x.Symbol == "ALGOUSDT").FirstOrDefault();


                // Close the short position.
                var exitShortResponse = await client.UsdFuturesApi.Trading.PlaceOrderAsync(
                   symbol: "ALGOUSDT",
                   side: Binance.Net.Enums.OrderSide.Buy,
                   type: Binance.Net.Enums.FuturesOrderType.Market,
                   quantity: Math.Abs(algoPosition.Quantity),
                   positionSide:Binance.Net.Enums.PositionSide.Short
                   // closePosition: true
                   );

                System.Console.WriteLine("borrowed and repayed");
            }

        }
    }
}
