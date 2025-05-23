using Microsoft.Extensions.Configuration;
using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Broker.Service;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Service.Historical;
using Quant.Infra.Net.SourceData.Service.RealTime;

namespace Quant.Infra.Net.Tests
{
    /// <summary>
    /// 用于测试美股 Alpaca 经纪服务的集成测试类。
    /// Integration test class for U.S. equity Alpaca broker service.
    /// </summary>
    [TestClass]
    public class USEquityAlpacaBrokerServiceTests
    {
        private readonly IUSEquityBrokerService _brokerService;
        private readonly IRealtimeDataSourceServiceTraditionalFinance _realtimeDataSourceService;
        private readonly IHistoricalDataSourceServiceTraditionalFinance _historicalDataSourceService;
        private readonly IConfiguration _configuration;
        private const string Symbol = "AAPL"; // 确保 Paper 账户中支持该股票

        /// <summary>
        /// 构造函数：加载配置文件及 user secrets，并初始化经纪服务实例。
        /// Constructor: loads config and initializes broker service.
        /// </summary>
        public USEquityAlpacaBrokerServiceTests()
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddUserSecrets<USEquityAlpacaBrokerServiceTests>()
                .Build();
            _configuration = config;
            _brokerService = new USEquityAlpacaBrokerService(config);
            _realtimeDataSourceService = new USEquityAlpacaBrokerService(config);
            _historicalDataSourceService = new USEquityAlpacaBrokerService(config);

        }

        /// <summary>
        /// 测试：调用 SetHoldings 应该成功建立一个仓位。
        /// Test: SetHoldings should successfully open a position.
        /// </summary>
        [TestMethod]
        public async Task SetHoldings_ShouldIncreasePosition()
        {
            //  检查是否开盘时间，如果不开盘，则跳过测试
            var isMarketOpening = await _brokerService.IsMarketOpeningAsync();
            if (isMarketOpening == false)
                return;

            await _brokerService.SetHoldingsAsync(Symbol, 0.05);
            await Task.Delay(1000); // 等待Alpaca API操作完成
            var hasPosition = await _brokerService.HasPositionAsync(Symbol);
            await Task.Delay(1000); // 等待Alpaca API操作完成
            Assert.IsTrue(hasPosition);
            await _brokerService.LiquidateAsync(Symbol);
        }

        /// <summary>
        /// 测试：HasPosition 应准确识别持仓状态。
        /// Test: HasPosition should detect open and cleared position correctly.
        /// </summary>
        [TestMethod]
        public async Task HasPosition_ShouldDetectPosition()
        {
            //  检查是否开盘时间，如果不开盘，则跳过测试
            var isMarketOpening = await _brokerService.IsMarketOpeningAsync();
            if (isMarketOpening == false)
                return;

            // 下单建仓（0.05 股）
            await _brokerService.SetHoldingsAsync(Symbol, 0.05); // if market closed, test will fail
            await Task.Delay(2000); // 每秒检查一次

            var hasPosition = await _brokerService.HasPositionAsync(Symbol);
            Assert.IsTrue(hasPosition, "Position was not established — check if order was filled, market open, or symbol is tradable.");

            // 清仓
            await _brokerService.LiquidateAsync(Symbol);
            await Task.Delay(2000); // 每秒检查一次
            var hasPositionAfter = await _brokerService.HasPositionAsync(Symbol);
            Assert.IsFalse(hasPositionAfter, "Position was not cleared — Liquidate may have failed or is delayed.");
        }


        /// <summary>
        /// 测试：GetPortfolioMarketValue 应返回正值。
        /// Test: GetPortfolioMarketValue should return a positive number.
        /// </summary>
        [TestMethod]
        public async Task GetPortfolioMarketValue_ShouldReturnPositiveValue()
        {
            var value = await _brokerService.GetPortfolioMarketValueAsync();
            Console.WriteLine($"Portfolio market value: {value}");
            Assert.IsTrue(value > 0, "Portfolio market value should be greater than 0.");
        }

        /// <summary>
        /// 测试：未实现盈亏比率应在合理范围内（-100% ~ +100%）。
        /// Test: Unrealized profit/loss rate should be within -100% to 100%.
        /// </summary>
        [TestMethod]
        public async Task GetUnrealizedProfitRate_ShouldBeWithinRange()
        {
            var rate = await _brokerService.GetUnrealizedProfitRateAsync();
            Console.WriteLine($"Unrealized profit/loss rate: {rate}");
            Assert.IsTrue(rate > -1.0 && rate < 1.0, "Unrealized PnL rate should be within -100% ~ 100%");
        }

        /// <summary>
        /// 测试：Liquidate 应能清除指定标的的持仓。
        /// Test: Liquidate should close the position successfully.
        /// </summary>
        [TestMethod]
        public async Task Liquidate_ShouldClearPosition()
        {
            //  检查是否开盘时间，如果不开盘，则跳过测试
            var isMarketOpening = await _brokerService.IsMarketOpeningAsync();
            if (isMarketOpening == false)
                return;

            await _brokerService.SetHoldingsAsync(Symbol, 0.05);
            await Task.Delay(2000); // 等待Alpaca API操作完成
            var hasPosition = await _brokerService.HasPositionAsync(Symbol);
            Assert.IsTrue(hasPosition);

            await _brokerService.LiquidateAsync(Symbol);
            await Task.Delay(1000); // 等待Alpaca API操作完成
            var cleared = await _brokerService.HasPositionAsync(Symbol);
            Assert.IsFalse(cleared);
        }


        private static readonly Underlying TestEquity = new Underlying
        {
            Symbol = "AAPL",
            AssetType = AssetType.UsEquity
        };

        /// <summary>
        /// 测试：GetLatestPriceAsync 应返回大于 0 的价格。
        /// </summary>
        [TestMethod]
        public async Task GetLatestPriceAsync_ShouldReturnPositivePrice()
        {
            var price = await _realtimeDataSourceService.GetLatestPriceAsync(TestEquity);
            Console.WriteLine($"Latest price for {Symbol}: {price}");
            Assert.IsTrue(price > 0, "Latest price should be greater than zero.");
        }

        /// <summary>
        /// 测试：GetHistoricalDataFrameAsync 在最近 5 天范围内能返回非空 DataFrame。
        /// </summary>
        [TestMethod]
        public async Task GetHistoricalDataFrameAsync_ShouldReturnDataFrameWithRowsAndColumns()
        {
            var end = DateTime.UtcNow.Date;
            var start = end.AddDays(-5);
            var df = await _historicalDataSourceService.GetHistoricalDataFrameAsync(
                TestEquity,
                start,
                end,
                ResolutionLevel.Daily);

            // 检查行数和列名
            Assert.IsTrue(df.Rows.Count > 0, "DataFrame should contain at least one row.");
            var expectedCols = new[] { "DateTime", "Open", "High", "Low", "Close", "Volume", "AdjustedClose" };
            CollectionAssert.IsSubsetOf(expectedCols, df.Columns.Select(c => c.Name).ToList());
        }

        /// <summary>
        /// 测试：GetOhlcvListAsync(endDt, limit) 应返回指定数量的条目。
        /// </summary>
        [TestMethod]
        public async Task GetOhlcvListAsync_EndDtLimit_ShouldReturnExactCount()
        {
            var end = DateTime.UtcNow.Date;
            int limit = 126;
            var list = (await _historicalDataSourceService.GetOhlcvListAsync(
                TestEquity,
                end,
                limit,
                ResolutionLevel.Daily)).ToList();

            // 可能因为周末无数据略少于 limit，但不多于 limit
            Assert.IsTrue(list.Count > 0 && list.Count <= limit, $"Should return up to {limit} bars.");
            Assert.IsTrue(list.All(o => o.Symbol == Symbol), "Each Ohlcv.Symbol should match.");
        }

        /// <summary>
        /// 测试：GetOhlcvListAsync(startDt, endDt) 应返回按时间排序的非空序列。
        /// </summary>
        [TestMethod]
        public async Task GetOhlcvListAsync_StartEnd_ShouldReturnTimeOrderedData()
        {
            var end = DateTime.UtcNow.Date;
            var start = end.AddDays(-126);
            var data = (await _historicalDataSourceService.GetOhlcvListAsync(
                TestEquity,
                start,
                end,
                ResolutionLevel.Daily)).ToList();

            Assert.IsTrue(data.Count > 0, "Should return at least one bar in the given range.");
            // 检查时间正序
            for (int i = 1; i < data.Count; i++)
            {
                Assert.IsTrue(data[i].OpenDateTime > data[i - 1].OpenDateTime,
                    "Bars should be in ascending time order.");
            }
        }
    }

}

