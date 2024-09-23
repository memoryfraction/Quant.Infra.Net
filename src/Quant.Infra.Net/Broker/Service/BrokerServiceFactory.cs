using Microsoft.Extensions.Configuration;
using Quant.Infra.Net.Account.Service;
using System;

namespace Quant.Infra.Net.Broker.Service
{
    public class BrokerServiceFactory
    {
        private readonly IConfiguration _configuration;

        public BrokerServiceFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public BrokerServiceBase CreateBrokerService(Shared.Model.Broker brokerType)
        {
            return brokerType switch
            {
                Shared.Model.Broker.Binance => new BinanceService(_configuration),
                Shared.Model.Broker.InteractiveBrokers => new InteractiveBrokersService(), // 添加 InteractiveBrokersService
                // 这里可以添加其他 broker 的实例化逻辑
                _ => throw new NotSupportedException($"Broker type '{brokerType}' is not supported.")
            };
        }
    }

}
