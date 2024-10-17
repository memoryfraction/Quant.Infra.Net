using AutoMapper;
using Binance.Net.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlX.XDevAPI;
using Quant.Infra.Net.Account.Service;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Broker.Service;
using Quant.Infra.Net.Notification.Service;

namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class BinanceUsdFutureTests
    {
        // IOC
        private ServiceCollection _services;
        private string _apiKey, _apiSecret;
        private IConfigurationRoot _configuration;
        private ServiceProvider _serviceProvider;
        
        public BinanceUsdFutureTests()
        {
            // Read Secret
            _configuration = new ConfigurationBuilder()
               .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
               .AddJsonFile("appsettings.json")
               .AddUserSecrets<BinanceTests>()
               .Build();

            // 依赖注入
            _services = new ServiceCollection();
            _services.AddScoped<IDingtalkService, DingtalkService>();
            _services.AddScoped<IBinanceOrderService, BinanceOrderService>();
            _services.AddScoped<BrokerServiceBase, BinanceService>();
            _services.AddScoped<IBinanceUsdFutureService, BinanceUsdFutureService>();
            _services.AddSingleton<IConfiguration>(_configuration);
            _services.AddSingleton<IMapper>(sp =>
            {
                var autoMapperConfiguration = new MapperConfiguration(cfg =>
                {
                    cfg.AddProfile<MappingProfile>();
                });
                return new Mapper(autoMapperConfiguration);
            });
            _serviceProvider = _services.BuildServiceProvider();

            _apiKey = _configuration["Exchange:apiKey"];
            _apiSecret = _configuration["Exchange:apiSecret"];

        }

        [TestMethod]
        public async Task ShowPositionMode_Should_Work()
        {
            var usdFutureService = _serviceProvider.GetService<IBinanceUsdFutureService>();
            await usdFutureService.ShowPositionModeAsync();
        }


        [TestMethod]
        public async Task GetBalanceAsync_Should_Work()
        {
            var usdFutureService = _serviceProvider.GetService<IBinanceUsdFutureService>();
            var balance = await usdFutureService.GetusdFutureAccountBalanceAsync();
            Assert.IsTrue(balance>10000);
        }


        [TestMethod]
        public async Task HasUsdFuturePositionAsync_Should_Work()
        {
            var usdFutureService = _serviceProvider.GetService<IBinanceUsdFutureService>();
            var result = await usdFutureService.HasUsdFuturePositionAsync("BTCUSDT");
            Assert.IsTrue(result == false);
        }

        [TestMethod]
        public async Task LiquidateUsdFutureAsync_Should_Work()
        {
            var usdFutureService = _serviceProvider.GetService<IBinanceUsdFutureService>();
            await usdFutureService.LiquidateUsdFutureAsync("BTCUSDT");
        }


        [TestMethod]
        public async Task SetUsdFutureLongHoldingsAsync_Should_Work()
        {
            var usdFutureService = _serviceProvider.GetService<IBinanceUsdFutureService>();

            var symbol = "BTCUSDT";
            await usdFutureService.SetUsdFutureHoldingsAsync(symbol, 0.01, PositionSide.Long); // 多头选择 PositionSide.Long;
            Thread.Sleep(5000);
            await usdFutureService.LiquidateUsdFutureAsync(symbol);
        }

        [TestMethod]
        public async Task SetUsdFutureShortHoldingsAsync_Should_Work()
        {
            var usdFutureService = _serviceProvider.GetService<IBinanceUsdFutureService>();
            var symbol = "BTCUSDT";
            await usdFutureService.SetUsdFutureHoldingsAsync(symbol, -0.01, PositionSide.Short);  // 空头选择 PositionSide.Short;
            Thread.Sleep(5000);
            await usdFutureService.LiquidateUsdFutureAsync(symbol);
        }


        [TestMethod]
        public async Task GetusdFutureUnrealizedProfitRateAsync_Should_Work()
        {
            var usdFutureService = _serviceProvider.GetService<IBinanceUsdFutureService>();
            var symbol1 = "BTCUSDT";
            var symbol2 = "ETHUSDT";

            // 同时做多和做空
            await usdFutureService.SetUsdFutureHoldingsAsync(symbol1, 0.01, PositionSide.Long);  
            await usdFutureService.SetUsdFutureHoldingsAsync(symbol2, -0.01, PositionSide.Short);  
            
            Thread.Sleep(10000);

            var unrealizedProfitRate = await usdFutureService.GetusdFutureUnrealizedProfitRateAsync();
            Console.WriteLine($"UnrealizedProfitRate:{unrealizedProfitRate}");

            await usdFutureService.LiquidateUsdFutureAsync(symbol1);
            await usdFutureService.LiquidateUsdFutureAsync(symbol2);
        }

    }
}
