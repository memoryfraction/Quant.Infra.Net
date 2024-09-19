using AutoMapper;
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
            // Build the configuration for config file, e.g. appsettings.json
            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddUserSecrets<Program>() 
                .Build();

            #region Dependency Injection
            var serviceCollection = new ServiceCollection();

            #region Scoped
            serviceCollection.AddScoped<IBinanceOrderService, BinanceOrderService>(); // Injection ITestService to the container
            #endregion

            #region Singleton
            serviceCollection.AddSingleton<IConfiguration>(configuration);  // Injection IConfiguration to the container

            // Register the Automapper to container
            serviceCollection.AddSingleton<IMapper>(sp =>
            {
                var autoMapperConfiguration = new MapperConfiguration(cfg =>
                {
                    cfg.AddProfile<MappingProfile>();
                });
                return new Mapper(autoMapperConfiguration);
            });
            #endregion
            #endregion

            var sp = serviceCollection.BuildServiceProvider();
            var _configuration = sp.GetService<IConfiguration>();
            var _apiKey =  _configuration["Exchange:apiKey"];
            var _apiSecret = _configuration["Exchange:apiSecret"];

            Binance.Net.Clients.BinanceRestClient.SetDefaultOptions(options =>
            {
                options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
            });

            // 创建 Binance 客户端            
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                // Margin Account Balance
                var account = await client.UsdFuturesApi.Account.GetAccountInfoV3Async();
                System.Console.WriteLine($"UsdFuturesApi Available Balance: {account.Data.AvailableBalance}."); // 获取合约账户的Margin Balance


                // 永续合约，开空仓
                //var enterShortResponse = await client.UsdFuturesApi.Trading.PlaceOrderAsync(
                //symbol: "ALGOUSDT",
                //   side: Binance.Net.Enums.OrderSide.Sell, // 开关仓此信号需要相反
                //   type: Binance.Net.Enums.FuturesOrderType.Market,
                //   quantity: 40, // 关仓数量需要与开仓数量一致， 总是正数
                //   positionSide: Binance.Net.Enums.PositionSide.Short // LONG/SHORT是对冲模式， 多头开关都用LONG, 空头开关都用SHORT
                //                                                      // closePosition: true // 不建议使用
                //   );


                // 获取当前持仓数量
                account = await client.UsdFuturesApi.Account.GetAccountInfoV3Async();
                var position = await client.UsdFuturesApi.Account.GetPositionInformationAsync();
                var holdingPositions = position.Data.Where(x => x.Quantity != 0).Select(x => x);
                var algoPosition = holdingPositions.Where(x => x.Symbol == "ALGOUSDT").FirstOrDefault();


                // 关空仓
                var exitShortResponse = await client.UsdFuturesApi.Trading.PlaceOrderAsync(
                   symbol: "ALGOUSDT",
                   side: Binance.Net.Enums.OrderSide.Buy, // 开关仓此信号需要相反
                   type: Binance.Net.Enums.FuturesOrderType.Market,
                   quantity: Math.Abs(algoPosition.Quantity), // 关仓数量需要与开仓数量一致， 总是正数
                   positionSide:Binance.Net.Enums.PositionSide.Short // LONG/SHORT是对冲模式， 多头开关都用LONG, 空头开关都用SHORT
                   // closePosition: true // 不建议使用
                   );

                System.Console.WriteLine("borrowed and repayed");
            }

        }
    }
}
