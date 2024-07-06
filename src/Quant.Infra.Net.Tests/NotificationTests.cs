using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Notification.Service;

namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class NotificationTests
    {
        // IOC
        private ServiceCollection _services;
        private string _wechatWebHook, _dingTalkAccessToken,_dingTalkSecret;
        private IConfigurationRoot _configuration;

        public NotificationTests()
        {
            // 依赖注入
            _services = new ServiceCollection();
            _services.AddScoped<IDingtalkService, DingtalkService>();
            _services.AddScoped<IWeChatService, WeChatService>();

            // Read Secret
            _configuration = new ConfigurationBuilder()
               .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
               .AddJsonFile("appsettings.json")
               .AddUserSecrets<NotificationTests>()
               .Build();

            _wechatWebHook = _configuration["Notification:WeChatRobotWebHook"];
            _dingTalkAccessToken = _configuration["Notification:DingTalk:accessToken"];
            _dingTalkSecret = _configuration["Notification:DingTalk:secret"];

        }
        [TestMethod]
        public async Task SendWeChatRobotNotification_Should_Work()
        {
            using (var sp = _services.BuildServiceProvider())
            {
                var weChatService = sp.GetRequiredService<IWeChatService>();
                var response = await weChatService.SendTextNotificationAsync("this is a test message from Quant.Infra.Net Unit Test.", _wechatWebHook);
                Console.WriteLine($"ErrorMessage:{response.ErrorMessage}");
                Console.WriteLine($"Status:{response.StatusCode}");
                Assert.IsTrue(response.IsSuccessful);
            }
        }

        [TestMethod]
        public async Task DingTalkSendNotification_Should_Work()
        {
            using (var sp = _services.BuildServiceProvider())
            {
                var dingtalkService = sp.GetRequiredService<IDingtalkService>();
                var response = await dingtalkService.SendNotificationAsync("test123", _dingTalkAccessToken, _dingTalkSecret);
                Console.WriteLine(response.Content);
                Assert.IsTrue(response.StatusCode == System.Net.HttpStatusCode.OK);
            }
        }
    }
}
