using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.Api;
[ApiController, Route("api/v1")]
public sealed class CatalogApiController(BusinessService service) : ControllerBase
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet("products")] public async Task<IActionResult> Products(string? search, Guid? categoryId, int page=1,int size=20,string sort="name",bool low=false,bool archived=false,CancellationToken ct=default) => Ok(await service.Products(ActorId,search,categoryId,page,size,sort,low,archived,ct));
    [HttpGet("products/{id:guid}")] public async Task<IActionResult> Product(Guid id,CancellationToken ct) => Ok(await service.Product(ActorId,id,ct));
    [HttpPost("products")] public async Task<IActionResult> Create(ProductInput input,CancellationToken ct) { var id=await service.SaveProduct(ActorId,null,input,ct); return Created($"/api/v1/products/{id}",new{id}); }
    [HttpPut("products/{id:guid}")] public async Task<IActionResult> Update(Guid id,ProductInput input,CancellationToken ct) { await service.SaveProduct(ActorId,id,input,ct); return NoContent(); }
    [HttpPost("products/{id:guid}/archive")] public async Task<IActionResult> Archive(Guid id,CancellationToken ct) { await service.ArchiveProduct(ActorId,id,ct); return NoContent(); }
    [HttpGet("lookups/{kind}")] public async Task<IActionResult> Lookups(string kind,CancellationToken ct) => Ok(await service.Lookups(ActorId,kind,ct));
    [HttpPost("lookups/{kind}")] public async Task<IActionResult> CreateLookup(string kind,LookupInput input,CancellationToken ct) => Ok(new{id=await service.SaveLookup(ActorId,kind,null,input.Name,ct)});
    [HttpPut("lookups/{kind}/{id:guid}")] public async Task<IActionResult> UpdateLookup(string kind,Guid id,LookupInput input,CancellationToken ct) { await service.SaveLookup(ActorId,kind,id,input.Name,ct); return NoContent(); }
    [HttpDelete("lookups/{kind}/{id:guid}")] public async Task<IActionResult> DeleteLookup(string kind,Guid id,CancellationToken ct) { await service.DeleteLookup(ActorId,kind,id,ct); return NoContent(); }
    [HttpGet("partners/{kind}")] public async Task<IActionResult> Partners(string kind,string? search,int page=1,int size=20,bool archived=false,CancellationToken ct=default) => Ok(await service.Partners(ActorId,kind,search,page,size,archived,ct));
    [HttpGet("partner/{id:guid}")] public async Task<IActionResult> Partner(Guid id,CancellationToken ct) => Ok(await service.Partner(ActorId,id,ct));
    [HttpPost("partners/{kind}")] public async Task<IActionResult> CreatePartner(string kind,PartnerInput input,CancellationToken ct) => Ok(new{id=await service.SavePartner(ActorId,kind,null,input,ct)});
    [HttpPut("partners/{kind}/{id:guid}")] public async Task<IActionResult> UpdatePartner(string kind,Guid id,PartnerInput input,CancellationToken ct) { await service.SavePartner(ActorId,kind,id,input,ct); return NoContent(); }
    [HttpPost("partner/{id:guid}/archive")] public async Task<IActionResult> ArchivePartner(Guid id,CancellationToken ct) { await service.ArchivePartner(ActorId,id,ct); return NoContent(); }
}
public sealed record LookupInput([System.ComponentModel.DataAnnotations.Required] string Name);
