using Microsoft.Extensions.Configuration;
using Quant.Infra.Net.Account.Service;
using System;

namespace Quant.Infra.Net.Broker.Service
{
    /// <summary>
    /// 券商服务工厂，根据 Broker 枚举创建对应的交易服务实例。
    /// Factory that creates broker service instances based on the Broker enum.
    /// </summary>
    public class BrokerServiceFactory
    {
        private readonly IConfiguration _configuration;

        /// <summary>
        /// 初始化 BrokerServiceFactory。
        /// Initialize the BrokerServiceFactory with configuration.
        /// </summary>
        /// <param name="configuration">应用配置 / Application configuration.</param>
        /// <exception cref="ArgumentNullException">当 configuration 为 null 时抛出 / Thrown when configuration is null.</exception>
        public BrokerServiceFactory(IConfiguration configuration)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));
            _configuration = configuration;
        }

        /// <summary>
        /// 根据券商类型创建对应的交易服务实例。
        /// Create a broker service instance based on the specified broker type.
        /// </summary>
        /// <param name="brokerType">券商类型（Binance, InteractiveBrokers 等）/ Broker type (e.g., Binance, InteractiveBrokers).</param>
        /// <returns>对应的交易服务实例 / The corresponding broker service instance.</returns>
        /// <exception cref="NotSupportedException">当券商类型不受支持时抛出 / Thrown when the broker type is not supported.</exception>
        public BrokerServiceBase CreateBrokerService(Shared.Model.Broker brokerType)
        {
            return brokerType switch
            {
                Shared.Model.Broker.Binance => new BinanceService(_configuration),
                Shared.Model.Broker.InteractiveBrokers => new InteractiveBrokersService(),
                _ => throw new NotSupportedException($"Broker type '{brokerType}' is not supported.")
            };
        }
    }
}
