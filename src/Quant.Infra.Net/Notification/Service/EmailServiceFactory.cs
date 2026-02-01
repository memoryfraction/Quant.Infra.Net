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
			return _serviceProvider.GetRequiredService<MailKitCommercialService>();
		}
	}
}
