using Quant.Infra.Net.Notification.Model;
using System;
using System.Threading.Tasks;

namespace Quant.Infra.Net.Notification.Service
{
    public class CommercialService : IEmailService
    {
        

        public Task<bool> SendBulkEmailAsync(EmailMessage message, EmailSettings setting)
        {
            throw new NotImplementedException();
        }
    }
}
