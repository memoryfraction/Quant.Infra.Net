using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Account.Service
{
    public class BinanceAccountService : IAccountService
    {
        public Task<List<string>> GetSymbolListAsync()
        {
            throw new NotImplementedException();
        }
    }
}
