using Org.BouncyCastle.Asn1.Cmp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Quant.Infra.Net.SourceData.Model
{
    // ---- DTOs ----
    public class CmcListingsResponse
    {
        [JsonPropertyName("status")] public CmcStatus? Status { get; init; }
        [JsonPropertyName("data")] public List<CmcListingItem>? Data { get; init; }
    }

    public class CmcStatus
    {
        [JsonPropertyName("error_code")] public int ErrorCode { get; init; }
        [JsonPropertyName("error_message")] public string? ErrorMessage { get; init; }
    }

    public class CmcListingItem
    {
        [JsonPropertyName("symbol")] public string Symbol { get; init; } = "";
    }
}
