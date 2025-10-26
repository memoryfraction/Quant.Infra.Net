using AutoMapper;
using Binance.Net.Enums;
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
        private string _cmcApiKey,_cmcBaseUrl;

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
            _cmcBaseUrl = _configuration["CoinMarketCap:BaseUrl"].ToString();
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

            var symbols = await svc.GetTopMarketCapSymbolsFromCoinMarketCapAsync(_cmcApiKey, _cmcBaseUrl, count);

            // 基本断言
            Assert.IsNotNull(symbols, "返回结果不应为 null。");
            Assert.AreEqual(count, symbols.Count, $"应返回恰好 {count} 个 symbol。");
            Assert.IsTrue(symbols.All(s => !string.IsNullOrWhiteSpace(s)), "所有 symbol 都应为非空字符串。");

            // 常识性断言：Top50 通常包含 BTC/ETH
            CollectionAssert.Contains(symbols, "BTC", "Top 50 预期包含 BTC。");
            CollectionAssert.Contains(symbols, "ETH", "Top 50 预期包含 ETH。");
        }

        // Todo:
        // 增加单元测试Task DownloadBinanceSpotAsync(IEnumerable<string> symbols,  DateTime startDt, DateTime endDt, string path = "", KlineInterval klineInterval = KlineInterval.OneHour);
        // Task DownloadBinanceUsdFutureAsync(IEnumerable<string> symbols, DateTime startDt, DateTime endDt, string path = "", KlineInterval klineInterval = KlineInterval.OneHour);
        /// <summary>
        /// 验证能否下载指定的现货 (Spot) K线数据并成功保存到 CSV 文件。
        /// </summary>
        [TestMethod]
        public async Task DownloadBinanceSpotAsync_Should_Save_Files()
        {
            var svc = _serviceProvider.GetRequiredService<ICryptoSourceDataService>();

            // 安排：测试参数
            var symbols = await svc.GetTopMarketCapSymbolsFromCoinMarketCapAsync(_cmcApiKey, _cmcBaseUrl, 50);
            // 选择一个短的、可预测的日期范围（例如过去 7 天的日线数据）
            var endDt = DateTime.UtcNow.Date.AddDays(-1);
            var startDt = endDt.AddDays(-365);
            var interval = KlineInterval.OneDay;

            // 使用临时路径确保测试隔离
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Data\\Spot");

            try
            {
                // 执行
                await svc.DownloadBinanceSpotAsync(symbols, startDt, endDt, testDir, interval);

                // 断言：验证目录和文件
                Assert.IsTrue(Directory.Exists(testDir), "目标下载目录必须存在。");

                foreach (var symbol in symbols)
                {
                    string filePath = Path.Combine(testDir, $"{symbol}.csv");
                    Assert.IsTrue(File.Exists(filePath), $"文件 {filePath} 必须存在。");

                    // 验证文件内容（至少包含头行和一行数据）
                    var lines = await File.ReadAllLinesAsync(filePath);
                    Assert.IsTrue(lines.Length > 1, $"文件 {filePath} 必须包含数据（行数大于1）。");
                }
            }
            finally
            {
                // 清理：删除测试目录
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }

        /// <summary>
        /// 验证能否下载指定的 USD 永续合约 (UsdFuture) K线数据并成功保存到 CSV 文件。
        /// </summary>
        [TestMethod]
        public async Task DownloadBinanceUsdFutureAsync_Should_Save_Files()
        {
            var svc = _serviceProvider.GetRequiredService<ICryptoSourceDataService>();

            // 安排：测试参数
            var symbols = await svc.GetTopMarketCapSymbolsFromCoinMarketCapAsync(_cmcApiKey, _cmcBaseUrl, 50);
            // 选择一个短的、可预测的日期范围（例如过去 7 天的日线数据）
            var endDt = DateTime.UtcNow.Date.AddDays(-1);
            var startDt = endDt.AddDays(-7);
            var interval = KlineInterval.OneDay;

            // 使用临时路径确保测试隔离
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Data\\PerpetualContract");

            try
            {
                // 执行
                await svc.DownloadBinanceUsdFutureAsync(symbols, startDt, endDt, testDir, interval);

                // 断言：验证目录和文件
                Assert.IsTrue(Directory.Exists(testDir), "目标下载目录必须存在。");

                foreach (var symbol in symbols)
                {
                    string filePath = Path.Combine(testDir, $"{symbol}.csv");
                    Assert.IsTrue(File.Exists(filePath), $"文件 {filePath} 必须存在。");

                    // 验证文件内容（至少包含头行和一行数据）
                    var lines = await File.ReadAllLinesAsync(filePath);
                    Assert.IsTrue(lines.Length > 1, $"文件 {filePath} 必须包含数据（行数大于1）。");
                }
            }
            finally
            {
                // 清理：删除测试目录
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }


    }
}
