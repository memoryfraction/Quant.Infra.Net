using System.Runtime.Serialization;

namespace Saas.Infra.Core
{
	/// <summary>
	/// 表示JWT令牌无效时抛出的异常。当令牌验证失败、令牌过期或令牌格式不正确时使用此异常。
	/// Represents an exception thrown when a JWT token is invalid, used when token validation fails, token is expired, or token format is incorrect.
	/// </summary>
	[Serializable]
	public class InvalidTokenException : Exception
	{
		/// <summary>
		/// 初始化 <see cref="InvalidTokenException"/> 类的新实例。 / Initializes a new instance of the <see cref="InvalidTokenException"/> class.
		/// </summary>
		public InvalidTokenException() : base("The token is invalid.") { }

		/// <summary>
		/// 使用指定的错误消息初始化 <see cref="InvalidTokenException"/> 类的新实例。 / Initializes a new instance with a specified error message.
		/// </summary>
		/// <param name="message">描述错误的消息。 / The error message that explains the reason for the exception.</param>
		public InvalidTokenException(string message) : base(message) { }

		/// <summary>
		/// 使用指定的错误消息和对作为此异常原因的内部异常的引用来初始化 <see cref="InvalidTokenException"/> 类的新实例。 / Initializes a new instance with a specified error message and a reference to the inner exception that is the cause of this exception.
		/// </summary>
		/// <param name="message">解释异常原因的错误消息。 / The error message that explains the reason for the exception.</param>
		/// <param name="innerException">导致当前异常的异常。 / The exception that is the cause of the current exception.</param>
		public InvalidTokenException(string message, Exception innerException) 
			: base(message, innerException) { }

		/// <summary>
		/// 用序列化数据初始化 <see cref="InvalidTokenException"/> 类的新实例。 / Initializes a new instance from serialized data.
		/// </summary>
		/// <param name="info">包含序列化对象数据的对象。 / The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
		/// <param name="context">有关源或目标的上下文信息。 / The StreamingContext that contains contextual information about the source or destination.</param>
		protected InvalidTokenException(SerializationInfo info, StreamingContext context) 
			: base(info, context) { }
	}
}
