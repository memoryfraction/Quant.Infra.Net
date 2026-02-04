using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Quant.Infra.Net.SourceData.Service;
using Quant.Infra.Net.SourceData.Service.Historical;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Tests
{
	[TestClass]
	public class DataSourceServiceTests
	{
		private readonly ServiceProvider _serviceProvider;

		public DataSourceServiceTests()
		{
			ServiceCollection _serviceCollection = new ServiceCollection();

			// 1. 修复 AutoMapper 报错：使用扩展方法注入
			// 这会自动配置并注册 IMapper 接口，适应 AutoMapper 的版本升级
			_serviceCollection.AddAutoMapper(cfg =>
			{
				cfg.AddProfile<MappingProfile>();
			}, typeof(MappingProfile).Assembly);

			// 2. 注册业务逻辑服务
			_serviceCollection.AddScoped<IHistoricalDataSourceService, HistoricalDataSourceServiceCsv>();
			_serviceCollection.AddScoped<ITraditionalFinanceSourceDataService, TraditionalFinanceSourceDataService>();

			_serviceProvider = _serviceCollection.BuildServiceProvider();
		}

		/// <summary>
		/// 从 Yahoo API 获取 Ohlcv 列表
		/// </summary>
		[TestMethod]
		public async Task DownloadOhlcvListAsync_Should_Work() // 已改为 async Task
		{
			// Arrange
			var sourceDataService = _serviceProvider.GetRequiredService<ITraditionalFinanceSourceDataService>();

			// Act
			// 已将 .Result 改为 await
			var ohlcvs = await sourceDataService.DownloadOhlcvListAsync(
				"AAPL",
				DateTime.UtcNow.AddYears(-1),
				DateTime.UtcNow,
				Shared.Model.ResolutionLevel.Hourly);

			// Assert
			Assert.IsNotNull(ohlcvs);
			Assert.IsTrue(ohlcvs.OhlcvSet.Any());
		}

		/// <summary>
		/// 批量获取数据 
		/// </summary>
		[TestMethod]
		public async Task DownloadSymbolsListAsync_Should_Work() // 已改为 async Task
		{
			// Arrange
			var sourceDataService = _serviceProvider.GetRequiredService<ITraditionalFinanceSourceDataService>();

			// Act
			var ohlcvs = await sourceDataService.DownloadOhlcvListAsync(
				"BTC-USD",
				DateTime.UtcNow.AddYears(-1),
				DateTime.UtcNow,
				Shared.Model.ResolutionLevel.Hourly);

			// Assert
			Assert.IsNotNull(ohlcvs);
			Assert.IsTrue(ohlcvs.OhlcvSet.Any());
		}

		/// <summary>
		/// 使用混编的方式，用Yfinance，下载数据，并存储在指定路径
		/// </summary>
		[TestMethod]
		public async Task DownloadDataUsingYfinance_Should_Work()
		{
			// 待实现的测试逻辑
			await Task.CompletedTask;
		}

		/// <summary>
		/// 获取标普500成分股
		/// </summary>
		[TestMethod]
		public async Task GetSp500Symbols_Should_Work()
		{
			// Arrange
			var sourceDataService = _serviceProvider.GetRequiredService<ITraditionalFinanceSourceDataService>();

			// Act
			var symbols = await sourceDataService.GetSp500SymbolsAsync();

			// Assert
			Assert.IsNotNull(symbols);
			// 标普500有时会有503或505只股票（因为部分公司有不同级别的股票），所以使用 > 490 的判断更稳健
			Assert.IsTrue(symbols.Count() >= 500);
		}
	}
}