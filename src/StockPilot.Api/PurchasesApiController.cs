using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.Api;
[ApiController,Route("api/v1/purchases"),Authorize(Roles="Admin,Manager,InventoryStaff")]
public sealed class PurchasesApiController(BusinessService s):ControllerBase
{
    private string ActorId=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]public async Task<IActionResult> List(string? search,Guid? partnerId,DocumentState? state,DateTime? from,DateTime? to,int page=1,CancellationToken ct=default)=>Ok(await s.Documents(ActorId,DocumentKind.Purchase,search,partnerId,state,from,to,page,ct:ct));
    [HttpGet("{id:guid}")]public async Task<IActionResult> Details(Guid id,CancellationToken ct){var d=await s.Document(ActorId,id,ct);return d.Kind==DocumentKind.Purchase?Ok(d):NotFound();}
    [HttpPost]public async Task<IActionResult> Draft(DraftInput input,CancellationToken ct){if(input.Kind!=DocumentKind.Purchase)return BadRequest();var id=await s.Draft(ActorId,input,ct);return Created($"/api/v1/purchases/{id}",new{id});}
    [HttpPost("{id:guid}/receive")]public async Task<IActionResult> Receive(Guid id,KeyInput input,CancellationToken ct)=>Ok(new{id=await s.Receive(ActorId,id,input.Key,ct)});
    [HttpPost("{id:guid}/cancel")]public async Task<IActionResult> Cancel(Guid id,KeyInput input,CancellationToken ct){var d=await s.Document(ActorId,id,ct);if(d.Kind!=DocumentKind.Purchase)return NotFound();return Ok(new{id=await s.Cancel(ActorId,id,input.Key,ct)});}
}
