using AutoMapper;
using Binance.Net.Clients;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.CommonObjects;
using CsvHelper.Configuration;
using CsvHelper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Notification.Service;
using System.Globalization;

namespace Quant.Infra.Net.Tests
{
    [TestClass]
    //[Ignore] // Binance blocks IP from China and US
    public class BinanceTests
    {

        // IOC
        private ServiceCollection _services;
        private string _apiKey, _apiSecret;
        private IConfigurationRoot _configuration;
        private ServiceProvider _serviceProvider;

        public BinanceTests() {
            // 依赖注入
            _services = new ServiceCollection();
            _services.AddScoped<IDingtalkService, DingtalkService>();
            _services.AddScoped<IBinanceService, BinanceService>();

            // Read Secret
            _configuration = new ConfigurationBuilder()
               .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
               .AddJsonFile("appsettings.json")
               .AddUserSecrets<BinanceTests>()
               .Build();

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


        #region Account

        [TestMethod]
        public async Task GetBinanceAccountSpotBalance_Should_Work()
        {
            Binance.Net.Clients.BinanceRestClient.SetDefaultOptions(options =>
            {
                options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
            });
            // 创建 Binance 客户端            
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                // 获取账户信息, 比如: usdt计价的余额
                var response = client.SpotApi.Account.GetBalancesAsync().Result;
                decimal totalUSDTBasedBalance = 0m;
                foreach (var token in response.Data)
                {
                    if (token.Asset == "USDT")
                    {
                        totalUSDTBasedBalance += token.Total;
                        continue;
                    }
                    String symbol = token.Asset + "USDT";
                    var symbolUSDTBasedPrice = client.SpotApi.ExchangeData.GetPriceAsync(symbol).Result.Data.Price;
                    totalUSDTBasedBalance += symbolUSDTBasedPrice * token.Total;
                }
                Console.WriteLine($"Total USDT Based Balance:{totalUSDTBasedBalance}");
                Assert.IsTrue(totalUSDTBasedBalance >= 0);
            }
        }

        [TestMethod]
        public async Task GetBinanceAccountFuturesBalance_Should_Work()
        {
            Binance.Net.Clients.BinanceRestClient.SetDefaultOptions(options =>
            {
                options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret); 
            });
            // 创建 Binance 客户端            
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                // 获取账户信息, 比如: usdt计价的余额
                var response = client.UsdFuturesApi.Account.GetBalancesAsync().Result;
                decimal totalUSDBasedBalance = 0m;
                foreach (var token in response.Data)
                {
                    totalUSDBasedBalance += token.WalletBalance;
                }
                Console.WriteLine($"Total USD Based Balance:{totalUSDBasedBalance}");
                Assert.IsTrue(totalUSDBasedBalance >= 0);
            }
        }


        /// <summary>
        /// Test to get history 5 years data; ip address need be outside of China, and US;
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task GetHistorySpotData_Should_Work()
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                // 设置获取历史数据的参数
                var symbol = "BTCUSDT"; // 交易对
                var interval = Binance.Net.Enums.KlineInterval.OneDay; // 时间间隔为一天
                var startTime = DateTime.UtcNow.AddDays(-30); // 从当前时间开始往前30天
                var endTime = DateTime.UtcNow; // 到当前时间

                // 获取历史K线数据
                var klinesResult = await client.SpotApi.ExchangeData.GetKlinesAsync(symbol, interval, startTime, endTime);

                Assert.IsTrue(klinesResult.Success);
                Assert.IsNotNull(klinesResult.Data);
                Assert.IsTrue(klinesResult.Data.Count() > 0);
            }
        }


        [TestMethod]
        public async Task GetPriceAsync()
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var priceResponse = await client.SpotApi.ExchangeData.GetPriceAsync("BTCUSDT");
                Console.WriteLine($"{priceResponse.Data.Symbol} : {priceResponse.Data.Price}");
                Assert.IsNotNull(priceResponse);
                Assert.IsTrue(priceResponse.Success);
                Assert.IsTrue(priceResponse.Data.Price>0);
            }
        }

        [TestMethod]
        public async Task GetSymbolList_Should_Work()
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var symbolList = await client.SpotApi.ExchangeData.GetExchangeInfoAsync();
                Assert.IsNotNull(symbolList);
                Assert.IsTrue(symbolList.Success);
                Assert.IsTrue(symbolList.Data.Symbols.Count() > 0);
            }
        }

        [TestMethod]
        public async Task SaveKlinesToCsv()
        {
            var symbol = "BTCUSDT";
            var path = AppDomain.CurrentDomain.BaseDirectory + "\\data\\spot\\";
            var interval = Binance.Net.Enums.KlineInterval.OneDay; // 时间间隔为一天
            var fileName = $"{symbol}.cvs";
            var fullPathFileName = Path.Combine(path, fileName);
            var endDt = DateTime.Now;
            var startDt = endDt.AddYears(-5);
            await SaveKlinesToCsv(symbol, interval, startDt,endDt,fullPathFileName);
        }


        public async Task SaveKlinesToCsv(string symbol, Binance.Net.Enums.KlineInterval interval, DateTime startDt, DateTime endDt, string fullPathFileName)
        {
            // 确保路径存在
            var directory = Path.GetDirectoryName(fullPathFileName);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            if (File.Exists(fullPathFileName))
                File.Delete(fullPathFileName);
            using (var client = new BinanceRestClient())
            {
                var klinesResult = await client.SpotApi.ExchangeData.GetKlinesAsync(symbol, interval, startDt, endDt); // 获取历史K线数据

                // Save klinesResult to fullPathFileName using csvHelper
                if (klinesResult.Success)
                {
                    using (var writer = new StreamWriter(fullPathFileName))
                    using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
                    {
                        // Write header
                        csv.WriteHeader<Kline>();
                        csv.NextRecord();

                        foreach (var line in klinesResult.Data)
                        {
                            csv.WriteRecord(new Kline
                            {
                                DateTime = line.OpenTime,
                                Open = line.OpenPrice,
                                High = line.HighPrice,
                                Low = line.LowPrice,
                                Close = line.ClosePrice,
                                Volume = line.Volume
                            });
                            csv.NextRecord();
                        }
                    }
                    Console.WriteLine($"Klines data saved successfully for {symbol}.");
                }
                else
                {
                    Console.WriteLine($"Error: {klinesResult.Error}");
                }
            }
        }

        #endregion


        #region Order Management
        [TestMethod]
        public async Task GetAllOpenOrderAsync_Should_Work()
        {
            // arrange

            // act
            var binanceOrderService = _serviceProvider.GetRequiredService<IBinanceService>();
            binanceOrderService.SetBinanceCredential(_apiKey, _apiSecret);
            var openOrders = await binanceOrderService.GetAllSpotOpenOrdersAsync();

            // assert
            Assert.IsNotNull(openOrders);
        }

        #endregion 
    }
}
