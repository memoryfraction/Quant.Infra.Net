using RestSharp;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Notification.Service
{
    /// <summary>
    /// 通知服务，传入参数，通知相关端，比如: 企业微信、钉钉等;
    /// </summary>
    public interface IDingtalkService
    {
        Task<RestResponse> SendNotificationAsync(string content, string accessToken, string secret);
    }
}