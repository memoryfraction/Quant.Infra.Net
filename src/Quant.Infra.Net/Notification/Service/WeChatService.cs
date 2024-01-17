using RestSharp;
using System;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Notification.Service
{
    public class WeChatService : INotificationService
    {
        public Task<RestResponse> SendNotificationAsync(string content, string accessToken, string secret)
        {
            throw new NotImplementedException();
        }
    }
}
