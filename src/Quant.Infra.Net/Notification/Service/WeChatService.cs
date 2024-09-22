using RestSharp;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Notification.Service
{
    public class WeChatService : IWeChatService
    {
        /// <summary>
        /// 通过企业微信群机器人发送信息
        /// </summary>
        /// <param name="content"></param>
        /// <param name="webHook"></param>
        /// <returns></returns>
        public async Task<RestResponse> SendTextNotificationAsync(string content, string webHook)
        {
            // 创建一个 RestClient 对象，传入 webHook 参数作为基地址
            var client = new RestClient();
            // 创建一个 RestRequest 对象，指定请求方法为 POST
            var request = new RestRequest(webHook, Method.Post);
            // 设置请求的 Content-Type 为 application/json
            request.AddHeader("Content-Type", "application/json");
            // 在请求体中添加 content 参数的内容，作为消息的文本
            request.AddJsonBody(new { msgtype = "text", text = new { content = content } });
            // 异步发送请求，并返回响应
            return await client.ExecuteAsync(request);
        }
    }
}