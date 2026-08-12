namespace Quant.Infra.Net.Notification.Model
{
    /// <summary>
    /// 消息体结构，用于构建钉钉/企业微信等机器人的消息格式。
    /// Message body structure for building bot messages (DingTalk, WeChat Work).
    /// </summary>
    public class MessageBody
    {
        /// <summary>
        /// 消息类型，默认为 text / Message type, default is "text".
        /// </summary>
        public string msgtype { get; set; } = "text";

        /// <summary>
        /// 文本内容载体 / Text content payload.
        /// </summary>
        public text text { get; set; }
    }

    /// <summary>
    /// 消息内容包装类 / Wrapper for message content.
    /// </summary>
    public class text
    {
        /// <summary>
        /// 实际消息内容 / Actual message content string.
        /// </summary>
        public string content { get; set; }
    }
}
