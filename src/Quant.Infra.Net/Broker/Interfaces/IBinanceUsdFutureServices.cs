
using Quant.Infra.Net.Shared.Model;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Broker.Interfaces
{
    public interface IBinanceUsdFutureServices
    {
        Task<decimal> GetusdFutureAccountBalanceAsync();

        /// <summary>
        /// 获取未变现的利润率
        /// </summary>
        /// <returns></returns>
        Task<double> GetusdFutureUnrealizedProfitRateAsync();

        Task usdFutureLiquidateAsync(string symbol);

        Task SetUsdFutureHoldingsAsync(string symbol, double rate);


    }
}
