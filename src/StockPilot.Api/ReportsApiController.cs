using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Infrastructure;
namespace StockPilot.Api;

[ApiController, Authorize(Roles = "Admin,Manager"), Route("api/v1/reports")]
public sealed class ReportsApiController(BusinessService s) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Report(DateTime? from, DateTime? to, CancellationToken ct) => Ok(await s.Report(User.FindFirstValue(ClaimTypes.NameIdentifier)!, from, to, ct));
}
