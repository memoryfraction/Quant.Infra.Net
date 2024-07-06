using CryptoExchange.Net.CommonObjects;
using Quant.Infra.Net.Shared.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Exchange.Service
{
    /// <summary>
    /// IIBKRService， 定义IBKR的服务接口
    /// </summary>
    public interface IIBKRService
    {
        // 获取账户信息

        // 订单 CRUD
        Task PlaceMarketOrderAsync(Order order, string exchange = "SMART", ContractSecurityType securityType = ContractSecurityType.Stock, Currency currency = Currency.USD);

        // 获取持仓信息
        
        // 获取历史数据

        // 获取实时数据
    }
}
