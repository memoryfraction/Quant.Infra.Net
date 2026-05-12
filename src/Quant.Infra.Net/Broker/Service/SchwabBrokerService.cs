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
        private const string MarketDataBaseUrl = "https://api.schwabapi.com/marketdata/v1/";
        private string? _accessToken;
        private string? _accountHash;
        private DateTime _tokenExpiry;

        public SchwabBrokerService(BrokerCredentials credentials, string accountNumber)
        {
            _apiKey = credentials.ApiKey;
            _apiSecret = credentials.Secret;
            _accountNumber = accountNumber;
            _baseUrl = (credentials.BaseUrl ?? "https://api.schwabapi.com/trader/v1").TrimEnd('/');
            
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(_baseUrl + "/");
        }

        #region Authentication

        /// <summary>
        /// 外部注入已有的 access token（OAuth 授权码换取后存入 session）
        /// </summary>
        public void SetAccessToken(string accessToken, int expiresInSeconds = 1800)
        {
            _accessToken = accessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresInSeconds - 60);
        }

        /// <summary>
        /// 获取访问令牌 - 仅使用已注入的 token，不自动申请
        /// </summary>
        private Task<string> GetAccessTokenAsync()
        {
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
                return Task.FromResult(_accessToken);

            throw new InvalidOperationException(
                "Access token 已过期或未设置，请重新登录授权。");
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

        private async Task<string> GetAccountHashAsync()
        {
            if (!string.IsNullOrEmpty(_accountHash))
                return _accountHash;

            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync("accounts/accountNumbers");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var accounts = JsonSerializer.Deserialize<JsonElement>(content);

            foreach (var account in accounts.EnumerateArray())
            {
                var accountNumber = account.GetProperty("accountNumber").GetString() ?? "";
                var hashValue = account.GetProperty("hashValue").GetString() ?? "";

                if (IsRequestedAccount(accountNumber, hashValue))
                {
                    _accountHash = hashValue;
                    return _accountHash;
                }
            }

            if (accounts.GetArrayLength() == 1)
            {
                _accountHash = accounts[0].GetProperty("hashValue").GetString();
                if (!string.IsNullOrEmpty(_accountHash))
                    return _accountHash;
            }

            throw new InvalidOperationException($"Schwab account {_accountNumber} was not found in authorized accounts.");
        }

        private bool IsRequestedAccount(string accountNumber, string hashValue)
        {
            var requested = NormalizeAccountNumber(_accountNumber);
            var actual = NormalizeAccountNumber(accountNumber);

            return actual.Equals(requested, StringComparison.OrdinalIgnoreCase) ||
                   hashValue.Equals(_accountNumber, StringComparison.OrdinalIgnoreCase) ||
                   (requested.Length >= 4 && actual.EndsWith(requested, StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizeAccountNumber(string value)
        {
            return new string(value.Where(char.IsLetterOrDigit).ToArray());
        }

        #endregion

        #region Account

        public async Task<SchwabAccount> GetAccountAsync()
        {
            try
            {
                await SetAuthHeaderAsync();
                var accountHash = await GetAccountHashAsync();
                var response = await _httpClient.GetAsync($"accounts/{accountHash}?fields=positions");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var accountData = JsonSerializer.Deserialize<JsonElement>(content);

                var securitiesAccount = accountData.GetProperty("securitiesAccount");
                var currentBalances = securitiesAccount.GetProperty("currentBalances");

                var account = new SchwabAccount
                {
                    AccountNumber = _accountNumber,
                    AccountType = securitiesAccount.GetProperty("type").GetString() ?? "",
                    CashBalance = GetJsonDecimal(currentBalances, "cashBalance"),
                    MarketValue = GetJsonDecimal(currentBalances, "longMarketValue"),
                    TotalEquity = GetJsonDecimal(currentBalances, "equity", "liquidationValue", "accountValue"),
                    BuyingPower = GetJsonDecimal(currentBalances, "buyingPower", "cashAvailableForTrading")
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
                var accountHash = await GetAccountHashAsync();
                var response = await _httpClient.GetAsync($"accounts/{accountHash}?fields=positions");
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
                            Quantity = GetJsonDecimal(pos, "longQuantity") - GetJsonDecimal(pos, "shortQuantity"),
                            CostPrice = GetJsonDecimal(pos, "averagePrice", "averageLongPrice", "averageShortPrice"),
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
                var response = await GetMarketDataAsync($"quotes?symbols={Uri.EscapeDataString(symbol)}");
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
                var symbolsParam = string.Join(",", symbols.Select(Uri.EscapeDataString));
                var response = await GetMarketDataAsync($"quotes?symbols={symbolsParam}");
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
                
                var queryParams = new List<string> { $"symbol={Uri.EscapeDataString(symbol)}" };
                if (!string.IsNullOrEmpty(contractType))
                    queryParams.Add($"contractType={contractType}");
                if (strikeCount.HasValue)
                    queryParams.Add($"strikeCount={strikeCount.Value}");

                var queryString = string.Join("&", queryParams);
                var response = await GetMarketDataAsync($"chains?{queryString}");
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

                var accountHash = await GetAccountHashAsync();
                var response = await _httpClient.PostAsync($"accounts/{accountHash}/orders", content);
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
                var accountHash = await GetAccountHashAsync();
                var response = await _httpClient.GetAsync($"accounts/{accountHash}/orders/{orderId}");
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
                var accountHash = await GetAccountHashAsync();
                var response = await _httpClient.DeleteAsync($"accounts/{accountHash}/orders/{orderId}");
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
                var accountHash = await GetAccountHashAsync();
                var toEnteredTime = DateTime.UtcNow;
                var fromEnteredTime = toEnteredTime.AddDays(-60);
                var query = $"maxResults={maxResults}" +
                    $"&fromEnteredTime={Uri.EscapeDataString(fromEnteredTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}" +
                    $"&toEnteredTime={Uri.EscapeDataString(toEnteredTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}";

                var response = await _httpClient.GetAsync($"accounts/{accountHash}/orders?{query}");
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
            JsonElement orderLeg = default;
            JsonElement instrument = default;
            var hasLeg = orderData.TryGetProperty("orderLegCollection", out var legs) &&
                         legs.ValueKind == JsonValueKind.Array &&
                         legs.GetArrayLength() > 0;
            if (hasLeg)
            {
                orderLeg = legs[0];
                orderLeg.TryGetProperty("instrument", out instrument);
            }

            return new SchwabOrder
            {
                OrderId = GetJsonString(orderData, "orderId"),
                Symbol = instrument.ValueKind == JsonValueKind.Object ? GetJsonString(instrument, "symbol") : "",
                Status = GetJsonString(orderData, "status"),
                OrderType = GetJsonString(orderData, "orderType"),
                Side = hasLeg ? GetJsonString(orderLeg, "instruction") : "",
                Quantity = hasLeg ? (int)GetJsonDecimal(orderLeg, "quantity") : 0,
                FilledQuantity = (int)GetJsonDecimal(orderData, "filledQuantity"),
                LimitPrice = TryGetNullableDecimal(orderData, "price"),
                StopPrice = TryGetNullableDecimal(orderData, "stopPrice"),
                AverageFilledPrice = TryGetNullableDecimal(orderData, "averageFilledPrice"),
                TimeInForce = GetJsonString(orderData, "duration"),
                CreatedAt = GetJsonString(orderData, "enteredTime"),
                UpdatedAt = orderData.TryGetProperty("closeTime", out var close) ? close.GetString() ?? "" : ""
            };
        }

        private static decimal GetJsonDecimal(JsonElement element, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (!element.TryGetProperty(propertyName, out var property))
                    continue;

                if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
                    return number;

                if (property.ValueKind == JsonValueKind.String &&
                    decimal.TryParse(property.GetString(), out var parsed))
                    return parsed;
            }

            return 0;
        }

        private static decimal? TryGetNullableDecimal(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property) ||
                property.ValueKind == JsonValueKind.Null ||
                property.ValueKind == JsonValueKind.Undefined)
                return null;

            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var number))
                return number;

            if (property.ValueKind == JsonValueKind.String &&
                decimal.TryParse(property.GetString(), out var parsed))
                return parsed;

            return null;
        }

        private static string GetJsonString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var property))
                return string.Empty;

            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString() ?? string.Empty,
                JsonValueKind.Number => property.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => property.GetRawText()
            };
        }

        #endregion

        #region Market Status

        public async Task<bool> IsMarketOpenAsync()
        {
            try
            {
                await SetAuthHeaderAsync();
                var response = await GetMarketDataAsync("markets?markets=equity");
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
                "EQUITY" => AssetType.UsEquity,
                "OPTION" => AssetType.UsOption,
                "MUTUAL_FUND" => AssetType.UsEquity,
                "FIXED_INCOME" => AssetType.UsEquity,
                _ => AssetType.UsEquity
            };
        }

        private async Task<HttpResponseMessage> GetMarketDataAsync(string pathAndQuery)
        {
            await SetAuthHeaderAsync();
            var uri = new Uri(new Uri(MarketDataBaseUrl), pathAndQuery);
            return await _httpClient.GetAsync(uri);
        }

        #endregion
    }
}
