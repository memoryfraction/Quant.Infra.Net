using AutoMapper;
using Binance.Net.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Shared.Service;
using Quant.Infra.Net.SourceData.Service;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Tests
{
	[TestClass]
	public class CryptoSourceDataServiceTests
	{
		private readonly ServiceProvider _serviceProvider;
		private readonly IConfigurationRoot _configuration;
		private readonly string _cmcApiKey, _cmcBaseUrl;

		public CryptoSourceDataServiceTests()
		{
			var serviceCollection = new ServiceCollection();

			// 1. 修复 AutoMapper 注入：使用标准扩展方法
			// 解决手动实例化时构造函数参数不匹配的问题
			serviceCollection.AddAutoMapper(cfg =>
			{
				cfg.AddProfile<MappingProfile>();
			}, typeof(MappingProfile).Assembly);

			// 2. 注入业务服务
			serviceCollection.AddScoped<ICryptoSourceDataService, CryptoSourceDataService>();

			// 3. 读取配置
			_configuration = new ConfigurationBuilder()
			   .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
			   .AddJsonFile("appsettings.json", optional: true)
			   .AddUserSecrets<CryptoSourceDataServiceTests>()
			   .Build();

			serviceCollection.AddSingleton<IConfiguration>(_configuration);

			_serviceProvider = serviceCollection.BuildServiceProvider();

			// 获取凭据（处理可能的空引用）
			_cmcApiKey = _configuration["CoinMarketCap:ApiKey"] ?? string.Empty;
			_cmcBaseUrl = _configuration["CoinMarketCap:BaseUrl"] ?? "https://pro-api.coinmarketcap.com";
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

			Assert.IsNotNull(symbols, "返回结果不应为 null。");
			Assert.AreEqual(count, symbols.Count, $"应返回恰好 {count} 个 symbol。");
			Assert.IsTrue(symbols.All(s => !string.IsNullOrWhiteSpace(s)), "所有 symbol 都应为非空字符串。");
			CollectionAssert.Contains(symbols, "BTC", "Top 50 预期包含 BTC。");
			CollectionAssert.Contains(symbols, "ETH", "Top 50 预期包含 ETH。");
		}

		/// <summary>
		/// 验证能否下载指定的现货 (Spot) K线数据并成功保存。
		/// </summary>
		[TestMethod]
		public async Task DownloadBinanceSpotAsync_Should_Save_Files()
		{
			var svc = _serviceProvider.GetRequiredService<ICryptoSourceDataService>();

			// 1. 获取并处理 Symbols
			var rawSymbols = await svc.GetTopMarketCapSymbolsFromCoinMarketCapAsync(_cmcApiKey, _cmcBaseUrl, 50);
			var symbols = rawSymbols
				.Where(s => !string.IsNullOrWhiteSpace(s) && !s.Equals("USDT", StringComparison.OrdinalIgnoreCase))
				.Select(s => s.Trim().ToUpperInvariant())
				.Select(s => s.EndsWith("USDT", StringComparison.Ordinal) ? s : s + "USDT")
				.Distinct()
				.ToList();

			var endDt = DateTime.UtcNow.Date.AddDays(-1);
			var startDt = endDt.AddDays(-365);
			var interval = KlineInterval.OneDay;

			// 2. 准备目录
			string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Spot");
			if (Directory.Exists(testDir))
			{
				foreach (var f in Directory.EnumerateFiles(testDir, "*.csv", SearchOption.TopDirectoryOnly))
				{
					File.SetAttributes(f, FileAttributes.Normal);
					File.Delete(f);
				}
			}
			else
			{
				Directory.CreateDirectory(testDir);
			}

			// 3. 执行下载
			// 关于“只下载了9个”的可能原因：
			// A. Binance 不支持某些 CMC 返回的代币（如法币、封装币）。
			// B. 网络请求频率限制（Rate Limit）。
			// C. 内部逻辑过滤。
			await svc.DownloadBinanceSpotAsync(symbols, startDt, endDt, testDir, interval);

			// 4. 断言验证
			Assert.IsTrue(Directory.Exists(testDir), "目标下载目录必须存在。");
			var files = Directory.GetFiles(testDir, "*.csv");

			// 调整阈值：CMC 前 50 在 Binance 通常有 35-45 个活跃交易对
			Assert.IsTrue(files.Length >= 30, $"生成的 CSV 文件数量 ({files.Length}) 过少，请检查 Binance 是否支持这些交易对。");

			var failures = new List<string>();
			foreach (var file in files)
			{
				var lines = await File.ReadAllLinesAsync(file);
				if (lines.Length < 10)
				{
					failures.Add($"{Path.GetFileName(file)}: 数据行数不足 ({lines.Length})");
				}
			}

			if (failures.Any())
			{
				Assert.Fail("部分 CSV 文件内容校验失败:\n" + string.Join("\n", failures));
			}
		}

		[TestMethod]
		public async Task DownloadBinanceUsdFutureAsync_Should_Save_Files()
		{
			var svc = _serviceProvider.GetRequiredService<ICryptoSourceDataService>();

			var rawSymbols = await svc.GetTopMarketCapSymbolsFromCoinMarketCapAsync(_cmcApiKey, _cmcBaseUrl, 50);
			var symbols = rawSymbols
				.Where(s => !string.IsNullOrWhiteSpace(s))
				.Select(s => s.Trim().ToUpperInvariant())
				.Select(s => s.EndsWith("USDT", StringComparison.Ordinal) ? s : s + "USDT")
				.Distinct()
				.ToList();

			var endDt = DateTime.UtcNow.Date.AddDays(-1);
			var startDt = endDt.AddDays(-365);
			var interval = KlineInterval.OneDay;

			string testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "PerpetualContract");
			if (!Directory.Exists(testDir)) Directory.CreateDirectory(testDir);

			// 清理旧文件
			foreach (var f in Directory.GetFiles(testDir, "*.csv")) File.Delete(f);

			await svc.DownloadBinanceUsdFutureAsync(symbols, startDt, endDt, testDir, interval);

			var files = Directory.GetFiles(testDir, "*.csv");
			// 合约交易对通常比现货少一些
			Assert.IsTrue(files.Length >= 25, $"合约文件数量 ({files.Length}) 不达标。");
		}

		[TestMethod]
		public async Task GetAllBinanceSpotSymbolsAsync_Should_Return_Valid_List()
		{
			var svc = _serviceProvider.GetRequiredService<ICryptoSourceDataService>();
			var symbols = await svc.GetAllBinanceSpotSymbolsAsync();

			Assert.IsNotNull(symbols);
			Assert.IsTrue(symbols.Count > 1000, "现货符号数量异常。");
			CollectionAssert.Contains(symbols.Select(s => s.ToUpper()).ToList(), "BTCUSDT");
		}

		[TestMethod]
		public async Task GetAllBinanceUsdFutureSymbolsAsync_Should_Return_Valid_List()
		{
			var svc = _serviceProvider.GetRequiredService<ICryptoSourceDataService>();
			var symbols = await svc.GetAllBinanceUsdFutureSymbolsAsync();

			Assert.IsNotNull(symbols);
			Assert.IsTrue(symbols.Count > 200, "合约符号数量异常。");
			CollectionAssert.Contains(symbols.Select(s => s.ToUpper()).ToList(), "BTCUSDT");
		}
	}
}