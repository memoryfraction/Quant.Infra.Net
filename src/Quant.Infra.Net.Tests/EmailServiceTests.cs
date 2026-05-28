using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Quant.Infra.Net.Notification.Model;
using Quant.Infra.Net.Notification.Service;

namespace Quant.Infra.Net.Tests
{
	[TestClass]
	public class EmailIntegrationTests
	{
		private EmailServiceFactory _factory;
		private IConfiguration _config;
		private IServiceProvider _serviceProvider;
		private string _testRecipient = "yuanyuancomecome@outlook.com";

		[TestInitialize]
		public void Setup()
		{
			// 1. Load configuration.
			_config = new ConfigurationBuilder()
				.AddJsonFile("appsettings.json", optional: true)
				.AddUserSecrets<EmailIntegrationTests>()
				.Build();

			// 2. Register services as the application DI container would.
			var services = new ServiceCollection();

			// Register a mocked IHostEnvironment.
			var mockEnv = new Mock<IHostEnvironment>();
			mockEnv.Setup(m => m.EnvironmentName).Returns("Development");
			mockEnv.Setup(m => m.ContentRootPath).Returns(AppDomain.CurrentDomain.BaseDirectory);
			services.AddSingleton(mockEnv.Object);

			// Register concrete services.
			services.AddTransient<PersonalEmailService>();
			services.AddTransient<CommercialEmailService>();

			// Register IConfiguration.
			services.AddSingleton(_config);

			_serviceProvider = services.BuildServiceProvider();

			// 3. Initialize the factory.
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
				Body = "<h1>MVP send test</h1><p>Sent through EmailServiceFactory to PersonalEmailService.</p>",
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
				Subject = $"Quant trading system email test - {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
				Body = "<h1>Test content omitted...</h1>", // Keep the original HTML content shape.
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

			// Verification.
			Console.WriteLine($"CommercialEmailService resolved from DI with IHostEnvironment injected");

			if (settings.Password.StartsWith("xkeysib-"))
			{
				Assert.Fail("API key detected, but this test requires SMTP credentials (xsmtpsib-...)");
			}

			// 2. Send a real email.
			try
			{
				var result = await service.SendBulkEmailAsync(message, settings);
				Assert.IsTrue(result, "Brevo real email delivery failed");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Exception: {ex.Message}");
				throw;
			}
		}
	}
}
