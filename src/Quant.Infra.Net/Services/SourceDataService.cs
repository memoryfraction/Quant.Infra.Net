using System;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Services
{
    public class SourceDataService : ISourceDataService
    {
        private bool _isBusy;


        Task ISourceDataService.BeginSyncSourceDailyDataAsync(string symbol, DateTime startDt, DateTime endDt)
        {
            throw new NotImplementedException();
        }
    }
}
