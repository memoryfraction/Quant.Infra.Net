namespace Quant.Infra.Net.Notification.Model
{
    public class MessageBody
    {
        public string msgtype { get; set; } = "text";
        public text text { get; set; }
    }

    public class text
    {
        public string content { get; set; }
    }
}