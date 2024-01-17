using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Account.Service
{
    public interface IAccountService
    {
        Task<List<string>> GetSymbolListAsync();

        // TODO 需要什么就实现什么接口;

        // TODO 已知USDT balance， 如何得到USD balance？ Ans: USDT BALANCE => BTCUSDT => BTCUSD
    }
}
