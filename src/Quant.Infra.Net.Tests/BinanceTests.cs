using AutoMapper;
using Binance.Net.Clients;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Account.Service;
using Quant.Infra.Net.Notification.Service;
using Quant.Infra.Net.Shared.Service;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Quant.Infra.Net.Tests
{
	[TestClass]
	public class BinanceTests
	{
		// IOC
		private ServiceCollection _services;
		private string _apiKey, _apiSecret;
		private IConfigurationRoot _configuration;
		private ServiceProvider _serviceProvider;

		public BinanceTests()
		{
			// 1. 依赖注入
			_services = new ServiceCollection();
			_services.AddScoped<IDingtalkService, DingtalkService>();
			_services.AddScoped<IBinanceOrderService, BinanceOrderService>();
			_services.AddScoped<BrokerServiceBase, BinanceService>();

			// 2. 读取配置
			_configuration = new ConfigurationBuilder()
			   .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
			   .AddJsonFile("appsettings.json")
			   .AddUserSecrets<BinanceTests>()
			   .Build();

			_services.AddSingleton<IConfiguration>(_configuration);

			// 3. 修复 AutoMapper 报错：使用扩展方法注入
			// 这会自动处理 MapperConfiguration 的创建并解决构造函数参数问题
			_services.AddAutoMapper(cfg =>
			{
				cfg.AddProfile<MappingProfile>();
			}, typeof(MappingProfile).Assembly);

			_serviceProvider = _services.BuildServiceProvider();

			_apiKey = _configuration["Exchange:apiKey"];
			_apiSecret = _configuration["Exchange:apiSecret"];
		}

		#region Account

		[TestMethod]
		public async Task GetBinanceSpotAccountSpotBalance_Should_Work()
		{
			BinanceRestClient.SetDefaultOptions(options =>
			{
				options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
			});

			using (var client = new BinanceRestClient())
			{
				// 已将 .Result 改为 await
				var response = await client.SpotApi.Account.GetBalancesAsync();
				decimal totalUSDTBasedBalance = 0m;

				foreach (var token in response.Data)
				{
					if (token.Asset == "USDT")
					{
						totalUSDTBasedBalance += token.Total;
						continue;
					}
					string symbol = token.Asset + "USDT";
					var priceResponse = await client.SpotApi.ExchangeData.GetPriceAsync(symbol);

					if (priceResponse.Success)
					{
						totalUSDTBasedBalance += priceResponse.Data.Price * token.Total;
					}
				}
				Console.WriteLine($"Total USDT Based Balance:{totalUSDTBasedBalance}");
				Assert.IsTrue(totalUSDTBasedBalance >= 0);
			}
		}

		[TestMethod]
		public async Task GetMarginAccountEquity_Should_Work()
		{
			BinanceRestClient.SetDefaultOptions(options =>
			{
				options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
			});

			using (var client = new BinanceRestClient())
			{
				// 已将 .Result 改为 await
				var marginAccountResponse = await client.SpotApi.Account.GetPortfolioMarginAccountInfoAsync();
				Console.WriteLine($"Total USDT Based Balance:{marginAccountResponse.Data.AccountEquity}");
				Assert.IsTrue(marginAccountResponse.Data.AccountEquity >= 0);
			}
		}

		[TestMethod]
		public async Task GetHistorySpotData_Should_Work()
		{
			using (var client = new BinanceRestClient())
			{
				var symbol = "BTCUSDT";
				var interval = Binance.Net.Enums.KlineInterval.OneDay;
				var startTime = DateTime.UtcNow.AddDays(-30);
				var endTime = DateTime.UtcNow;

				var klinesResult = await client.SpotApi.ExchangeData.GetKlinesAsync(symbol, interval, startTime, endTime);

				Assert.IsTrue(klinesResult.Success);
				Assert.IsNotNull(klinesResult.Data);
				Assert.IsTrue(klinesResult.Data.Any());
			}
		}

		[TestMethod]
		public async Task GetPriceAsync()
		{
			using (var client = new BinanceRestClient())
			{
				var priceResponse = await client.SpotApi.ExchangeData.GetPriceAsync("BTCUSDT");
				Console.WriteLine($"{priceResponse.Data.Symbol} : {priceResponse.Data.Price}");
				Assert.IsNotNull(priceResponse);
				Assert.IsTrue(priceResponse.Success);
				Assert.IsTrue(priceResponse.Data.Price > 0);
			}
		}

		[TestMethod]
		public async Task GetSpotSymbolListAsync_Should_Work()
		{
			var binanceOrderService = _serviceProvider.GetRequiredService<BrokerServiceBase>();
			var spotSymbolList = await binanceOrderService.GetSpotSymbolListAsync();
			Assert.IsNotNull(spotSymbolList);
		}

		[TestMethod]
		public async Task SaveKlinesToCsv()
		{
			var symbol = "BTCUSDT";
			var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "spot");
			if (!Directory.Exists(path)) Directory.CreateDirectory(path);

			var interval = Binance.Net.Enums.KlineInterval.OneDay;
			var fileName = $"{symbol}.csv"; // 修正了拼写错误 .cvs -> .csv
			var fullPathFileName = Path.Combine(path, fileName);
			var endDt = DateTime.Now;
			var startDt = endDt.AddYears(-5);

			await UtilityService.SaveOhlcvsToCsv(symbol, interval, startDt, endDt, fullPathFileName);
		}
		#endregion

		#region Order Management
		[TestMethod]
		public async Task GetAllOpenOrderAsync_Should_Work()
		{
			var binanceOrderService = _serviceProvider.GetRequiredService<IBinanceOrderService>();
			binanceOrderService.SetBinanceCredential(_apiKey, _apiSecret);
			var openOrders = await binanceOrderService.GetAllSpotOpenOrdersAsync();
			Assert.IsNotNull(openOrders);
		}
		#endregion

		#region UsdFuture

		[TestMethod]
		public async Task GetBinanceAccountFuturesBalance_Should_Work()
		{
			BinanceRestClient.SetDefaultOptions(options =>
			{
				options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
			});

			using (var client = new BinanceRestClient())
			{
				var response = await client.UsdFuturesApi.Account.GetBalancesAsync();
				decimal totalUSDBasedBalance = 0m;
				foreach (var token in response.Data)
				{
					totalUSDBasedBalance += token.WalletBalance;
				}
				Console.WriteLine($"Total USD Based Balance:{totalUSDBasedBalance}");
				Assert.IsTrue(totalUSDBasedBalance >= 0);
			}
		}

		[TestMethod]
		public async Task GetUsdFutureAccountBalance_Should_Work()
		{
			BinanceRestClient.SetDefaultOptions(options =>
			{
				options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
			});

			using (var client = new BinanceRestClient())
			{
				var accountInfoV3 = await client.UsdFuturesApi.Account.GetAccountInfoV3Async();
				var balance = accountInfoV3.Data.AvailableBalance;
				Console.WriteLine($"UsdFuturesApi Available Balance: {balance}.");
				Assert.IsTrue(balance >= 0);
			}
		}

		[TestMethod]
		public async Task OpenAndExit_Should_Work()
		{
			BinanceRestClient.SetDefaultOptions(options =>
			{
				options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
			});

			using (var client = new BinanceRestClient())
			{
				// 1. 开空仓
				await client.UsdFuturesApi.Trading.PlaceOrderAsync(
				   symbol: "ALGOUSDT",
				   side: Binance.Net.Enums.OrderSide.Sell,
				   type: Binance.Net.Enums.FuturesOrderType.Market,
				   quantity: 40,
				   positionSide: Binance.Net.Enums.PositionSide.Short
				);

				// 2. 获取当前持仓
				var position = await client.UsdFuturesApi.Account.GetPositionInformationAsync();
				var algoPosition = position.Data.FirstOrDefault(x => x.Symbol == "ALGOUSDT" && x.Quantity != 0);

				// 3. 关空仓
				if (algoPosition != null)
				{
					await client.UsdFuturesApi.Trading.PlaceOrderAsync(
					   symbol: "ALGOUSDT",
					   side: Binance.Net.Enums.OrderSide.Buy,
					   type: Binance.Net.Enums.FuturesOrderType.Market,
					   quantity: Math.Abs(algoPosition.Quantity),
					   positionSide: Binance.Net.Enums.PositionSide.Short
					);
				}
			}
		}

		[TestMethod]
		public async Task Calculate_UnrealizedProfit_Should_Work()
		{
			BinanceRestClient.SetDefaultOptions(options =>
			{
				options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
			});

			using (var client = new BinanceRestClient())
			{
				var account = await client.UsdFuturesApi.Account.GetAccountInfoV3Async();
				var balance = account.Data.AvailableBalance;
				Console.WriteLine($"UsdFuturesApi Available Balance: {balance}.");
				Assert.IsTrue(balance >= 0);
			}
		}
		#endregion
	}
}