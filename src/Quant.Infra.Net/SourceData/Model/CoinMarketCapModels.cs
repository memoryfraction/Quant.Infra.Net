using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Quant.Infra.Net.SourceData.Model
{
    /// <summary>
    /// CoinMarketCap 上市币种列表响应 DTO。
    /// DTO for the CoinMarketCap listings API response.
    /// </summary>
    public class CmcListingsResponse
    {
        /// <summary>
        /// API 请求状态 / Status of the API request.
        /// </summary>
        [JsonPropertyName("status")] public CmcStatus? Status { get; init; }

        /// <summary>
        /// 上市币种数据列表 / List of listing items returned by CoinMarketCap.
        /// </summary>
        [JsonPropertyName("data")] public List<CmcListingItem>? Data { get; init; }
    }

    /// <summary>
    /// CoinMarketCap API 状态信息。
    /// Status information from the CoinMarketCap API response.
    /// </summary>
    public class CmcStatus
    {
        /// <summary>
        /// 错误码（0 表示成功）/ Error code (0 indicates success).
        /// </summary>
        [JsonPropertyName("error_code")] public int ErrorCode { get; init; }

        /// <summary>
        /// 错误消息 / Human-readable error message if the request failed.
        /// </summary>
        [JsonPropertyName("error_message")] public string? ErrorMessage { get; init; }
    }

    /// <summary>
    /// CoinMarketCap 上市币种条目。
    /// Individual listing item from the CoinMarketCap API.
    /// </summary>
    public class CmcListingItem
    {
        /// <summary>
        /// 交易品种代码（如 BTC, ETH）/ Trading symbol (e.g., BTC, ETH).
        /// </summary>
        [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";
    }
}
