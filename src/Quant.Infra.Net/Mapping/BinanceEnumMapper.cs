using Quant.Infra.Net.Shared.Model;
using Riok.Mapperly.Abstractions;

namespace Quant.Infra.Net.Mapping
{
    [Mapper]
    public static partial class BinanceEnumMapper
    {
        [MapEnum(EnumMappingStrategy.ByValue)]
        public static partial Binance.Net.Enums.SpotOrderType ToBinanceSpotOrderType(OrderActionType type);

        [MapEnum(EnumMappingStrategy.ByName)]
        public static partial Binance.Net.Enums.CancelReplaceMode ToBinanceCancelReplaceMode(CancelReplaceMode mode);
    }
}