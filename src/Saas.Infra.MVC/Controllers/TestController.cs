using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Saas.Infra.MVC.Controllers
{
	/// <summary>
	/// 提供JWT认证测试接口。
	/// Provides JWT authentication test endpoints.
	/// </summary>
	[ApiController]
	[Route("api/test")]
	public class TestController : ControllerBase
	{
		/// <summary>
		/// 获取受JWT保护的数据。需要有效的JWT令牌才能访问。
		/// Retrieves protected data that requires a valid JWT token to access.
		/// </summary>
		/// <returns>
		/// 包含用户信息和时间戳的响应对象。
		/// A response object containing user information and timestamp.
		/// </returns>
		[HttpGet("protected")]
		[Authorize]
		public IActionResult GetProtectedData()
		{
			// Extract username from JWT claims
			var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
				?? User.FindFirst("sub")?.Value;

			return Ok(new
			{
				Message = "JWT authentication successful.",
				Username = username,
				Timestamp = DateTime.UtcNow
			});
		}
	}
}