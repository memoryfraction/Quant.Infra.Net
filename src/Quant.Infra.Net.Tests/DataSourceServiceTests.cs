using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.SourceData.Service;

namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class DataSourceServiceTests
    {
        private readonly ServiceProvider _serviceProvider;
        public DataSourceServiceTests()
        {
            ServiceCollection _serviceCollection = new ServiceCollection ();
            // Register the Automapper to container
            _serviceCollection.AddSingleton<IMapper>(sp =>
            {
                var autoMapperConfiguration = new MapperConfiguration(cfg =>
                {
                    cfg.AddProfile<MappingProfile>();
                });
                return new Mapper(autoMapperConfiguration);
            });
            _serviceCollection.AddScoped<ITraditionalFinanceSourceDataService, TraditionalFinanceSourceDataService>();
            _serviceProvider = _serviceCollection.BuildServiceProvider();
        }

        
        /// <summary>
        /// Get ohlcvs from Yahoo API 
        /// </summary>
        [TestMethod]
        public void DownloadOhlcvListAsync_Should_Work()
        {
            var mapper = _serviceProvider.GetRequiredService<IMapper>();
            var sourceDataService = _serviceProvider.GetRequiredService<ITraditionalFinanceSourceDataService>();
            var ohlcvs = sourceDataService.DownloadOhlcvListAsync("BTC-USD", DateTime.UtcNow.AddYears(-1), DateTime.UtcNow,Shared.Model.ResolutionLevel.Hourly).Result;
            Assert.IsNotNull(ohlcvs);
            Assert.IsTrue(ohlcvs.OhlcvList.Any());
        }

        /// <summary>
        /// 使用混编的方式，用Yfinance，下载数据，并存储在指定路径
        /// </summary>
        [TestMethod]
        public void DownloadDataUsingYfinance_Should_Work()
        {
                 
        }


        /// <summary>
        /// Get symbols from sp500
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task GetSp500Symbols_Should_Work()
        {
            var sourceDataService = _serviceProvider.GetRequiredService<ITraditionalFinanceSourceDataService>();
            var symbols = await sourceDataService.GetSp500SymbolsAsync();
            Assert.IsNotNull(symbols);
            Assert.IsTrue(symbols.Count() == 500);
        }

    }
}
