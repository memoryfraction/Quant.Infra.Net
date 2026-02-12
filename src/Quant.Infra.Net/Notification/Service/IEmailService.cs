using Quant.Infra.Net.Notification.Model;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Notification.Service
{
    public interface IEmailService
    {
		Task<bool> SendBulkEmailAsync(EmailMessage message, EmailSettingBase setting);
	}
}
