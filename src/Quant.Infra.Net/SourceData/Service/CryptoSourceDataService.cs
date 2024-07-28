using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Quant.Infra.Net.SourceData.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Quant.Infra.Net.SourceData.Service
{
    public class CryptoSourceDataService
    {
        public async Task DownloadBinanceSpotAsync(DateTime startDt, DateTime endDt, string path = "", KlineInterval klineInterval = KlineInterval.OneHour)
        {
            var symbols = new List<string>();
            //1 GET BINANCE SPOT SYMBOLS
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var exchangeInfo = await client.SpotApi.ExchangeData.GetExchangeInfoAsync();
                symbols = exchangeInfo.Data.Symbols.Select(x => x.Name).ToList();
                Console.WriteLine(symbols.Count);
            }

            //2 过滤出以稳定币为结尾的symbol否则没有意义
            // 找出symbols中仅存在于stableCoinSymbols的部分，大小写不敏感
            var filteredSymbols = new List<string>();
            filteredSymbols.AddRange(symbols.Where(x => x.ToLower().EndsWith("usdt")).Select(x => x).ToList());

            //3 下载 
            if (string.IsNullOrEmpty(path))
                path = AppDomain.CurrentDomain.BaseDirectory + "\\data\\spot\\";
            if (!Directory.Exists(path))
                await Task.Run(() => Directory.CreateDirectory(path));

            var interval = klineInterval; // 时间间隔默认为1天
            foreach (var symbol in filteredSymbols)
            {
                Console.WriteLine($"Downloading: {symbol}.");
                var fileName = $"{symbol}.csv";
                var fullPathFileName = Path.Combine(path, fileName);

                await SaveSpotKlinesToCsv(symbol, interval, startDt, endDt, fullPathFileName);
            }
            Console.WriteLine($"All done!");
        }

        public async Task DownloadBinanceUsdFutureAsync(DateTime startDt, DateTime endDt, string path = "", KlineInterval klineInterval = KlineInterval.OneHour)
        {
            //1 GET BINANCE UsdFuture symbols
            var symbols = new List<string>();
            using (var client = new Binance.Net.Clients.BinanceRestClient())
            {
                var exchangeInfo = await client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
                symbols = exchangeInfo.Data.Symbols.Select(x => x.Name).ToList();
                Console.WriteLine($"symbols.Count: {symbols.Count}");
            }

            //2 过滤出以稳定币为结尾的symbol否则没有意义
            var filteredSymbols = new List<string>();
            filteredSymbols.AddRange(symbols.Where(x => x.ToLower().EndsWith("usdt")).Select(x => x).ToList());

            //3 下载 
            if (string.IsNullOrEmpty(path))
                path = AppDomain.CurrentDomain.BaseDirectory + "\\data\\UsdFuture\\";
            if (!Directory.Exists(path))
                await Task.Run(() => Directory.CreateDirectory(path));

            var interval = klineInterval; // 时间间隔默认为1天
            foreach (var symbol in filteredSymbols)
            {
                Console.WriteLine($"Downloading: {symbol}.");
                var fileName = $"{symbol}.csv";
                var fullPathFileName = Path.Combine(path, fileName);

                await SaveUsdFutureKlinesToCsv(symbol, interval, startDt, endDt, fullPathFileName);
            }
            Console.WriteLine($"All done!");
        }

        #region private functions

        /// <summary>
        /// 从Binance下载数据， 存到制定的(csv)文件
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="interval"></param>
        /// <param name="startDt"></param>
        /// <param name="endDt"></param>
        /// <param name="fullPathFileName"></param>
        /// <returns></returns>
        async Task SaveSpotKlinesToCsv(string symbol, Binance.Net.Enums.KlineInterval interval, DateTime startDt, DateTime endDt, string fullPathFileName)
        {
            if (endDt > DateTime.Now)
                throw new ArgumentOutOfRangeException();

            // 确保路径存在
            var directory = Path.GetDirectoryName(fullPathFileName);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var ohlcvs = new HashSet<Ohlcv>();
            using (var client = new BinanceRestClient())
            {
                var lastDtInOhlcvs = ohlcvs.Select(x => x.DateTime).LastOrDefault();
                var paramStartDt = startDt;
                while (lastDtInOhlcvs < endDt)
                {
                    // 每次调用默认只能获取500条数据;
                    if (lastDtInOhlcvs != default(DateTime)) // 此时ohlcvs有值
                    {
                        paramStartDt = lastDtInOhlcvs;
                    }

                    // 获取历史K线数据 
                    var klinesResult = await client.SpotApi.ExchangeData.GetKlinesAsync(symbol, interval, paramStartDt, endDt);
                    if (klinesResult.Success)
                    {
                        if (klinesResult.Data.Count() == 0) // 如果取不到数据，跳过该币种
                            return;
                        ohlcvs = UpsertOhlcvs(klinesResult.Data, ohlcvs, startDt, endDt);
                    }
                    lastDtInOhlcvs = ohlcvs.Select(x => x.DateTime).LastOrDefault();
                }

                // Save ohlcvs to file: {fullPathFileName} using csvHelper
                lastDtInOhlcvs = ohlcvs.Select(x => x.DateTime).LastOrDefault();
                var firstDtInOhlcvs = ohlcvs.Select(x => x.DateTime).FirstOrDefault();
                if (firstDtInOhlcvs.Date != startDt)
                {
                    Console.WriteLine($"firstDtInOhlcvs:{firstDtInOhlcvs} does not match with startDt:{startDt}");
                    return;
                }
                if (lastDtInOhlcvs.Date != endDt)
                {
                    Console.WriteLine($"lastDtInOhlcvs:{firstDtInOhlcvs} does not match with endDt:{endDt}");
                    return;
                }

                if (File.Exists(fullPathFileName))
                    File.Delete(fullPathFileName);
                using (var writer = new StreamWriter(fullPathFileName))
                {
                    writer.WriteLine("DateTime,Open,High,Low,Close,Volume");
                    foreach (var ohlcv in ohlcvs)
                    {
                        writer.WriteLine($"{ohlcv.DateTime},{ohlcv.Open},{ohlcv.High},{ohlcv.Low},{ohlcv.Close},{ohlcv.Volume}");
                    }
                }
                Console.WriteLine($"Klines data saved successfully for {symbol}.");
            }
        }

        async Task SaveUsdFutureKlinesToCsv(string symbol, Binance.Net.Enums.KlineInterval interval, DateTime startDt, DateTime endDt, string fullPathFileName)
        {
            if (endDt > DateTime.Now)
                throw new ArgumentOutOfRangeException();

            // 确保路径存在
            var directory = Path.GetDirectoryName(fullPathFileName);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var ohlcvs = new HashSet<Ohlcv>();
            using (var client = new BinanceRestClient())
            {
                var lastDtInOhlcvs = ohlcvs.Select(x => x.DateTime).LastOrDefault();
                var paramStartDt = startDt;
                while (lastDtInOhlcvs < endDt)
                {
                    // 每次调用默认只能获取500条数据;
                    if (lastDtInOhlcvs != default(DateTime)) // 此时ohlcvs有值
                    {
                        paramStartDt = lastDtInOhlcvs;
                    }

                    // 获取历史K线数据 
                    var klinesResult = await client.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, interval, paramStartDt, endDt);
                    if (klinesResult.Success)
                    {
                        if (klinesResult.Data.Count() == 0) // 如果取不到数据，跳过该币种
                            return;
                        ohlcvs = UpsertOhlcvs(klinesResult.Data, ohlcvs, startDt, endDt);
                    }
                    lastDtInOhlcvs = ohlcvs.Select(x => x.DateTime).LastOrDefault();
                }

                // Save ohlcvs to file: {fullPathFileName} using csvHelper
                lastDtInOhlcvs = ohlcvs.Select(x => x.DateTime).LastOrDefault();
                var firstDtInOhlcvs = ohlcvs.Select(x => x.DateTime).FirstOrDefault();
                if (firstDtInOhlcvs.Date != startDt)
                {
                    Console.WriteLine($"firstDtInOhlcvs:{firstDtInOhlcvs} does not match with startDt:{startDt}");
                    return;
                }
                if (lastDtInOhlcvs.Date != endDt)
                {
                    Console.WriteLine($"lastDtInOhlcvs:{firstDtInOhlcvs} does not match with endDt:{endDt}");
                    return;
                }

                if (File.Exists(fullPathFileName))
                    File.Delete(fullPathFileName);
                using (var writer = new StreamWriter(fullPathFileName))
                {
                    writer.WriteLine("DateTime,Open,High,Low,Close,Volume");
                    foreach (var ohlcv in ohlcvs)
                    {
                        writer.WriteLine($"{ohlcv.DateTime},{ohlcv.Open},{ohlcv.High},{ohlcv.Low},{ohlcv.Close},{ohlcv.Volume}");
                    }
                }
                Console.WriteLine($"Klines data saved successfully for {symbol}.");
            }
        }


        HashSet<Ohlcv> UpsertOhlcvs(IEnumerable<IBinanceKline> klines, HashSet<Ohlcv> ohlcvs, DateTime startDt, DateTime endDt)
        {
            foreach (var kline in klines)
            {
                if (kline.CloseTime < startDt)
                    continue;

                var ohlcv = new Ohlcv()
                {
                    DateTime = kline.CloseTime,
                    Open = kline.OpenPrice,
                    High = kline.HighPrice,
                    Low = kline.LowPrice,
                    Close = kline.ClosePrice,
                    Volume = kline.Volume
                };

                ohlcvs.Add(ohlcv);
                if (kline.CloseTime > endDt)
                    break;
            }
            return ohlcvs;
        }


        bool IntervalIsMatch(IEnumerable<IBinanceKline> klines, Binance.Net.Enums.KlineInterval interval)
        {
            var klinesInterval = CalculateInterval(klines);
            return klinesInterval == interval;
        }

        Binance.Net.Enums.KlineInterval CalculateInterval(IEnumerable<IBinanceKline> klines)
        {
            var klineList = klines.ToList();
            if (klineList.Count < 2)
            {
                throw new ArgumentException("At least two klines are required to calculate the interval.");
            }

            var firstKline = klineList[0];
            var secondKline = klineList[1];

            var intervalSeconds = (secondKline.OpenTime - firstKline.OpenTime).TotalSeconds;

            // Convert seconds to KlineInterval
            return intervalSeconds switch
            {
                1 => Binance.Net.Enums.KlineInterval.OneSecond,
                60 => Binance.Net.Enums.KlineInterval.OneMinute,
                180 => Binance.Net.Enums.KlineInterval.ThreeMinutes,
                300 => Binance.Net.Enums.KlineInterval.FiveMinutes,
                900 => Binance.Net.Enums.KlineInterval.FifteenMinutes,
                1800 => Binance.Net.Enums.KlineInterval.ThirtyMinutes,
                3600 => Binance.Net.Enums.KlineInterval.OneHour,
                7200 => Binance.Net.Enums.KlineInterval.TwoHour,
                14400 => Binance.Net.Enums.KlineInterval.FourHour,
                21600 => Binance.Net.Enums.KlineInterval.SixHour,
                28800 => Binance.Net.Enums.KlineInterval.EightHour,
                43200 => Binance.Net.Enums.KlineInterval.TwelveHour,
                86400 => Binance.Net.Enums.KlineInterval.OneDay,
                259200 => Binance.Net.Enums.KlineInterval.ThreeDay,
                604800 => Binance.Net.Enums.KlineInterval.OneWeek,
                2592000 => Binance.Net.Enums.KlineInterval.OneMonth,
                _ => throw new ArgumentException("Unsupported interval.")
            };
        }
        #endregion
    }
}
