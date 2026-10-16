using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.Api;
[ApiController,Route("api/v1/sales"),Authorize(Roles="Admin,Manager,SalesStaff")]
public sealed class SalesApiController(BusinessService s):ControllerBase
{
    private string ActorId=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet]public async Task<IActionResult> List(string? search,Guid? partnerId,DocumentState? state,DateTime? from,DateTime? to,int page=1,CancellationToken ct=default)=>Ok(await s.Documents(ActorId,DocumentKind.Sale,search,partnerId,state,from,to,page,ct:ct));
    [HttpGet("{id:guid}")]public async Task<IActionResult> Details(Guid id,CancellationToken ct){var d=await s.Document(ActorId,id,ct);return d.Kind==DocumentKind.Sale?Ok(d):NotFound();}
    [HttpPost]public async Task<IActionResult> Draft(DraftInput input,CancellationToken ct){if(input.Kind!=DocumentKind.Sale)return BadRequest();var id=await s.Draft(ActorId,input,ct);return Created($"/api/v1/sales/{id}",new{id});}
    [HttpPost("{id:guid}/confirm")]public async Task<IActionResult> Confirm(Guid id,KeyInput input,CancellationToken ct)=>Ok(new{id=await s.Confirm(ActorId,id,input.Key,ct)});
    [HttpPost("{id:guid}/cancel")]public async Task<IActionResult> Cancel(Guid id,KeyInput input,CancellationToken ct){var d=await s.Document(ActorId,id,ct);if(d.Kind!=DocumentKind.Sale)return NotFound();return Ok(new{id=await s.Cancel(ActorId,id,input.Key,ct)});}
    [HttpPost("{id:guid}/payments")]public async Task<IActionResult> Pay(Guid id,PaymentInput input,CancellationToken ct)=>Ok(new{id=await s.RecordPayment(ActorId,id,input,ct:ct)});
    [HttpPost("{id:guid}/refunds"),Authorize(Roles="Admin,Manager")]public async Task<IActionResult> Refund(Guid id,PaymentInput input,CancellationToken ct)=>Ok(new{id=await s.RecordPayment(ActorId,id,input,true,ct)});
    [HttpPost("{id:guid}/reverse"),Authorize(Roles="Admin,Manager")]public async Task<IActionResult> Reverse(Guid id,ReverseInput input,CancellationToken ct)=>Ok(new{id=await s.Reverse(ActorId,id,input,ct)});
}
