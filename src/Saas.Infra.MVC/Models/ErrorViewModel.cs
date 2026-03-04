namespace Saas.Infra.MVC.Models
{
	/// <summary>
	/// 表示错误页面的视图模型。包含请求ID和是否显示请求ID的标志。
	/// Represents the view model for the error page, including request ID and flag to show request ID.
	/// </summary>
	public class ErrorViewModel
	{
		/// <summary>
		/// 获取或设置请求的唯一标识符。
		/// Gets or sets the unique identifier for the request.
		/// </summary>
		public string? RequestId { get; set; }

		/// <summary>
		/// 获取一个值，指示是否应显示请求ID。当RequestId不为空时返回true。
		/// Gets a value indicating whether the request ID should be displayed. Returns true when RequestId is not empty.
		/// </summary>
		public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
	}
}
