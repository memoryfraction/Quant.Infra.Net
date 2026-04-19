using Quant.Infra.Net.Broker.Interfaces;
using Quant.Infra.Net.Broker.Model;
using Quant.Infra.Net.Portfolio.Models;
using Quant.Infra.Net.Shared.Model;
using Quant.Infra.Net.Shared.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Broker.Service
{
    /// <summary>
    /// Charles Schwab 券商服务实现
    /// Implementation of Charles Schwab broker service
    /// </summary>
    public class SchwabBrokerService : ISchwabBrokerService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _apiSecret;
        private readonly string _accountNumber;
        private readonly string _baseUrl;
        private string? _accessToken;
        private DateTime _tokenExpiry;

        public SchwabBrokerService(BrokerCredentials credentials, string accountNumber)
        {
            _apiKey = credentials.ApiKey;
            _apiSecret = credentials.Secret;
            _accountNumber = accountNumber;
            _baseUrl = credentials.BaseUrl ?? "https://api.schwabapi.com/trader/v1";
            
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl)
            };
        }

        #region Authentication

        /// <summary>
        /// 获取访问令牌
        /// Get access token
        /// </summary>
        private async Task<string> GetAccessTokenAsync()
        {
            // 如果令牌仍然有效，直接返回
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
            {
                return _accessToken;
            }

            try
            {
                // Schwab 使用 OAuth 2.0 认证
                var authUrl = "https://api.schwabapi.com/v1/oauth/token";
                var authClient = new HttpClient();

                var authData = new Dictionary<string, string>
                {
                    { "grant_type", "client_credentials" },
                    { "client_id", _apiKey },
                    { "client_secret", _apiSecret }
                };

                var content = new FormUrlEncodedContent(authData);
                var response = await authClient.PostAsync(authUrl, content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

                _accessToken = tokenResponse.GetProperty("access_token").GetString();
                var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();
                _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60); // 提前60秒刷新

                UtilityService.LogAndWriteLine($"[Schwab] 成功获取访问令牌，有效期: {expiresIn} 秒");
                return _accessToken ?? throw new InvalidOperationException("Failed to get access token");
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine($"[Schwab] 获取访问令牌失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 设置请求头
        /// Set request headers
        /// </summary>
        private async Task SetAuthHeaderAsync()
        {
            var token = await GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        #endregion

        #region Account

        public async Task<SchwabAccount> GetAccountAsync()
        {
            try
            {
                await SetAuthHeaderAsync();
                var response = await _httpClient.GetAsync($"/accounts/{_accountNumber}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var accountData = JsonSerializer.Deserialize<JsonElement>(content);

                var securitiesAccount = accountData.GetProperty("securitiesAccount");
                var currentBalances = securitiesAccount.GetProperty("currentBalances");

                var account = new SchwabAccount
                {
                    AccountNumber = _accountNumber,
                    AccountType = securitiesAccount.GetProperty("type").GetString() ?? "",
                    CashBalance = currentBalances.GetProperty("cashBalance").GetDecimal(),
                    MarketValue = currentBalances.GetProperty("longMarketValue").GetDecimal(),
                    TotalEquity = currentBalances.GetProperty("equity").GetDecimal(),
                    BuyingPower = currentBalances.GetProperty("buyingPower").GetDecimal()
                };

                UtilityService.LogAndWriteLine($"[Schwab] 账户信息: 总资产={account.TotalEquity:C}, 市值={account.MarketValue:C}, 现金={account.CashBalance:C}");
                return account;
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine($"[Schwab] 获取账户信息失败: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Positions

        public async Task<List<Position>> GetPositionsAsync()
        {
            try
            {
                await SetAuthHeaderAsync();
                var response = await _httpClient.GetAsync($"/accounts/{_accountNumber}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var accountData = JsonSerializer.Deserialize<JsonElement>(content);

                var positions = new List<Position>();
                
                if (accountData.TryGetProperty("securitiesAccount", out var securitiesAccount) &&
                    securitiesAccount.TryGetProperty("positions", out var positionsArray))
                {
                    foreach (var pos in positionsArray.EnumerateArray())
                    {
                        var instrument = pos.GetProperty("instrument");
                        var symbol = instrument.GetProperty("symbol").GetString() ?? "";
                        var assetType = instrument.GetProperty("assetType").GetString() ?? "EQUITY";

                        var position = new Position
                        {
                            Symbol = symbol,
                            Quantity = pos.GetProperty("longQuantity").GetDecimal(),
                            CostPrice = pos.GetProperty("averagePrice").GetDecimal(),
                            AssetType = ParseAssetType(assetType),
                            UnrealizedProfitLoss = pos.TryGetProperty("currentDayProfitLoss", out var pnl) 
                                ? pnl.GetDecimal() 
                                : null,
                            EntryDateTime = DateTime.UtcNow // Schwab API 可能不提供入场时间
                        };

                        positions.Add(position);
                    }
                }

                UtilityService.LogAndWriteLine($"[Schwab] 获取到 {positions.Count} 个持仓");
                return positions;
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine($"[Schwab] 获取持仓信息失败: {ex.Message}");
                throw;
            }
        }

        public async Task<Position?> GetPositionAsync(string symbol)
        {
            var positions = await GetPositionsAsync();
            return positions.FirstOrDefault(p => p.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Quotes

        public async Task<SchwabQuote> GetQuoteAsync(string symbol)
        {
            try
            {
                await SetAuthHeaderAsync();
                var response = await _httpClient.GetAsync($"/marketdata/v1/quotes?symbols={symbol}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var quotesData = JsonSerializer.Deserialize<JsonElement>(content);

                if (quotesData.TryGetProperty(symbol, out var quoteData))
                {
                    var quote = ParseQuote(symbol, quoteData);
                    UtilityService.LogAndWriteLine($"[Schwab] {symbol} 报价: ${quote.LastPrice}");
                    return quote;
                }

                throw new InvalidOperationException($"未找到 {symbol} 的报价数据");
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine($"[Schwab] 获取报价失败 ({symbol}): {ex.Message}");
                throw;
            }
        }

        public async Task<Dictionary<string, SchwabQuote>> GetQuotesAsync(List<string> symbols)
        {
            try
            {
                await SetAuthHeaderAsync();
                var symbolsParam = string.Join(",", symbols);
                var response = await _httpClient.GetAsync($"/marketdata/v1/quotes?symbols={symbolsParam}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var quotesData = JsonSerializer.Deserialize<JsonElement>(content);

                var quotes = new Dictionary<string, SchwabQuote>();
                foreach (var symbol in symbols)
                {
                    if (quotesData.TryGetProperty(symbol, out var quoteData))
                    {
                        quotes[symbol] = ParseQuote(symbol, quoteData);
                    }
                }

                UtilityService.LogAndWriteLine($"[Schwab] 获取到 {quotes.Count}/{symbols.Count} 个报价");
                return quotes;
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine($"[Schwab] 批量获取报价失败: {ex.Message}");
                throw;
            }
        }

        private SchwabQuote ParseQuote(string symbol, JsonElement quoteData)
        {
            var quote = quoteData.GetProperty("quote");
            return new SchwabQuote
            {
                Symbol = symbol,
                BidPrice = quote.TryGetProperty("bidPrice", out var bid) ? bid.GetDecimal() : 0,
                AskPrice = quote.TryGetProperty("askPrice", out var ask) ? ask.GetDecimal() : 0,
                LastPrice = quote.GetProperty("lastPrice").GetDecimal(),
                Volume = quote.TryGetProperty("totalVolume", out var vol) ? vol.GetInt64() : 0,
                High = quote.TryGetProperty("highPrice", out var high) ? high.GetDecimal() : 0,
                Low = quote.TryGetProperty("lowPrice", out var low) ? low.GetDecimal() : 0,
                Open = quote.TryGetProperty("openPrice", out var open) ? open.GetDecimal() : 0,
                Close = quote.TryGetProperty("closePrice", out var close) ? close.GetDecimal() : 0,
                Change = quote.TryGetProperty("netChange", out var change) ? change.GetDecimal() : 0,
                ChangePercent = quote.TryGetProperty("netPercentChange", out var pct) ? pct.GetDecimal() : 0,
                Timestamp = quote.TryGetProperty("quoteTime", out var time) ? time.GetInt64() : 0
            };
        }

        #endregion

        #region Option Chain

        public async Task<SchwabOptionChain> GetOptionChainAsync(string symbol, string? contractType = null, int? strikeCount = null)
        {
            try
            {
                await SetAuthHeaderAsync();
                
                var queryParams = new List<string> { $"symbol={symbol}" };
                if (!string.IsNullOrEmpty(contractType))
                    queryParams.Add($"contractType={contractType}");
                if (strikeCount.HasValue)
                    queryParams.Add($"strikeCount={strikeCount.Value}");

                var queryString = string.Join("&", queryParams);
                var response = await _httpClient.GetAsync($"/marketdata/v1/chains?{queryString}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var chainData = JsonSerializer.Deserialize<JsonElement>(content);

                var optionChain = new SchwabOptionChain
                {
                    Symbol = symbol,
                    Status = chainData.GetProperty("status").GetString() ?? "",
                    UnderlyingPrice = chainData.GetProperty("underlyingPrice").GetDecimal()
                };

                // 解析 Call 期权
                if (chainData.TryGetProperty("callExpDateMap", out var callMap))
                {
                    optionChain.CallOptions = ParseOptionContracts(callMap, "CALL");
                }

                // 解析 Put 期权
                if (chainData.TryGetProperty("putExpDateMap", out var putMap))
                {
                    optionChain.PutOptions = ParseOptionContracts(putMap, "PUT");
                }

                UtilityService.LogAndWriteLine($"[Schwab] {symbol} 期权链: {optionChain.CallOptions.Count} Calls, {optionChain.PutOptions.Count} Puts");
                return optionChain;
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine($"[Schwab] 获取期权链失败 ({symbol}): {ex.Message}");
                throw;
            }
        }

        private List<SchwabOptionContract> ParseOptionContracts(JsonElement expDateMap, string contractType)
        {
            var contracts = new List<SchwabOptionContract>();

            foreach (var expDate in expDateMap.EnumerateObject())
            {
                var expirationDate = expDate.Name;
                
                foreach (var strikeEntry in expDate.Value.EnumerateObject())
                {
                    foreach (var contract in strikeEntry.Value.EnumerateArray())
                    {
                        var optionContract = new SchwabOptionContract
                        {
                            Symbol = contract.GetProperty("symbol").GetString() ?? "",
                            Description = contract.GetProperty("description").GetString() ?? "",
                            ExpirationDate = expirationDate,
                            Strike = contract.GetProperty("strikePrice").GetDecimal(),
                            ContractType = contractType,
                            Bid = contract.TryGetProperty("bid", out var bid) ? bid.GetDecimal() : 0,
                            Ask = contract.TryGetProperty("ask", out var ask) ? ask.GetDecimal() : 0,
                            Last = contract.TryGetProperty("last", out var last) ? last.GetDecimal() : 0,
                            Mark = contract.TryGetProperty("mark", out var mark) ? mark.GetDecimal() : 0,
                            Volume = contract.TryGetProperty("totalVolume", out var vol) ? vol.GetInt64() : 0,
                            OpenInterest = contract.TryGetProperty("openInterest", out var oi) ? oi.GetInt64() : 0,
                            ImpliedVolatility = contract.TryGetProperty("volatility", out var iv) ? iv.GetDecimal() : 0,
                            Delta = contract.TryGetProperty("delta", out var delta) ? delta.GetDecimal() : 0,
                            Gamma = contract.TryGetProperty("gamma", out var gamma) ? gamma.GetDecimal() : 0,
                            Theta = contract.TryGetProperty("theta", out var theta) ? theta.GetDecimal() : 0,
                            Vega = contract.TryGetProperty("vega", out var vega) ? vega.GetDecimal() : 0,
                            Rho = contract.TryGetProperty("rho", out var rho) ? rho.GetDecimal() : 0,
                            InTheMoney = contract.TryGetProperty("inTheMoney", out var itm) && itm.GetBoolean()
                        };

                        contracts.Add(optionContract);
                    }
                }
            }

            return contracts;
        }

        #endregion

        #region Orders

        public async Task<string> PlaceOrderAsync(SchwabOrderRequest orderRequest)
        {
            try
            {
                await SetAuthHeaderAsync();

                var orderPayload = new
                {
                    orderType = orderRequest.OrderType,
                    session = "NORMAL",
                    duration = orderRequest.TimeInForce,
                    orderStrategyType = "SINGLE",
                    orderLegCollection = new[]
                    {
                        new
                        {
                            instruction = orderRequest.Side,
                            quantity = orderRequest.Quantity,
                            instrument = new
                            {
                                symbol = orderRequest.Symbol,
                                assetType = orderRequest.AssetType
                            }
                        }
                    },
                    price = orderRequest.LimitPrice,
                    stopPrice = orderRequest.StopPrice
                };

                var json = JsonSerializer.Serialize(orderPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"/accounts/{_accountNumber}/orders", content);
                response.EnsureSuccessStatusCode();

                // Schwab 返回订单 ID 在 Location header 中
                var location = response.Headers.Location?.ToString() ?? "";
                var orderId = location.Split('/').LastOrDefault() ?? "";

                UtilityService.LogAndWriteLine($"[Schwab] 订单已提交: {orderId} ({orderRequest.Side} {orderRequest.Quantity} {orderRequest.Symbol})");
                return orderId;
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine($"[Schwab] 下单失败: {ex.Message}");
                throw;
            }
        }

        public async Task<SchwabOrder> GetOrderAsync(string orderId)
        {
            try
            {
                await SetAuthHeaderAsync();
                var response = await _httpClient.GetAsync($"/accounts/{_accountNumber}/orders/{orderId}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var orderData = JsonSerializer.Deserialize<JsonElement>(content);

                return ParseOrder(orderData);
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine($"[Schwab] 获取订单失败 ({orderId}): {ex.Message}");
                throw;
            }
        }

        public async Task<bool> CancelOrderAsync(string orderId)
        {
            try
            {
                await SetAuthHeaderAsync();
                var response = await _httpClient.DeleteAsync($"/accounts/{_accountNumber}/orders/{orderId}");
                response.EnsureSuccessStatusCode();

                UtilityService.LogAndWriteLine($"[Schwab] 订单已取消: {orderId}");
                return true;
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine($"[Schwab] 取消订单失败 ({orderId}): {ex.Message}");
                return false;
            }
        }

        public async Task<List<SchwabOrder>> GetOrdersAsync(int maxResults = 100)
        {
            try
            {
                await SetAuthHeaderAsync();
                var response = await _httpClient.GetAsync($"/accounts/{_accountNumber}/orders?maxResults={maxResults}");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var ordersArray = JsonSerializer.Deserialize<JsonElement>(content);

                var orders = new List<SchwabOrder>();
                foreach (var orderData in ordersArray.EnumerateArray())
                {
                    orders.Add(ParseOrder(orderData));
                }

                UtilityService.LogAndWriteLine($"[Schwab] 获取到 {orders.Count} 个订单");
                return orders;
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine($"[Schwab] 获取订单列表失败: {ex.Message}");
                throw;
            }
        }

        private SchwabOrder ParseOrder(JsonElement orderData)
        {
            var orderLeg = orderData.GetProperty("orderLegCollection")[0];
            var instrument = orderLeg.GetProperty("instrument");

            return new SchwabOrder
            {
                OrderId = orderData.GetProperty("orderId").GetString() ?? "",
                Symbol = instrument.GetProperty("symbol").GetString() ?? "",
                Status = orderData.GetProperty("status").GetString() ?? "",
                OrderType = orderData.GetProperty("orderType").GetString() ?? "",
                Side = orderLeg.GetProperty("instruction").GetString() ?? "",
                Quantity = orderLeg.GetProperty("quantity").GetInt32(),
                FilledQuantity = orderData.TryGetProperty("filledQuantity", out var filled) ? filled.GetInt32() : 0,
                LimitPrice = orderData.TryGetProperty("price", out var price) ? price.GetDecimal() : null,
                StopPrice = orderData.TryGetProperty("stopPrice", out var stop) ? stop.GetDecimal() : null,
                AverageFilledPrice = orderData.TryGetProperty("averageFilledPrice", out var avg) ? avg.GetDecimal() : null,
                TimeInForce = orderData.GetProperty("duration").GetString() ?? "",
                CreatedAt = orderData.GetProperty("enteredTime").GetString() ?? "",
                UpdatedAt = orderData.TryGetProperty("closeTime", out var close) ? close.GetString() ?? "" : ""
            };
        }

        #endregion

        #region Market Status

        public async Task<bool> IsMarketOpenAsync()
        {
            try
            {
                await SetAuthHeaderAsync();
                var response = await _httpClient.GetAsync("/marketdata/v1/markets?markets=equity");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var marketData = JsonSerializer.Deserialize<JsonElement>(content);

                if (marketData.TryGetProperty("equity", out var equity) &&
                    equity.TryGetProperty("EQ", out var eq))
                {
                    var isOpen = eq.GetProperty("isOpen").GetBoolean();
                    UtilityService.LogAndWriteLine($"[Schwab] 市场状态: {(isOpen ? "开盘" : "休市")}");
                    return isOpen;
                }

                return false;
            }
            catch (Exception ex)
            {
                UtilityService.LogAndWriteLine($"[Schwab] 获取市场状态失败: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Helper Methods

        private AssetType ParseAssetType(string assetType)
        {
            return assetType.ToUpper() switch
            {
                "EQUITY" => AssetType.USEquity,
                "OPTION" => AssetType.Option,
                "MUTUAL_FUND" => AssetType.USEquity,
                "FIXED_INCOME" => AssetType.USEquity,
                _ => AssetType.USEquity
            };
        }

        #endregion
    }
}
