using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Notification.Service
{
	public class EmailServiceFactory
	{
		private readonly IServiceProvider _serviceProvider;
		public EmailServiceFactory(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

		public IEmailService GetService(int recipientCount)
		{
			// 自动分流逻辑
			if (recipientCount < 50)
			{
				return _serviceProvider.GetRequiredService<PersonalEmailService>();
			}
			return _serviceProvider.GetRequiredService<CommercialService>();
		}

		/// <summary>
		/// 根据服务类型获取邮件服务
		/// </summary>
		/// <param name="serviceType">服务类型</param>
		/// <returns>邮件服务实例</returns>
		public IEmailService GetService(EmailServiceType serviceType)
		{
			return serviceType switch
			{
				EmailServiceType.Personal => _serviceProvider.GetRequiredService<PersonalEmailService>(),
				EmailServiceType.Commercial => _serviceProvider.GetRequiredService<CommercialService>(),
				_ => _serviceProvider.GetRequiredService<PersonalEmailService>()
			};
		}
	}

	/// <summary>
	/// 邮件服务类型枚举
	/// </summary>
	public enum EmailServiceType
	{
		/// <summary>
		/// 个人邮件服务（126邮箱SMTP）
		/// </summary>
		Personal = 1,

		/// <summary>
		/// 商业邮件服务（Brevo API）
		/// </summary>
		Commercial = 2
	}
}
