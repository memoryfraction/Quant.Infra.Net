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
            // todo Dependency Inject IMapper
            // Register the Automapper to container
            _serviceCollection.AddSingleton<IMapper>(sp =>
            {
                var autoMapperConfiguration = new MapperConfiguration(cfg =>
                {
                    cfg.AddProfile<MappingProfile>();
                });
                return new Mapper(autoMapperConfiguration);
            });
            _serviceCollection.AddScoped<ISourceDataService, SourceDataService>();
            _serviceProvider = _serviceCollection.BuildServiceProvider();
        }

        

        [TestMethod]
        public void GetOhlcvs_Should_Work()
        {
            var mapper = _serviceProvider.GetRequiredService<IMapper>();
            var sourceDataService = _serviceProvider.GetRequiredService<ISourceDataService>();
            var ohlcvs = sourceDataService.GetOhlcvsAsync("BTC-USD", DateTime.UtcNow.AddYears(-1), DateTime.UtcNow).Result;
            Assert.IsNotNull(ohlcvs);
            Assert.IsTrue(ohlcvs.OhlcvList.Any());
        }
    }
}
