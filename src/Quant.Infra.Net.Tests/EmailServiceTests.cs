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
		private string _testRecipient = "yuanyuancomecome@outlook.com";


		[TestInitialize]
		public void Setup()
		{
			// 1. 加载配置
			// 注意：如果希望自动包含环境变量，请在测试项目的 .csproj 中添加 NuGet 包:
			// <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="x.y.z" />
			// 添加包后可以恢复 .AddEnvironmentVariables() 调用。
			_config = new ConfigurationBuilder()
				.AddJsonFile("appsettings.test.json", optional: true)
				.AddUserSecrets<EmailIntegrationTests>() // 建议将授权码存在 Secret 中
				.Build();

			// 2. 模拟生产环境的 DI 容器注册
			var services = new ServiceCollection();

			// 必须注册具体的实现类，因为 Factory 内部使用 GetRequiredService<T>
			services.AddTransient<PersonalEmailService>();
			services.AddTransient<CommercialService>();

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

			// 从配置中读取邮箱设置
			var emailConfig = _config.GetSection("Email");
			var personalConfig = emailConfig.GetSection("Personal");
			
			var settings = new EmailSettings
			{
				SmtpServer = personalConfig["SmtpServer"] ?? "smtp.126.com",
				Port = int.Parse(personalConfig["Port"] ?? "465"),
				SenderEmail = personalConfig["SenderEmail"] ?? "test@126.com",
				Password = personalConfig["Password"] ?? "test-password",
				SenderName = personalConfig["SenderName"] ?? "Test Sender"
			};

			// Act
			// 1. 通过工厂获取服务 (recipientCount 为 1，应返回 PersonalEmailService)
			var service = _factory.GetService(recipients.Count);

			// 验证工厂路由是否符合 MVP 预期
			Assert.IsInstanceOfType(service, typeof(PersonalEmailService), "MVP 阶段且收件人<50时应使用个人服务");

			// 2. 调用真实发送 (注意：这里使用模拟配置，不会真实发送)
			var result = await service.SendBulkEmailAsync(message, settings);

			// Assert
			Assert.IsTrue(result, "邮件发送失败，请检查配置或网络连接。");
			Console.WriteLine($"个人邮件服务测试完成，收件人: {_testRecipient}");
			Console.WriteLine("注意：使用的是测试配置，可能不会真实发送邮件");
		}

		[TestMethod]
		public async Task MVP_SendCommercial()
		{
			// Arrange
			// 发送给两个真实收件人，测试 Brevo 真实邮件发送
			var recipients = new List<string> { _testRecipient, "rong.fan1031@gmail.com" };

		var message = new EmailMessage
		{
			To = recipients,
			Subject = $"🎯 量化交易系统邮件测试 - {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
			Body = @"
				<html>
				<head>
					<style>
						body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; line-height: 1.6; color: #333; }
						.container { max-width: 600px; margin: 0 auto; padding: 20px; }
						.header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }
						.content { background: #f8f9fa; padding: 30px; border-radius: 0 0 10px 10px; }
						.info-box { background: white; padding: 20px; border-radius: 8px; margin: 20px 0; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
						.success { color: #28a745; font-weight: bold; }
						.footer { text-align: center; color: #6c757d; font-size: 12px; margin-top: 30px; }
					</style>
				</head>
				<body>
					<div class='container'>
						<div class='header'>
							<h1>📈 Quant.Infra.Net</h1>
							<h2>量化交易基础设施邮件服务</h2>
						</div>
						<div class='content'>
							<div class='info-box'>
								<h3>🎉 邮件服务测试成功！</h3>
								<p>恭喜！您的 Brevo 商业邮件服务已成功配置并正常工作。</p>
								<p class='success'>✅ SMTP 连接正常</p>
								<p class='success'>✅ 身份验证通过</p>
								<p class='success'>✅ 邮件发送成功</p>
							</div>
							
							<div class='info-box'>
								<h3>📊 本次测试详情</h3>
								<table style='width: 100%; border-collapse: collapse;'>
									<tr><td style='padding: 8px; border-bottom: 1px solid #dee2e6;'><strong>发送时间:</strong></td><td style='padding: 8px; border-bottom: 1px solid #dee2e6;'>" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + @"</td></tr>
									<tr><td style='padding: 8px; border-bottom: 1px solid #dee2e6;'><strong>邮件服务:</strong></td><td style='padding: 8px; border-bottom: 1px solid #dee2e6;'>Brevo (SendinBlue)</td></tr>
									<tr><td style='padding: 8px; border-bottom: 1px solid #dee2e6;'><strong>项目名称:</strong></td><td style='padding: 8px; border-bottom: 1px solid #dee2e6;'>Quant.Infra.Net</td></tr>
									<tr><td style='padding: 8px; border-bottom: 1px solid #dee2e6;'><strong>收件人:</strong></td><td style='padding: 8px; border-bottom: 1px solid #dee2e6;'>" + string.Join(", ", recipients) + @"</td></tr>
									<tr><td style='padding: 8px;'><strong>服务类型:</strong></td><td style='padding: 8px;'>CommercialService (批量邮件)</td></tr>
								</table>
							</div>
							
							<div class='info-box'>
								<h3>🚀 下一步</h3>
								<p>现在您可以在生产环境中使用这个邮件服务了：</p>
								<ul>
									<li>📧 发送交易通知</li>
									<li>📊 发送报告邮件</li>
									<li>⚠️ 发送系统警报</li>
									<li>👥 发送批量通知</li>
								</ul>
							</div>
						</div>
						<div class='footer'>
							<p>此邮件由 Quant.Infra.Net 邮件服务自动发送</p>
							<p>测试方法: MVP_SendCommercial | 发送时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + @"</p>
						</div>
					</div>
				</body>
				</html>",
			IsHtml = true
		};

			// 从配置和用户机密中读取 Brevo 设置
			var emailConfig = _config.GetSection("Email");
			var commercialConfig = emailConfig.GetSection("Commercial");
			
			var settings = new EmailSettings
			{
				SmtpServer = commercialConfig["SmtpServer"] ?? "smtp-relay.brevo.com",
				Port = int.Parse(commercialConfig["Port"] ?? "587"),
				SenderEmail = commercialConfig["SenderEmail"] ?? "yuanhw512@gmail.com",
				SenderName = commercialConfig["SenderName"] ?? "Quant Lab System",
				Username = commercialConfig["Username"] ?? "", // SMTP 用户名，如果为空会在 CommercialService 中提示
				Password = commercialConfig["Password"] ?? throw new InvalidOperationException("Brevo SMTP Key not found in user secrets")
			};

			// 验证配置
			Console.WriteLine($"SMTP 服务器: {settings.SmtpServer}");
			Console.WriteLine($"端口: {settings.Port}");
			Console.WriteLine($"发件人: {settings.SenderEmail}");
			Console.WriteLine($"发件人名称: {settings.SenderName}");
			Console.WriteLine($"SMTP 用户名: {settings.Username}");
			Console.WriteLine($"收件人: {_testRecipient}");

			// Act
			// 1. 通过工厂获取服务 (recipientCount 为 1，但我们强制使用 CommercialService 进行测试)
			var service = new CommercialService(); // 直接使用 CommercialService 而不是通过工厂

			// 验证 SMTP 密钥是否正确读取
			// 验证 SMTP 密钥格式
			if (settings.Password.StartsWith("xkeysib-"))
			{
				Console.WriteLine("❌ 检测到 API Key，但需要 SMTP 凭据");
				Console.WriteLine("请按以下步骤获取 Brevo SMTP 凭据：");
				Console.WriteLine("1. 登录 Brevo 账户 (https://app.brevo.com)");
				Console.WriteLine("2. 点击右上角头像 → Settings → SMTP & API");
				Console.WriteLine("3. 在 SMTP 标签页中，点击 'Generate a new SMTP key'");
				Console.WriteLine("4. 复制显示的 SMTP 用户名和 SMTP 密钥");
				Console.WriteLine("5. 更新用户机密：");
				Console.WriteLine("   dotnet user-secrets set \"Email:Commercial:Username\" \"你的SMTP用户名\"");
				Console.WriteLine("   dotnet user-secrets set \"Email:Commercial:Password\" \"你的SMTP密钥\"");
				Assert.Fail("需要 SMTP 凭据，不是 API Key");
			}
			else if (settings.Password.StartsWith("xsmtpsib-"))
			{
				Console.WriteLine($"✅ 检测到正确的 SMTP 密钥格式");
				Console.WriteLine($"使用 SMTP 密钥: {settings.Password.Substring(0, Math.Min(20, settings.Password.Length))}...");
				
				if (string.IsNullOrEmpty(settings.Username))
				{
					Console.WriteLine("⚠️  缺少 SMTP 用户名，将尝试发送但可能失败");
					Console.WriteLine("如果发送失败，请获取 SMTP 用户名并运行：");
					Console.WriteLine("dotnet user-secrets set \"Email:Commercial:Username\" \"你的SMTP用户名\"");
				}
				else
				{
					Console.WriteLine($"✅ SMTP 用户名: {settings.Username}");
				}
			}
			else
			{
				Console.WriteLine($"❌ 未识别的密钥格式: {settings.Password.Substring(0, Math.Min(15, settings.Password.Length))}...");
				Assert.Fail("密钥格式不正确");
			}

			// 2. 调用 Brevo 真实发送
			Console.WriteLine("开始发送邮件...");
			
			try
			{
				var result = await service.SendBulkEmailAsync(message, settings);

				// Assert
				if (result)
				{
					Console.WriteLine($"✅ 真实邮件已通过 Brevo 发送至: {_testRecipient}");
					Console.WriteLine("请检查收件箱（包括垃圾邮件文件夹）");
				}
				else
				{
					Console.WriteLine("❌ 邮件发送失败，但没有抛出异常");
					Console.WriteLine("可能的问题：");
					Console.WriteLine("1. SMTP 认证失败 - 需要正确的 SMTP 用户名和密钥");
					Console.WriteLine("2. 发件人邮箱未在 Brevo 中验证");
					Console.WriteLine("3. 使用了 API Key 而不是 SMTP 密钥");
				}
				
				Assert.IsTrue(result, "Brevo 真实邮件发送失败 - 请检查 Brevo SMTP 配置");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ 邮件发送过程中发生异常: {ex.Message}");
				Console.WriteLine($"异常详情: {ex}");
				Console.WriteLine("解决方案：");
				Console.WriteLine("1. 获取 Brevo SMTP 凭据（不是 API Key）：");
				Console.WriteLine("   - 登录 https://app.brevo.com");
				Console.WriteLine("   - 右上角头像 → Settings → SMTP & API");
				Console.WriteLine("   - SMTP 标签页 → Generate a new SMTP key");
				Console.WriteLine("2. 更新用户机密：");
				Console.WriteLine("   dotnet user-secrets set \"Email:Commercial:Username\" \"你的SMTP用户名\"");
				Console.WriteLine("   dotnet user-secrets set \"Email:Commercial:Password\" \"你的SMTP密钥\"");
				Console.WriteLine("3. 确保发件人邮箱已在 Brevo 中验证");
				throw;
			}
		}
	}
}