using System.Collections.Generic;

namespace Quant.Infra.Net.Notification.Model
{
	public class EmailMessage
	{
		public List<string> To { get; set; } = new();
		public string Subject { get; set; } = string.Empty;
		public string Body { get; set; } = string.Empty;
		public bool IsHtml { get; set; } = true;
	}
}
