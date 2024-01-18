using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Notification.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Tests
{
    [TestClass]
    public class NotificationTests
    {
        // IOC
        private ServiceCollection _services;
        private string _wechatWebHook;
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
               .AddUserSecrets<DingTalkTests>()
               .Build();

            _wechatWebHook = _configuration["WeChatRobotWebHook"];
           
        }
        [TestMethod]
        public async Task SendWeChatRobotNotification_Should_Work()
        {
            using (var sp = _services.BuildServiceProvider())
            {
                var weChatService = sp.GetRequiredService<IWeChatService>();
                var response = await weChatService.SendNotificationAsync("this is a test message from Quant.Infra.Net Unit Test.", _wechatWebHook);
                Console.WriteLine($"ErrorMessage:{response.ErrorMessage}");
                Console.WriteLine($"Status:{response.StatusCode}");
                Assert.IsTrue(response.IsSuccessful);
            }
        }
    }
}
