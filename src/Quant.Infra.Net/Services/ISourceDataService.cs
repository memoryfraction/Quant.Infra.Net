using System;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Services
{
    public interface ISourceDataService
    {
        Task BeginSyncSourceDailyDataAsync(string symbol, DateTime startDt, DateTime endDt);


             
    }
}
