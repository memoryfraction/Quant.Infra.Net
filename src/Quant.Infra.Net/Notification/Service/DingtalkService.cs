using Newtonsoft.Json;
using Quant.Infra.Net.Notification.Model;
using RestSharp;
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Notification.Service
{
    public class DingtalkService : IDingtalkService
    {
        public async Task<RestResponse> SendNotificationAsync(string content, string accessToken, string secret)
        {
            // 时间戳精确到毫秒,这里需要注意下
            var timestamp = ((DateTime.Now.Ticks - TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1)).Ticks) / 10000).ToString();
            var stringToSign = timestamp + "\n" + secret;
            var sign = EncryptWithSHA256(stringToSign, secret);
            var targetURL = new Uri($"https://oapi.dingtalk.com/robot/send?access_token={accessToken}&timestamp={timestamp}&sign={sign}");
            var client = new RestClient(targetURL);
            var request = new RestRequest(targetURL, Method.Post);
            request.AddHeader("Content-Type", "application/json");
            var messageBody = new MessageBody()
            {
                text = new text()
                {
                    content = content
                }
            };
            var body = JsonConvert.SerializeObject(messageBody);
            request.AddParameter("application/json", body, ParameterType.RequestBody);
            var response = await client.ExecuteAsync(request);
            return response;
        }

        /// <summary>
        /// Base64 SHA256
        /// </summary>
        /// <param name="data">待加密数据</param>
        /// <param name="secret">密钥</param>
        /// <returns></returns>
        private string EncryptWithSHA256(string data, string secret)
        {
            secret = secret ?? "";

            // 1、string 转换成 utf-8 的byte[]
            var encoding = Encoding.UTF8;
            byte[] keyByte = encoding.GetBytes(secret);
            byte[] dataBytes = encoding.GetBytes(data);

            // 2、 HMACSHA256加密
            using (var hmac256 = new HMACSHA256(keyByte))
            {
                byte[] hashData = hmac256.ComputeHash(dataBytes);
                // 3、转换成base64
                var base64Str = Convert.ToBase64String(hashData);
                // 4、urlEncode编码
                return System.Web.HttpUtility.UrlEncode(base64Str, Encoding.UTF8);
            }
        }
    }
}