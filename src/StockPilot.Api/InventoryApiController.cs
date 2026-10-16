using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.Api;
[ApiController,Route("api/v1/inventory"),Authorize(Roles="Admin,Manager,InventoryStaff")]
public sealed class InventoryApiController(BusinessService s):ControllerBase
{
    private string ActorId=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]public async Task<IActionResult> Index(string? search,int page=1,bool low=false,CancellationToken ct=default)=>Ok(await s.Products(ActorId,search,page:page,low:low,ct:ct));
    [HttpGet("movements")]public async Task<IActionResult> Movements(Guid? productId,int page=1,CancellationToken ct=default)=>Ok(await s.Movements(ActorId,productId,page,ct:ct));
    [HttpPost("adjustments")]public async Task<IActionResult> Adjust(AdjustmentInput input,CancellationToken ct)=>Ok(new{id=await s.Adjust(ActorId,input,ct)});
    [HttpGet("audit"),Authorize(Roles="Admin,Manager")]public async Task<IActionResult> Audit(int page=1,CancellationToken ct=default)=>Ok(await s.AuditHistory(ActorId,page,ct:ct));
}
