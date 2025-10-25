using AutoMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.SourceData.Service;


namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class CryptoSourceDataServiceTests
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly ServiceCollection _serviceCollection;
        private readonly IConfigurationRoot _configuration;
        private string _cmcApiKey;

        public CryptoSourceDataServiceTests()
        {
            _serviceCollection = new ServiceCollection();
            // Register the Automapper to container
            _serviceCollection.AddSingleton<IMapper>(sp =>
            {
                var autoMapperConfiguration = new MapperConfiguration(cfg =>
                {
                    cfg.AddProfile<MappingProfile>();
                });
                return new Mapper(autoMapperConfiguration);
            });
            _serviceCollection.AddScoped<ICryptoSourceDataService, CryptoSourceDataService>();

            // Read Secret
            _configuration = new ConfigurationBuilder()
               .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
               .AddJsonFile("appsettings.json")
               .AddUserSecrets<CryptoSourceDataServiceTests>()
               .Build();

            _serviceCollection.AddSingleton<IConfiguration>(_configuration);

            _serviceProvider = _serviceCollection.BuildServiceProvider();

            _cmcApiKey = _configuration["CoinMarketCap:ApiKey"].ToString();
        }

        /// <summary>
        /// 验证能否从 CoinMarketCap 获取市值前 50 的 symbols
        /// </summary>
        [TestMethod]
        public async Task GetTopMarketCapSymbolsFromCoinMarketCapAsync_Should_Work()
        {
            if (string.IsNullOrWhiteSpace(_cmcApiKey))
            {
                Assert.Inconclusive("未设置环境变量 CMC_API_KEY，测试已跳过。");
            }

            var svc = _serviceProvider.GetRequiredService<ICryptoSourceDataService>();

            const int count = 50;
            const string baseUrl = "https://pro-api.coinmarketcap.com"; // 如需沙箱可改为 https://sandbox-api.coinmarketcap.com

            var symbols = await svc.GetTopMarketCapSymbolsFromCoinMarketCapAsync(_cmcApiKey, baseUrl, count);

            // 基本断言
            Assert.IsNotNull(symbols, "返回结果不应为 null。");
            Assert.AreEqual(count, symbols.Count, $"应返回恰好 {count} 个 symbol。");
            Assert.IsTrue(symbols.All(s => !string.IsNullOrWhiteSpace(s)), "所有 symbol 都应为非空字符串。");

            // 常识性断言：Top50 通常包含 BTC/ETH
            CollectionAssert.Contains(symbols, "BTC", "Top 50 预期包含 BTC。");
            CollectionAssert.Contains(symbols, "ETH", "Top 50 预期包含 ETH。");
        }
    }
}
