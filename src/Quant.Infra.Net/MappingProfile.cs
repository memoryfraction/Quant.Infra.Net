using AutoMapper;
using Quant.Infra.Net.SourceData.Model;
using YahooFinanceApi;

namespace Quant.Infra.Net
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // 添加其他映射信息
            CreateMap<Shared.Model.ResolutionLevel, YahooFinanceApi.Period>().ReverseMap();
            CreateMap<Candle, Ohlcv>().ReverseMap();
        }
    }
}