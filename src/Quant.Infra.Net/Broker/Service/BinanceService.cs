using Binance.Net.Clients;
using Binance.Net.Enums;
using CryptoExchange.Net.Authentication;
using Microsoft.Data.Analysis;
using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.SourceData.Model;
using Quant.Infra.Net.SourceData.Service.Historical;
using Quant.Infra.Net.SourceData.Service.RealTime;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Account.Service
{
    /// <summary>
    /// Binance服务类，实现了与Binance相关的操作
    /// Binance Service class, implements operations related to Binance
    /// </summary>
    public class BinanceService : BrokerServiceBase, IHistoricalDataSourceServiceCryptoBinance, IRealtimeDataSourceServiceCrypto
    {
        private BinanceRestClient _binanceRestClient;
        private readonly AsyncRetryPolicy _retryPolicy;

        /// <summary>
        /// 基础货币，默认为USD
        /// Base currency, default is USD
        /// </summary>
        public override Currency BaseCurrency { get; set; } = Currency.USD;

        private string _apiKey, _apiSecret;

        private IConfiguration _configuration;

        public BinanceService(IConfiguration configuration)
        {
            _configuration = configuration;
            _apiKey = _configuration["Exchange:ApiKey"];
            _apiSecret = _configuration["Exchange:ApiSecret"];

            _binanceRestClient = new Binance.Net.Clients.BinanceRestClient();

            _retryPolicy = Policy
            .Handle<Exception>() // 可以根据需要处理其他类型的错误
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))); // 指数退避
        }

        /// <summary>
        /// 异步获取所有现货交易对的列表
        /// Asynchronously get the list of all spot trading pairs
        /// </summary>
        /// <returns>返回所有现货交易对的列表 / Returns a list of all spot trading pairs</returns>
        public override async Task<IEnumerable<string>> GetSpotSymbolListAsync()
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var spotExchangeInfo = await client.SpotApi.ExchangeData.GetExchangeInfoAsync();
                if (spotExchangeInfo.Success == true)
                    return spotExchangeInfo.Data.Symbols.Select(x => x.Name).ToList();
                else
                    return new List<string>();
            }
        }

        /// <summary>
        /// 异步获取所有USD合约交易对的列表
        /// Asynchronously get the list of all USD futures trading pairs
        /// </summary>
        /// <returns>返回所有USD合约交易对的列表 / Returns a list of all USD futures trading pairs</returns>
        public override async Task<IEnumerable<string>> GetUsdFuturesSymbolListAsync()
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var symbolList = await client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
                if (symbolList.Success == true)
                    return symbolList.Data.Symbols.Select(x => x.Name).ToList();
                else
                    return new List<string>();
            }
        }

        /// <summary>
        /// 异步获取所有币本位合约交易对的列表
        /// Asynchronously get the list of all coin-margined futures trading pairs
        /// </summary>
        /// <returns>返回所有币本位合约交易对的列表 / Returns a list of all coin-margined futures trading pairs</returns>
        public override async Task<IEnumerable<string>> GetCoinFuturesSymbolListAsync()
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var symbolList = await client.CoinFuturesApi.ExchangeData.GetExchangeInfoAsync();
                if (symbolList.Success == true)
                    return symbolList.Data.Symbols.Select(x => x.Name).ToList();
                else
                    return new List<string>();
            }
        }

        public override async Task<decimal> GetLatestPriceAsync(Underlying underlying)
        {
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                if (underlying.AssetType == AssetType.CryptoSpot)
                {
                    var getPriceResponse = await client.SpotApi.ExchangeData.GetPriceAsync(underlying.Symbol);
                    if (getPriceResponse.Success == true)
                        return getPriceResponse.Data.Price;
                    else
                        throw new Exception();
                }
                else if(underlying.AssetType == AssetType.CryptoPerpetualContract)
                {
                    var getPriceResponse = await client.UsdFuturesApi.ExchangeData.GetPriceAsync(underlying.Symbol);
                    if (getPriceResponse.Success == true)
                        return getPriceResponse.Data.Price;
                    else
                        throw new Exception();
                }
                else
                {
                    throw new Exception();
                }
            }
        }

        /// <summary>
        /// 异步设置指定资产的持仓比例
        /// Asynchronously set holdings ratio for a specific asset
        /// </summary>
        /// <param name="symbol">资产的代码 / The asset symbol</param>
        /// <param name="assetType">资产类型 / The type of asset</param>
        /// <param name="ratio">持仓比例 / The holdings ratio</param>
        public override void SetHoldings(Underlying underlying, decimal ratio)
        {
            // Todo: 查看当前持仓; 

            throw new NotImplementedException();
        }

        public override void Liquidate(Underlying underlying)
        {
            // 查看当前持仓; 
            var quantity = GetHoldingAsync(underlying).Result;

            // Todo: 做反向交易;
            throw new NotImplementedException();
        }

        /// <summary>
        /// 创建 UsdFuture Market Order
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="orderSide">开关仓此信号需要相反</param>
        /// <param name="quantity">关仓数量需要与开仓数量一致， 总是正数; 最小交易数量5U</param>
        /// <param name="positionSide">LONG/SHORT是对冲模式， 多头开关都用LONG, 空头开关都用SHORT</param>
        /// <param name="futuresOrderType">FuturesOrderType.Market可以确保成交</param>
        /// <returns></returns>
        private async Task CreateUsdFutureOrder(
            string symbol, 
            OrderSide orderSide, 
            decimal quantity,
            PositionSide positionSide, 
            FuturesOrderType futuresOrderType = FuturesOrderType.Market)
        {
            BinanceRestClient.SetDefaultOptions(options =>
            {
                options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
            });

            // 创建 Binance 客户端            
            using (var client = new BinanceRestClient())
            {
                // 永续合约，开空仓
                var enterShortResponse = await client.UsdFuturesApi.Trading.PlaceOrderAsync(
                    symbol: symbol,
                    side: orderSide, // 开关仓此信号需要相反
                    type: futuresOrderType,
                    quantity: quantity, // 关仓数量需要与开仓数量一致， 总是正数; 最小交易数量5U
                    positionSide: positionSide // LONG/SHORT是对冲模式， 多头开关都用LONG, 空头开关都用SHORT
                );
            }
        }

        private async Task CreateSpotOrder(string symbol, decimal quantity, SpotOrderType spotOrderType = SpotOrderType.Market)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 异步获取指定资产的持仓份额
        /// Asynchronously get holdings shares for a specific asset
        /// </summary>
        /// <param name="symbol">资产的代码 / The asset symbol</param>
        /// <param name="assetType">资产类型 / The type of asset</param>
        /// <returns>返回持有该资产的份额 / Returns the holdings shares of the asset</returns>
        public override async Task<decimal> GetHoldingAsync(Underlying underlying)
        {
            BinanceRestClient.SetDefaultOptions(options =>
            {
                options.ApiCredentials = new ApiCredentials(_apiKey, _apiSecret);
            });
            // 创建 Binance 客户端            
            using (var client = new BinanceRestClient())
            {
                if(underlying.AssetType == AssetType.CryptoPerpetualContract)
                {
                    // 获取当前持仓数量
                    var accountInfo = await client.UsdFuturesApi.Account.GetAccountInfoV3Async();
                    var position = await client.UsdFuturesApi.Account.GetPositionInformationAsync();
                    var holdingPositions = position.Data.Where(x => x.Quantity != 0)
                        .Select(x => x);
                    var underlyingPosition = holdingPositions
                        .Where(x => x.Symbol == underlying.Symbol)
                        .FirstOrDefault();
                    return underlyingPosition.Quantity;
                }
                else if(underlying.AssetType == AssetType.CryptoSpot)
                {
                    // 获取当前持仓数量
                    var accountInfo = await client.SpotApi.Account.GetAccountInfoAsync();
                    var holdingPosition = accountInfo.Data.Balances
                        .Where(x => x.Asset.ToLower() == underlying.Symbol.ToLower())
                        .FirstOrDefault();
                    return holdingPosition.Total;
                }
                else
                {
                    throw new Exception($"Not supported AssetType:{underlying.AssetType}");
                }

            }
        }

        /// <summary>
        /// 异步获取指定资产的市场价值
        /// Asynchronously get the market value for a specific asset
        /// </summary>
        /// <param name="symbol">资产的代码 / The asset symbol</param>
        /// <param name="assetType">资产类型 / The type of asset</param>
        /// <returns>返回该资产的市场价值 / Returns the market value of the asset</returns>
        public override async Task<decimal> GetMarketValueAsync(Underlying underlying)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 异步获取所有持仓的总市场价值
        /// Asynchronously get the total market value of all holdings
        /// </summary>
        /// <returns>返回所有持仓的市场总价值 / Returns the total market value of all holdings</returns>
        public override async Task<decimal> GetTotalMarketValueAsync()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 异步计算未实现盈亏
        /// Asynchronously calculate unrealized profit and loss
        /// </summary>
        /// <returns>返回未实现盈亏 / Returns the unrealized profit and loss</returns>
        public async Task<decimal> CalculateUnrealizedProfitAsync()
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// 获取给定交易对的最新 OHLCV 数据, 从endDt倒序获取， 通过while循环，直到结果集合满足limit数量。
        /// </summary>
        /// <param name="underlying"></param>
        /// <param name="endDt"></param>
        /// <param name="limit"></param>
        /// <param name="resolutionLevel"></param>
        /// <returns></returns>
        public async Task<IEnumerable<Ohlcv>> GetOhlcvListAsync(
            Underlying underlying,
            DateTime endDt,
            int limit,
            ResolutionLevel resolutionLevel = ResolutionLevel.Hourly)
        {
            var hashSet = new HashSet<Ohlcv>();
            DateTime tmpEndDt = endDt;
            var oneTimeNumber = 1000; // 每次request，取1000条;
            while (hashSet.Count < limit)
            {
                var tmpList = await GetOhlcvListAsync(underlying, resolutionLevel: resolutionLevel, startDt:null, endDt: tmpEndDt, oneTimeNumber);
                foreach(var tmpElm in tmpList)
                    hashSet.Add(tmpElm);
                tmpEndDt = hashSet.OrderBy(x => x.CloseDateTime).Select(x => x.CloseDateTime).FirstOrDefault();
            }

            // 根据CloseDateTime正向排序，从hashSet尾部取数据limit个;
            return hashSet
                .OrderBy(x => x.CloseDateTime)    // Sort by CloseDateTime in ascending order
                .TakeLast(limit)                  // Take the last 'limit' items
                .ToList();                        // Convert to List or IEnumerable as required
        }


        /// <summary>
        /// 获取给定交易对的最新 OHLCV 数据。
        /// <para>Fetch the latest OHLCV data for the specified trading pair.</para>
        /// </summary>
        /// <param name="symbol">交易对符号，例如 "BTCUSDT"。<para>Trading pair symbol, e.g., "BTCUSDT".</para></param>
        /// <param name="resolutionLevel">K线的时间分辨率，默认为每小时。<para>The time resolution of the Klines, default is hourly.</para></param>
        /// <param name="assetType">资产类型，默认为加密货币现货。<para>Asset type, default is cryptocurrency spot.</para></param>
        /// <param name="startDt">开始时间，可选。<para>Start time, optional.</para></param>
        /// <param name="endDt">结束时间，可选。<para>End time, optional.</para></param>
        /// <param name="limit">获取的 K线数量，默认为 1。Binance规定上限: 1500。<para>Number of Klines to fetch, default is 1.</para></param>
        /// <returns>返回指定交易对的最新 OHLCV 数据。<para>Returns the latest OHLCV data for the specified trading pair.</para></returns>
        /// <exception cref="NotSupportedException">当资产类型不被支持时抛出此异常。<para>Throws this exception when the asset type is not supported.</para></exception>
        /// <exception cref="Exception">当无法成功获取 OHLCV 数据时抛出此异常。<para>Throws this exception when unable to successfully fetch OHLCV data.</para></exception>
        private async Task<List<Ohlcv>> GetOhlcvListAsync(
            Underlying underlying,
            ResolutionLevel resolutionLevel = ResolutionLevel.Hourly,
            DateTime? startDt = null,
            DateTime? endDt = null,
            int limit = 1)
        {
            // 定义重试策略
            var retryPolicy = Policy
                .Handle<Exception>()
                .RetryAsync(3, (exception, retryCount) =>
                {
                    var message = $"Retry {retryCount} due to: {exception.Message}";
                    Console.WriteLine(message); // 控制台输出
                    Log.Warning(message); // 日志记录
                });
            try
            {
                using (var client = new Binance.Net.Clients.BinanceRestClient())
                {
                    // 根据 resolutionLevel 映射到 KlineInterval
                    var interval = GetKlineInterval(resolutionLevel);

                    return await retryPolicy.ExecuteAsync(async () =>
                    {
                        switch (underlying.AssetType)
                        {
                            case AssetType.CryptoSpot:
                                // 获取加密货币现货的K线数据
                                var spotKlinesResponse = await client.SpotApi.ExchangeData.GetKlinesAsync(underlying.Symbol, interval, startTime: startDt, endTime: endDt, limit: limit);
                                if (spotKlinesResponse.Success && spotKlinesResponse.Data.Any())
                                {
                                    // 映射多个K线数据为List<Ohlcv>
                                    return spotKlinesResponse.Data.Select(kline => new Ohlcv
                                    {
                                        Open = kline.OpenPrice,
                                        High = kline.HighPrice,
                                        Low = kline.LowPrice,
                                        Close = kline.ClosePrice,
                                        Volume = kline.Volume,
                                        CloseDateTime = kline.CloseTime
                                    }).ToList();
                                }
                                throw new Exception($"Failed to get spot OHLCV for {underlying.Symbol}: {spotKlinesResponse.Error?.Message}");

                            case AssetType.CryptoPerpetualContract:
                                // 获取加密货币永续合约的K线数据
                                var perpetualKlinesResponse = await client.UsdFuturesApi.ExchangeData.GetKlinesAsync(underlying.Symbol, interval, startTime: startDt, endTime: endDt, limit: limit);
                                if (perpetualKlinesResponse.Success && perpetualKlinesResponse.Data.Any())
                                {
                                    // 映射多个K线数据为List<Ohlcv>
                                    return perpetualKlinesResponse.Data.Select(kline => new Ohlcv
                                    {
                                        Open = kline.OpenPrice,
                                        High = kline.HighPrice,
                                        Low = kline.LowPrice,
                                        Close = kline.ClosePrice,
                                        Volume = kline.Volume,
                                        CloseDateTime = kline.CloseTime
                                    }).ToList();
                                }
                                throw new Exception($"Failed to get perpetual contract OHLCV for {underlying.Symbol}: {perpetualKlinesResponse.Error?.Message}");

                            case AssetType.CryptoOption:
                                // 获取加密货币期权的OHLCV数据 (假设Binance支持期权交易,具体接口需要查找Binance期权API)
                                throw new NotImplementedException("Crypto options are not yet implemented.");

                            default:
                                // 其他资产类型不支持
                                throw new NotSupportedException($"Asset type {underlying.AssetType} is not supported.");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                // 记录最终异常
                var errorMessage = $"Error fetching OHLCV data for {underlying.Symbol}: {ex.Message}";
                Console.WriteLine(errorMessage); // 控制台输出
                Log.Error(ex, errorMessage); // 日志记录
                throw; // 重新抛出异常以便上层处理
            }
        }


        private Binance.Net.Enums.KlineInterval GetKlineInterval(ResolutionLevel resolutionLevel)
        {
            return resolutionLevel switch
            {
                ResolutionLevel.Tick => Binance.Net.Enums.KlineInterval.OneMinute, // 假设Tick映射为1分钟
                ResolutionLevel.Second => Binance.Net.Enums.KlineInterval.OneMinute,
                ResolutionLevel.Minute => Binance.Net.Enums.KlineInterval.OneMinute,
                ResolutionLevel.Hourly => Binance.Net.Enums.KlineInterval.OneHour,
                ResolutionLevel.Daily => Binance.Net.Enums.KlineInterval.OneDay,
                ResolutionLevel.Weekly => Binance.Net.Enums.KlineInterval.OneWeek,
                ResolutionLevel.Monthly => Binance.Net.Enums.KlineInterval.OneMonth,
                _ => throw new NotSupportedException($"Resolution level {resolutionLevel} is not supported."),
            };
        }

        /// <summary>
        /// 根据入参，获取OhlcvList， 然后构建DataFrame，并返回。
        /// </summary>
        /// <param name="underlying"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="resolutionLevel"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public async Task<DataFrame> GetHistoricalDataFrameAsync(Underlying underlying, DateTime startDate, DateTime endDate, ResolutionLevel resolutionLevel)
        {
            var ohlcvList = await GetOhlcvListAsync(underlying, resolutionLevel, startDate, endDate);
            // 创建 DataFrame 列，只需要 DateTime 和 Close 列
            var dateTimeColumn = new PrimitiveDataFrameColumn<DateTime>("DateTime");
            var closeColumn = new DoubleDataFrameColumn("Close");

            // 遍历 ohlcvList，向 DataFrame 的列中填充数据
            foreach (var ohlcv in ohlcvList)
            {
                dateTimeColumn.Append(ohlcv.CloseDateTime);  // DateTime 列
                closeColumn.Append((double)ohlcv.Close);     // Close 列
            }

            // 创建 DataFrame 并添加 DateTime 和 Close 列
            var dataFrame = new DataFrame();
            dataFrame.Columns.Add(dateTimeColumn);
            dataFrame.Columns.Add(closeColumn);

            return dataFrame;
        }


        async Task<IEnumerable<Ohlcv>> IHistoricalDataSourceService.GetOhlcvListAsync(
            Underlying underlying, 
            DateTime startDt, 
            DateTime endDt, 
            ResolutionLevel resolutionLevel)
        {
            var limit = calculateLimit(startDt, endDt, resolutionLevel);
            return await GetOhlcvListAsync(underlying, endDt, limit, resolutionLevel);
        }


        /// <summary>
        /// Calculates the number of bars (limit) based on the time range and resolution level.
        /// 根据时间范围和解析级别，计算需要的bar数量（limit）。
        /// </summary>
        /// <param name="startDt">The start date and time for the time range. 时间范围的开始日期和时间。</param>
        /// <param name="endDt">The end date and time for the time range. 时间范围的结束日期和时间。</param>
        /// <param name="resolutionLevel">The resolution level (tick, second, minute, etc.). 解析级别（Tick、秒、分钟等）。</param>
        /// <returns>The number of bars required to cover the time range. 覆盖此时间范围所需的bar数量。</returns>
        int calculateLimit(DateTime startDt, DateTime endDt, ResolutionLevel resolutionLevel)
        {
            // Calculate the total time span between the start and end dates
            // 计算开始日期和结束日期之间的总时间跨度
            var timeSpan = endDt - startDt;

            // Calculate the number of bars based on the resolution level
            // 根据解析级别计算bar的数量
            return resolutionLevel switch
            {
                ResolutionLevel.Tick => (int)Math.Ceiling(timeSpan.TotalMilliseconds),  // Assuming 1 ms per tick 假设每个tick为1毫秒
                ResolutionLevel.Second => (int)Math.Ceiling(timeSpan.TotalSeconds),     // 每秒
                ResolutionLevel.Minute => (int)Math.Ceiling(timeSpan.TotalMinutes),     // 每分钟
                ResolutionLevel.Hourly => (int)Math.Ceiling(timeSpan.TotalHours),       // 每小时
                ResolutionLevel.Daily => (int)Math.Ceiling(timeSpan.TotalDays),         // 每天
                ResolutionLevel.Weekly => (int)Math.Ceiling(timeSpan.TotalDays / 7),    // 每周
                ResolutionLevel.Monthly => (int)Math.Ceiling(timeSpan.TotalDays / 30),  // 近似为每月30天
                _ => throw new ArgumentException("Unsupported resolution level")        // 不支持的解析级别
            };
        }

        public Task<IEnumerable<Ohlcv>> GetOhlcvListAsync(Underlying underlying, DateTime startDt, DateTime endDt, ResolutionLevel resolutionLevel = ResolutionLevel.Hourly)
        {
            throw new NotImplementedException();
        }

       

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> apiCall)
        {
            return await _retryPolicy.ExecuteAsync(async () => await apiCall());
        }

        
    }
}