using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Notification.Model;
using Quant.Infra.Net.Notification.Service;


namespace Quant.Infra.Net.Tests
{
	[TestClass]
	public class EmailIntegrationTests
	{
		private EmailServiceFactory _factory;
		private IConfiguration _config;
		private string _testRecipient = "alphawealthlab@outlook.com";


		[TestInitialize]
		public void Setup()
		{
			// 1. 加载配置
			_config = new ConfigurationBuilder()
				.AddJsonFile("appsettings.test.json", optional: true)
				.AddEnvironmentVariables()
				.AddUserSecrets<EmailIntegrationTests>() // 建议将授权码存在 Secret 中
				.Build();

			// 2. 模拟生产环境的 DI 容器注册
			var services = new ServiceCollection();

			// 必须注册具体的实现类，因为 Factory 内部使用 GetRequiredService<T>
			services.AddTransient<PersonalEmailService>();
			services.AddTransient<MailKitCommercialService>();

			// 将 IConfiguration 注入容器供某些可能需要的服务使用
			services.AddSingleton(_config);

			var serviceProvider = services.BuildServiceProvider();

			// 3. 初始化工厂
			_factory = new EmailServiceFactory(serviceProvider);
		}


		[TestMethod]
		public async Task MVP_PersonalSendTest_ViaFactory()
		{
			// Arrange
			var recipients = new List<string> { _testRecipient };
			var message = new EmailMessage
			{
				To = recipients,
				Subject = $"MVP Factory Test - {DateTime.Now:HH:mm}",
				Body = "<h1>MVP 发送测试</h1><p>通过 EmailServiceFactory 路由至 PersonalEmailService 发送。</p>",
				IsHtml = true
			};

			// 从配置中读取 126 邮箱设置
			// 假设 JSON 结构为: "Email": { "Personal": { "SmtpServer": "...", "Password": "..." } }
			var settings = _config.GetSection("Email").Get<EmailSettings>();

			// Act
			// 1. 通过工厂获取服务 (recipientCount 为 1，应返回 PersonalEmailService)
			var service = _factory.GetService(recipients.Count);

			// 验证工厂路由是否符合 MVP 预期
			Assert.IsInstanceOfType(service, typeof(PersonalEmailService), "MVP 阶段且收件人<50时应使用个人服务");

			// 2. 调用真实发送
			var result = await service.SendBulkEmailAsync(message, settings);

			// Assert
			Assert.IsTrue(result, "邮件发送失败，请检查 126 授权码或 SMTP 端口配置。");
			Console.WriteLine($"真实邮件已发出，请检查收件箱: {_testRecipient}");
		}

	}
}