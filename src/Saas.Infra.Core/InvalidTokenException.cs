using System.Runtime.Serialization;

namespace Saas.Infra.Core
{
	/// <summary>
	/// 表示JWT令牌无效时抛出的异常。
	/// 当令牌验证失败、令牌过期或令牌格式不正确时使用此异常。
	/// </summary>
	[Serializable]
	public class InvalidTokenException : Exception
	{
		/// <summary>
		/// 初始化 <see cref="InvalidTokenException"/> 类的新实例。
		/// </summary>
		public InvalidTokenException() : base("The token is invalid.") { }

		/// <summary>
		/// 使用指定的错误消息初始化 <see cref="InvalidTokenException"/> 类的新实例。
		/// </summary>
		/// <param name="message">描述错误的消息。</param>
		public InvalidTokenException(string message) : base(message) { }

		/// <summary>
		/// 使用指定的错误消息和对作为此异常原因的内部异常的引用来初始化 <see cref="InvalidTokenException"/> 类的新实例。
		/// </summary>
		/// <param name="message">解释异常原因的错误消息。</param>
		/// <param name="innerException">导致当前异常的异常。</param>
		public InvalidTokenException(string message, Exception innerException) 
			: base(message, innerException) { }

		/// <summary>
		/// 用序列化数据初始化 <see cref="InvalidTokenException"/> 类的新实例。
		/// </summary>
		/// <param name="info">包含序列化对象数据的对象。</param>
		/// <param name="context">有关源或目标的上下文信息。</param>
		protected InvalidTokenException(SerializationInfo info, StreamingContext context) 
			: base(info, context) { }
	}
}
