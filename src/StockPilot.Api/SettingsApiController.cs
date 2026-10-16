using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.Api;
[ApiController,Route("api/v1/settings")]
public sealed class SettingsApiController(BusinessService s):ControllerBase
{
    [HttpGet]public async Task<IActionResult> Get(CancellationToken ct)=>Ok(await s.Settings(User.FindFirstValue(ClaimTypes.NameIdentifier)!,ct));
    [HttpPut,Authorize(Roles="Admin")]public async Task<IActionResult> Save(SettingsInput input,CancellationToken ct){await s.SaveSettings(User.FindFirstValue(ClaimTypes.NameIdentifier)!,input,ct);return NoContent();}
}
