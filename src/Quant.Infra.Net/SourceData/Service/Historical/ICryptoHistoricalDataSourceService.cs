using Quant.Infra.Net.Shared.Model;

namespace Quant.Infra.Net.SourceData.Service.Historical
{
    public interface ICryptoHistoricalDataSourceService: IHistoricalDataSourceService
    {
        // 默认方法表示现货Spot， 如果需要其他的数据，可以此处添加;
    }
}
