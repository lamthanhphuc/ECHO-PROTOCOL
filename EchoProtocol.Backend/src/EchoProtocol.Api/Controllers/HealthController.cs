using EchoProtocol.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace EchoProtocol.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<HealthData>> Get()
    {
        return Ok(ApiResponse<HealthData>.Ok(
            new HealthData { Service = "EchoProtocol.Api" },
            "API is running"));
    }
}

public class HealthData
{
    public string Service { get; set; } = string.Empty;
}
