using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        public async Task GetBalanceAsync_Should_Work()
        {
            var usdFutureService = _serviceProvider.GetService<IBinanceUsdFutureService>();

            var balance = await usdFutureService.GetusdFutureAccountBalanceAsync();

            Assert.IsTrue(balance>10000);
        }

        // todo 其他方法; 

    }
}
