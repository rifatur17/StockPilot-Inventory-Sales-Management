using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Infrastructure;
namespace StockPilot.Web.Controllers;
[Authorize(Roles="Admin,Manager,InventoryStaff")]
public sealed class LookupsController(BusinessService svc):Controller
{
    private string ActorId=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index(CancellationToken ct){ViewBag.Units=await svc.Lookups(ActorId,"units",ct);return View(await svc.Lookups(ActorId,"categories",ct));}
    [HttpPost] public async Task<IActionResult> Save(string kind,Guid? id,string name,CancellationToken ct){await svc.SaveLookup(ActorId,kind,id,name,ct);TempData["Success"]="Saved.";return RedirectToAction(nameof(Index));}
    [HttpPost] public async Task<IActionResult> Delete(string kind,Guid id,CancellationToken ct){await svc.DeleteLookup(ActorId,kind,id,ct);TempData["Success"]="Deleted unreferenced lookup.";return RedirectToAction(nameof(Index));}
}
