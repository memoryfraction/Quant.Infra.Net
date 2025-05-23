namespace Quant.Infra.Net.Broker.Model
{
    /// <summary>
    /// BrokerCredentials
    /// 交易账户凭证信息
    /// </summary>
    public class BrokerCredentials
    {
        /// <summary>
        /// ApiKey
        /// 交易API的访问密钥
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
        /// <summary>
        /// Secret
        /// 交易API的密钥
        /// </summary>
        public string Secret { get; set; } = string.Empty;
        /// <summary>
        /// BaseUrl
        /// 交易所环境的基础URL（如Paper/Live）
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;  // 可选，用于交易所环境（如Paper/Live）
    }
}
