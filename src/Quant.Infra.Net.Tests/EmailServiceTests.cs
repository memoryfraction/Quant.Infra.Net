using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting; // 必须引用
using Moq; // 建议安装 NuGet 包: Moq
using Quant.Infra.Net.Notification.Model;
using Quant.Infra.Net.Notification.Service;

namespace Quant.Infra.Net.Tests
{
	[TestClass]
	public class EmailIntegrationTests
	{
		private EmailServiceFactory _factory;
		private IConfiguration _config;
		private IServiceProvider _serviceProvider; // 提升为成员变量以便直接获取服务
		private string _testRecipient = "yuanyuancomecome@outlook.com";

		[TestInitialize]
		public void Setup()
		{
			// 1. 加载配置
			_config = new ConfigurationBuilder()
				.AddJsonFile("appsettings.test.json", optional: true)
				.AddUserSecrets<EmailIntegrationTests>()
				.Build();

			// 2. 模拟生产环境的 DI 容器注册
			var services = new ServiceCollection();

			// --- 关键修改：模拟并注册 IHostEnvironment ---
			var mockEnv = new Mock<IHostEnvironment>();
			mockEnv.Setup(m => m.EnvironmentName).Returns("Development");
			mockEnv.Setup(m => m.ContentRootPath).Returns(AppDomain.CurrentDomain.BaseDirectory);
			services.AddSingleton(mockEnv.Object);

			// 注册具体的实现类
			services.AddTransient<PersonalEmailService>();
			services.AddTransient<CommercialEmailService>();

			// 将 IConfiguration 注入容器
			services.AddSingleton(_config);

			_serviceProvider = services.BuildServiceProvider();

			// 3. 初始化工厂
			_factory = new EmailServiceFactory(_serviceProvider, _config);
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

			var emailConfig = _config.GetSection("Email");
			var personalConfig = emailConfig.GetSection("Personal");

			var settings = new PersonalEmailSetting
			{
				SmtpServer = personalConfig["SmtpServer"] ?? "smtp.126.com",
				Port = int.Parse(personalConfig["Port"] ?? "465"),
				SenderEmail = personalConfig["SenderEmail"] ?? "test@126.com",
				Password = personalConfig["Password"] ?? "test-password",
				SenderName = personalConfig["SenderName"] ?? "Test Sender"
			};

			// Act
			var service = _factory.GetService(recipients.Count);

			// Assert
			Assert.IsInstanceOfType(service, typeof(PersonalEmailService));
			var result = await service.SendBulkEmailAsync(message, settings);
			Assert.IsTrue(result);
		}

		[TestMethod]
		public async Task MVP_SendCommercial()
		{
			// Arrange
			var recipients = new List<string> { _testRecipient, "rong.fan1031@gmail.com" };

			var message = new EmailMessage
			{
				To = recipients,
				Subject = $"🎯 量化交易系统邮件测试 - {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
				Body = "<h1>测试内容已省略...</h1>", // 保持你原来的 HTML 内容
				IsHtml = true
			};

			var emailConfig = _config.GetSection("Email");
			var commercialConfig = emailConfig.GetSection("Commercial");

			var settings = new CommercialEmailSetting
			{
				SmtpServer = commercialConfig["SmtpServer"] ?? "smtp-relay.brevo.com",
				Port = int.Parse(commercialConfig["Port"] ?? "587"),
				Username = commercialConfig["Username"] ?? "",
				Password = commercialConfig["Password"] ?? throw new InvalidOperationException("Brevo SMTP Key not found"),
				SenderEmail = commercialConfig["SenderEmail"] ?? "yuanhw512@gmail.com",
				SenderName = commercialConfig["SenderName"] ?? "Quant Lab System"
			};
			settings.SenderEmail = settings.SenderEmail.ToLower();

			// Act 
			// --- 关键修改：从 DI 容器获取服务，而不是 new ---
			var service = _serviceProvider.GetRequiredService<CommercialEmailService>();

			// 验证逻辑
			Console.WriteLine($"✅ 使用由 DI 容器注入 IHostEnvironment 的 CommercialEmailService");

			if (settings.Password.StartsWith("xkeysib-"))
			{
				Assert.Fail("检测到 API Key，但该测试需要 SMTP 凭据 (xsmtpsib-...)");
			}

			// 2. 调用真实发送
			try
			{
				var result = await service.SendBulkEmailAsync(message, settings);
				Assert.IsTrue(result, "Brevo 真实邮件发送失败");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ 异常: {ex.Message}");
				throw;
			}
		}
	}
}