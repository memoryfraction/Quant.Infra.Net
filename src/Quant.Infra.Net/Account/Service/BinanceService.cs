using System;
using Quant.Infra.Net.Shared.Model;
using System.Collections.Generic;
using System.Threading.Tasks;
using Binance.Net.Clients;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Quant.Infra.Net.Account.Service
{
    /// <summary>
    /// Binance服务类，实现了与Binance相关的操作
    /// Binance Service class, implements operations related to Binance
    /// </summary>
    public class BinanceService : AbstractBrokerService
    {
        private readonly BinanceRestClient _binanceRestClient;

        /// <summary>
        /// 基础货币，默认为USD
        /// Base currency, default is USD
        /// </summary>
        public override Currency BaseCurrency { get; set; } = Currency.USD;
        private string _apiKey, _apiSecret;
        private IConfiguration _configuration;

        public BinanceService(IConfiguration configuration)
        {
            _apiKey = _configuration["Exchange:apiKey"];
            _apiSecret = _configuration["Exchange:apiSecret"];
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
                var symbolList = await client.SpotApi.ExchangeData.GetExchangeInfoAsync();
                if (symbolList.Success == true)
                    return symbolList.Data.Symbols.Select(x => x.Name).ToList();
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

        /// <summary>
        /// 异步设置指定资产的持仓比例
        /// Asynchronously set holdings ratio for a specific asset
        /// </summary>
        /// <param name="symbol">资产的代码 / The asset symbol</param>
        /// <param name="assetType">资产类型 / The type of asset</param>
        /// <param name="ratio">持仓比例 / The holdings ratio</param>
        public override async Task SetHoldingsAsync(string symbol, AssetType assetType, decimal ratio)
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
        public override async Task<decimal> GetHoldingAsync(string symbol, AssetType assetType)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 异步获取指定资产的市场价值
        /// Asynchronously get the market value for a specific asset
        /// </summary>
        /// <param name="symbol">资产的代码 / The asset symbol</param>
        /// <param name="assetType">资产类型 / The type of asset</param>
        /// <returns>返回该资产的市场价值 / Returns the market value of the asset</returns>
        public override async Task<decimal> GetMarketValueAsync(string symbol, AssetType assetType)
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
    }
}
