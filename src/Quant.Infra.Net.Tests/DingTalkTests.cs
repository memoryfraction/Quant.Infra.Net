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
    public  class DingTalkTests
    {

        // IOC
        private ServiceCollection _services;
        private string _accessToken, _secret;
        private IConfigurationRoot _configuration;

        public DingTalkTests()
        {
            // 依赖注入
            _services = new ServiceCollection();
            _services.AddScoped<INotificationService, DingtalkService>();

            // Read Secret
            _configuration = new ConfigurationBuilder()
               .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
               .AddJsonFile("appsettings.json")
               .AddUserSecrets<DingTalkTests>()
               .Build();

            _accessToken = _configuration["DingDing:accessToken"];
            _secret = _configuration["DingDing:secret"];
        }

        [TestMethod]
        public async Task TestMethod()
        {
            using (var sp = _services.BuildServiceProvider())
            {
                var dingtalkService = sp.GetRequiredService<INotificationService>();
                var response = await dingtalkService.SendNotificationAsync("test123", _accessToken, _secret);
                Console.WriteLine(response.Content);
                Assert.IsTrue(response.StatusCode == System.Net.HttpStatusCode.OK);
            }
        }

    }
}
