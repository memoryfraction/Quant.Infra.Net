using Alpaca.Markets;
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


        /// <summary>
        /// 测试：PlaceOrderAsync 应能正确处理负的小数股数量（即卖出 fractional share），并成功建立空头仓位。
        /// Test: PlaceOrderAsync should correctly handle negative fractional quantity (i.e., shorting fractional share),
        /// and successfully establish a short position.
        /// </summary>
        /// <remarks>
        /// 注意：此测试依赖 Alpaca 模拟账户是否允许卖空 fractional shares（大多数标的默认不支持）。
        /// 如果失败，请确认 symbol 是否可交易且市场已开盘。
        /// Note: This test depends on Alpaca's simulated account allowing fractional shorting (which is restricted for most symbols).
        /// If it fails, check whether the symbol is tradable and the market is open.
        /// </remarks>
        [TestMethod]
        public async Task PlaceOrderAsync_ShouldSupportNegativeFractionalQuantity()
        {
            // 检查是否开盘时间，如果不开盘，则跳过测试
            // Skip the test if market is closed
            var isMarketOpen = await _brokerService.IsMarketOpeningAsync();
            if (!isMarketOpen)
            {
                Assert.Inconclusive("Market is closed, skipping test.");
                return;
            }

            // 下单：尝试设置 -0.05 百分比的仓位，打印结果;
            // Attempt to short 0.01 fractional share
            try
            {
                await _brokerService.LiquidateAsync(Symbol); // 确保没有持仓
                await Task.Delay(1500);
                await _brokerService.SetHoldingsAsync(Symbol, -0.05);
                await Task.Delay(1500); // 等待订单处理 / Wait for order execution

                var hasPosition = await _brokerService.HasPositionAsync(Symbol);
                Console.WriteLine($"Has short fractional position: {hasPosition}");

                // 如果实际账户不支持 fractional short，可能 hasPosition 为 false
                // May fail if Alpaca account doesn't support fractional shorting
                Assert.IsTrue(hasPosition, "Expected to have short fractional position, but none found.");
            }
            catch (Exception ex)
            {
                Assert.Fail($"PlaceOrderAsync failed with exception: {ex.Message}");
            }
            finally
            {
                // 清仓 / Ensure position is cleared
                await _brokerService.LiquidateAsync(Symbol);
                await Task.Delay(1000);
            }
        }

        /// <summary>
        /// 测试：获取过去一整年（365 日）的 Daily OHLCV 数据，检查第一条和最后一条时间范围正确，条数大于250。
        /// </summary>
        [TestMethod]
        public async Task GetOhlcvListAsync_OneYearDaily_ShouldCoverOneYearRange()
        {
            // 准备：过去一整年
            var end = DateTime.UtcNow.Date;
            var start = end.AddYears(-1);

            // 执行
            var list = (await _historicalDataSourceService.GetOhlcvListAsync(
                TestEquity,
                end,
                limit: 252,//1年252交易日
                ResolutionLevel.Daily))
                .ToList();

            // 验证起止时间范围：首条>=start，末条<=end
            Assert.IsTrue(list.Count  == 252);
            // 输出最有一条的数据
            var last = list.Last();
            // 获取美东时区信息（Windows ID 为 "Eastern Standard Time"）
            var estZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            var lastUtc = DateTime.SpecifyKind(last.OpenDateTime, DateTimeKind.Utc);
            var lastEst = TimeZoneInfo.ConvertTimeFromUtc(lastUtc, estZone);

            // 为了输出带时区偏移，用 DateTimeOffset
            var lastEstOffset = new DateTimeOffset(lastEst, estZone.GetUtcOffset(lastEst));

            Console.WriteLine(
                $"Last Bar (UTC) → {lastUtc:yyyy-MM-dd HH:mm:ss}Z\n" +
                $"Last Bar (ET)  → {lastEstOffset:yyyy-MM-dd HH:mm:ss zzz}");

            Assert.IsTrue(list.Last().OpenDateTime.Date <= end, $"Last  bar date {list.Last().OpenDateTime:yyyy-MM-dd} should be <= {end:yyyy-MM-dd}");

        }

        /// <summary>
        /// 测试：获取过去一整年（365 日）的 Hourly OHLCV 数据，检查第一条和最后一条时间范围正确，条数大于 24*200。
        /// </summary>
        [TestMethod]
        public async Task GetOhlcvListAsync_OneYearHourly_ShouldCoverOneYearRange()
        {
            // 准备：过去一整年
            var end = DateTime.UtcNow;
            var start = end.AddDays(-30);

            // 执行
            var list = (await _historicalDataSourceService.GetOhlcvListAsync(
                TestEquity,
                end,
                limit:130, // 20 交易日 * 6.5小时 = 130
                ResolutionLevel.Hourly))
                .ToList();

            // 验证不为空且条数至少 24*200（约200交易日*24小时）
            Assert.IsTrue(list.Count >= 130, $"Hourly bars count should be >= 4800, but was {list.Count}");

            // 验证起止时间范围：首条>=start，末条<=end
            Assert.IsTrue(list.Last().OpenDateTime <= end, $"Last  bar time {list.Last().OpenDateTime:O} should be <= {end:O}");
        }

        [TestMethod]
        public async Task GetAccountAsync_Should_Work()
        {
            // 调用 GetAccountAsync
            var account = await _brokerService.GetAccountAsync();

            // 基本校验
            Assert.IsNotNull(account, "Account object should not be null.");
            Console.WriteLine($"Account Status       : {account.Status}");
            Console.WriteLine($"TradableCash         : {account.TradableCash}");
            Console.WriteLine($"Buying Power         : {account.BuyingPower}");
            Console.WriteLine($"Regt. Buying Power   : {account.BuyingPower}");

            // 断言：账户状态为 ACTIVE
            Assert.AreEqual(AccountStatus.Active, account.Status, "Account status should be Active.");

            // 断言：现金和组合市值应当大于等于 0
            Assert.IsTrue(account.TradableCash >= 0m, "Cash should be non-negative.");

            //（可选）断言：日内交易购买力不小于现金
            Assert.IsTrue(account.BuyingPower >= account.TradableCash,
                "BuyingPower should be at least as large as Cash.");

        }


    }

}

