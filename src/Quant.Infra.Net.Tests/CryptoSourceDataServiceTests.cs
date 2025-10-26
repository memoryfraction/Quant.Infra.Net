using AutoMapper;
using Binance.Net.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Shared.Service;
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


        /// <summary>
        /// 验证能否下载指定的现货 (Spot) K线数据并成功保存到 CSV 文件。
        /// </summary>
        [TestMethod]
        public async Task DownloadBinanceSpotAsync_Should_Save_Files()
        {
            var svc = _serviceProvider.GetRequiredService<ICryptoSourceDataService>();

            // 安排：测试参数
            var symbols = await svc.GetTopMarketCapSymbolsFromCoinMarketCapAsync(_cmcApiKey, _cmcBaseUrl, 50);
            symbols = symbols
                .Where(s => !string.IsNullOrWhiteSpace(s) && !s.Equals("USDT", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Trim().ToUpperInvariant())
                .Select(s => s.EndsWith("USDT", StringComparison.Ordinal) ? s : s + "USDT")
                .Distinct()
                .ToList();

            // 选择一个短的、可预测的日期范围（例如过去 7 天的日线数据）
            var endDt = DateTime.UtcNow.Date.AddDays(-1);
            var startDt = endDt.AddDays(-365);
            var interval = KlineInterval.OneDay;

            // 使用临时路径确保测试隔离
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Data\\Spot");
            await UtilityService.IsPathExistAsync(testDir);
            // 如果存在就整目录删掉再重建（最省心）
            if (Directory.Exists(testDir))
            {
                // 防止只读属性阻碍删除
                foreach (var f in Directory.EnumerateFiles(testDir, "*", SearchOption.AllDirectories))
                    File.SetAttributes(f, FileAttributes.Normal);

                Directory.Delete(testDir, recursive: true);
            }
            await UtilityService.IsPathExistAsync(testDir);

            // 执行
            await svc.DownloadBinanceSpotAsync(symbols, startDt, endDt, testDir, interval); // 50个symbol为什么，只下载了9个csv？

            // 断言：验证目录和文件
            Assert.IsTrue(Directory.Exists(testDir), "目标下载目录必须存在。");

            // 有 > 30个文件，每个文件有10行以上数据，就正确; 检查文件数量和内容;
            var files = Directory.GetFiles(testDir, "*.csv");
            Assert.IsTrue(files.Length > 30, $"生成的 CSV 文件数量 ({files.Length}) 应超过 30 个。");

            var failures = new List<string>();
            foreach (var symbol in symbols)
            {
                try
                {
                    var filePath = Path.Combine(testDir, $"{symbol}.csv");
                    if (!File.Exists(filePath))
                    {
                        failures.Add($"{symbol}: 文件不存在 -> {filePath}");
                        continue;
                    }
                    var lines = await File.ReadAllLinesAsync(filePath);
                    if (lines.Length < 10)
                    {
                        failures.Add($"{symbol}: 文件行数 ({lines.Length}) 小于 10 行");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{symbol}: 校验时异常 - {ex.Message}");
                }
            }

            if (failures.Count > 0)
            {
                Assert.Fail($"以下 symbols 校验失败（共 {failures.Count} 个）:\n" + string.Join("\n", failures));
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
            symbols = symbols
                .Where(s => !string.IsNullOrWhiteSpace(s) && !s.Equals("USDT", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Trim().ToUpperInvariant())
                .Select(s => s.EndsWith("USDT", StringComparison.Ordinal) ? s : s + "USDT")
                .Distinct()
                .ToList();

            // 选择一个短的、可预测的日期范围（例如过去 7 天的日线数据）
            var endDt = DateTime.UtcNow.Date.AddDays(-1);
            var startDt = endDt.AddDays(-365);
            var interval = KlineInterval.OneDay;

            // 使用临时路径确保测试隔离
            string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"Data\\PerpetualContract");
            if(!Directory.Exists(testDir))
                Directory.CreateDirectory(testDir);

            // 如果存在就整目录删掉再重建（最省心）
            if (Directory.Exists(testDir))
            {
                // 防止只读属性阻碍删除
                foreach (var f in Directory.EnumerateFiles(testDir, "*", SearchOption.AllDirectories))
                    File.SetAttributes(f, FileAttributes.Normal);

                Directory.Delete(testDir, recursive: true);
            }

            if (!Directory.Exists(testDir))
                Directory.CreateDirectory(testDir);

            // 执行
            await svc.DownloadBinanceUsdFutureAsync(symbols, startDt, endDt, testDir, interval);

            // 断言：验证目录和文件
            Assert.IsTrue(Directory.Exists(testDir), "目标下载目录必须存在。");

            // 有 > 30个文件，每个文件有10行以上数据，就正确
            // 检查文件数量和内容
            var files = Directory.GetFiles(testDir, "*.csv");
            Assert.IsTrue(files.Length >= 25, $"生成的 CSV 文件数量 ({files.Length}) 应超过 25 个。");

            var failures = new List<string>();
            foreach (var symbol in symbols)
            {
                try
                {
                    var filePath = Path.Combine(testDir, $"{symbol}.csv");
                    if (!File.Exists(filePath))
                    {
                        failures.Add($"{symbol}: 文件不存在 -> {filePath}");
                        continue;
                    }
                    var lines = await File.ReadAllLinesAsync(filePath);
                    if (lines.Length < 10)
                    {
                        failures.Add($"{symbol}: 文件行数 ({lines.Length}) 小于 10 行");
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    failures.Add($"{symbol}: 校验时异常 - {ex.Message}");
                }
            }

            if (failures.Count > 0)
            {
                Assert.Fail($"以下 symbols 校验失败（共 {failures.Count} 个）:\n" + string.Join("\n", failures));
            }
        }


        /// <summary>
        /// 验证能否从 Binance 获取所有现货 (Spot) 交易对符号列表。
        /// </summary>
        [TestMethod]
        public async Task GetAllBinanceSpotSymbolsAsync_Should_Return_Valid_List()
        {
            var svc = _serviceProvider.GetRequiredService<ICryptoSourceDataService>();
            // 执行
            var symbols = await svc.GetAllBinanceSpotSymbolsAsync();
            // 基本断言
            Assert.IsNotNull(symbols, "返回结果不应为 null。");
            Assert.IsTrue(symbols.Count > 0, "符号列表不应为空。");
            Assert.IsTrue(symbols.Count > 1000, "Binance Spot 符号数量应超过 1000 个（基于当前市场规模）。");
            Assert.IsTrue(symbols.All(s => !string.IsNullOrWhiteSpace(s)), "所有 symbol 都应为非空字符串。");
            Assert.IsTrue(symbols.All(s => s.Length >= 4 && s.Length <= 20), "每个 symbol 长度应在合理范围内（例如 BTCUSDT）。");
            // 常识性断言：预期包含常见交易对
            CollectionAssert.Contains(symbols, "BTCUSDT", "Spot 列表预期包含 BTCUSDT。");
            CollectionAssert.Contains(symbols, "ETHUSDT", "Spot 列表预期包含 ETHUSDT。");
            // 验证无重复
            Assert.AreEqual(symbols.Count, symbols.Distinct().Count(), "符号列表不应有重复项。");
        }


        /// <summary>
        /// 验证能否从 Binance 获取所有 USD 永续合约 (UsdFuture) 交易对符号列表。
        /// </summary>
        [TestMethod]
        public async Task GetAllBinanceUsdFutureSymbolsAsync_Should_Return_Valid_List()
        {
            var svc = _serviceProvider.GetRequiredService<ICryptoSourceDataService>();
            // 执行
            var symbols = await svc.GetAllBinanceUsdFutureSymbolsAsync();
            // 基本断言
            Assert.IsNotNull(symbols, "返回结果不应为 null。");
            Assert.IsTrue(symbols.Count > 0, "符号列表不应为空。");
            Assert.IsTrue(symbols.Count > 200, "Binance USD Futures 符号数量应超过 200 个（基于当前市场规模）。");
            Assert.IsTrue(symbols.All(s => !string.IsNullOrWhiteSpace(s)), "所有 symbol 都应为非空字符串。");
            // Assert.IsTrue(symbols.All(s => s.EndsWith("USDT", StringComparison.OrdinalIgnoreCase)), "USD Futures 符号应以 USDT 结尾。");
            Assert.IsTrue(symbols.All(s => s.Length >= 4 && s.Length <= 20), "每个 symbol 长度应在合理范围内（例如 BTCUSDT）。");
            // 常识性断言：预期包含常见交易对
            CollectionAssert.Contains(symbols, "BTCUSDT", "USD Futures 列表预期包含 BTCUSDT。");
            CollectionAssert.Contains(symbols, "ETHUSDT", "USD Futures 列表预期包含 ETHUSDT。");
            // 验证无重复
            Assert.AreEqual(symbols.Count, symbols.Distinct().Count(), "符号列表不应有重复项。");
        }

    }
}
