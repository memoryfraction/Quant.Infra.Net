using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
	// 受JWT保护的接口
	[HttpGet("protected")]
	[Authorize] // 核心：必须携带有效JWT才能访问
	public IActionResult GetProtectedData()
	{
		// 从JWT中解析用户信息
		var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
			?? User.FindFirst("sub")?.Value;

		return Ok(new
		{
			Message = "✅ JWT 认证成功！",
			Username = username,
			Timestamp = DateTime.UtcNow
		});
	}
}