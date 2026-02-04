using AutoMapper;
using Binance.Net.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.Account.Service;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Broker.Service;
using Quant.Infra.Net.Notification.Service;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Tests
{
	[TestClass]
	public class BinanceUsdFutureTests
	{
		// IOC 相关容器
		private readonly ServiceCollection _services;
		private readonly ServiceProvider _serviceProvider;
		private readonly IConfigurationRoot _configuration;
		private readonly string _apiKey, _apiSecret;

		public BinanceUsdFutureTests()
		{
			// 1. 读取配置
			_configuration = new ConfigurationBuilder()
			   .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
			   .AddJsonFile("appsettings.json")
			   .AddUserSecrets<BinanceUsdFutureTests>() // 建议指向当前测试类
			   .Build();

			// 2. 初始化依赖注入
			_services = new ServiceCollection();

			_services.AddSingleton<IConfiguration>(_configuration);
			_services.AddScoped<IDingtalkService, DingtalkService>();
			_services.AddScoped<IBinanceOrderService, BinanceOrderService>();
			_services.AddScoped<BrokerServiceBase, BinanceService>();
			_services.AddScoped<IBinanceUsdFutureService, BinanceUsdFutureService>();

			// 3. 修复 AutoMapper：使用标准扩展方法注入
			// 这会自动扫描 MappingProfile 并处理其复杂的依赖关系
			_services.AddAutoMapper(cfg =>
			{
				cfg.AddProfile<MappingProfile>();
			}, typeof(MappingProfile).Assembly);

			_serviceProvider = _services.BuildServiceProvider();

			// 4. 获取 API 凭证
			_apiKey = _configuration["Exchange:apiKey"];
			_apiSecret = _configuration["Exchange:apiSecret"];
		}

		[TestMethod]
		public async Task ShowPositionMode_Should_Work()
		{
			var usdFutureService = _serviceProvider.GetRequiredService<IBinanceUsdFutureService>();
			await usdFutureService.ShowPositionModeAsync();
		}

		[TestMethod]
		public async Task GetBalanceAsync_Should_Work()
		{
			var usdFutureService = _serviceProvider.GetRequiredService<IBinanceUsdFutureService>();
			var balance = await usdFutureService.GetusdFutureAccountBalanceAsync();

			Console.WriteLine($"Current Balance: {balance}");
			// 根据实际账户情况调整断言值
			Assert.IsTrue(balance >= 0, "Balance should not be negative.");
		}

		[TestMethod]
		public async Task HasUsdFuturePositionAsync_Should_Work()
		{
			var usdFutureService = _serviceProvider.GetRequiredService<IBinanceUsdFutureService>();
			var result = await usdFutureService.HasUsdFuturePositionAsync("BTCUSDT");

			// 验证逻辑：如果您当前没开仓，则应为 false
			Assert.IsFalse(result);
		}

		[TestMethod]
		public async Task LiquidateUsdFutureAsync_Should_Work()
		{
			var usdFutureService = _serviceProvider.GetRequiredService<IBinanceUsdFutureService>();
			// 警告：这会平掉所有 BTCUSDT 仓位
			await usdFutureService.LiquidateUsdFutureAsync("BTCUSDT");
		}

		[TestMethod]
		public async Task SetUsdFutureLongHoldingsAsync_Should_Work()
		{
			var usdFutureService = _serviceProvider.GetRequiredService<IBinanceUsdFutureService>();
			var symbol = "BTCUSDT";

			// 开多仓 0.01 BTC
			await usdFutureService.SetUsdFutureHoldingsAsync(symbol, 0.01, PositionSide.Long);

			// 等待服务器处理及网络延迟
			await Task.Delay(5000);

			// 平仓
			await usdFutureService.LiquidateUsdFutureAsync(symbol);
		}

		[TestMethod]
		public async Task SetUsdFutureShortHoldingsAsync_Should_Work()
		{
			var usdFutureService = _serviceProvider.GetRequiredService<IBinanceUsdFutureService>();
			var symbol = "BTCUSDT";

			// 开空仓 0.01 BTC (注意：有些 Service 实现里负号表示方向，请确保与您的底层逻辑一致)
			await usdFutureService.SetUsdFutureHoldingsAsync(symbol, -0.01, PositionSide.Short);

			await Task.Delay(5000);
			await usdFutureService.LiquidateUsdFutureAsync(symbol);
		}

		[TestMethod]
		public async Task GetusdFutureUnrealizedProfitRateAsync_Should_Work()
		{
			var usdFutureService = _serviceProvider.GetRequiredService<IBinanceUsdFutureService>();
			var symbol1 = "BTCUSDT";
			var symbol2 = "ETHUSDT";

			// 测试对冲或多仓位未实现盈亏统计
			await usdFutureService.SetUsdFutureHoldingsAsync(symbol1, 0.01, PositionSide.Long);
			await usdFutureService.SetUsdFutureHoldingsAsync(symbol2, -0.01, PositionSide.Short);

			await Task.Delay(10000);

			var unrealizedProfitRate = await usdFutureService.GetusdFutureUnrealizedProfitRateAsync();
			Console.WriteLine($"Total Unrealized Profit Rate: {unrealizedProfitRate:P2}");

			// 清理测试仓位
			await usdFutureService.LiquidateUsdFutureAsync(symbol1);
			await usdFutureService.LiquidateUsdFutureAsync(symbol2);
		}
	}
}