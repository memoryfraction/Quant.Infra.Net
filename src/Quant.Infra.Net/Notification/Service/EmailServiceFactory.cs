using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quant.Infra.Net.Notification.Model;
using System;

namespace Quant.Infra.Net.Notification.Service
{
	public class EmailServiceFactory
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly IConfiguration _configuration;

		public EmailServiceFactory(IServiceProvider serviceProvider, IConfiguration configuration)
		{
			_configuration = configuration;
			_serviceProvider = serviceProvider;
		}

		public IEmailService GetService(int recipientCount)
		{
			// 1. 获取配置中的策略类型 (Commercial, Personal 或 Auto)
			string strategy = _configuration["Email:Type"];

			// 2. 根据策略和收件人数量决定具体服务类型
			if (strategy.ToLower() == "Commercial".ToLower())
			{
				return _serviceProvider.GetRequiredService<CommercialEmailService>();
			}

			if (strategy.ToLower() == "Personal".ToLower())
			{
				return _serviceProvider.GetRequiredService<PersonalEmailService>();
			}

			throw new InvalidOperationException("Invalid email service type configured.");
		}

		
	}
}
