using RestSharp;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Notification.Service
{
    public interface IWeChatService
    {
        Task<RestResponse> SendTextNotificationAsync(string content, string webHook);
    }
}