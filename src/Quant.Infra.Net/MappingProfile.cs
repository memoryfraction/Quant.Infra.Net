using AutoMapper;

namespace Quant.Infra.Net
{
    /// <summary>
    /// 映射配置。
    /// Mapping profile for AutoMapper mappings used across the project.
    /// </summary>
    public class MappingProfile : Profile
    {
        /// <summary>
        /// 初始化映射配置。
        /// Initialize mapping configurations. This constructor registers mappings between
        /// domain/source models and external library models (e.g. YahooFinanceApi).
        /// </summary>
        public MappingProfile()
        {
            // register mappings between internal models and external library models
        }
    }
}