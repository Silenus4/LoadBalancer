using Microsoft.AspNetCore.Mvc;

namespace LoadBalancerTestAPI.Controllers;

[ApiController]
[Route("health")]
public class HealthCheckController : ControllerBase
{
    [HttpGet]
    public IActionResult HealthCheck()
    {
        return Ok("Healthy");
    }
}