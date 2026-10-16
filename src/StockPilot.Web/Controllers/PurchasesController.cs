using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.Web.Controllers;
[Authorize(Roles="Admin,Manager,InventoryStaff")]
public sealed class PurchasesController(BusinessService s):Controller
{
    private string ActorId=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index(string? search,Guid? partnerId,DocumentState? state,DateTime? from,DateTime? to,int page=1,CancellationToken ct=default)=>View("~/Views/Shared/Documents.cshtml",await s.Documents(ActorId,DocumentKind.Purchase,search,partnerId,state,from,to,page,ct:ct));
    public async Task<IActionResult> Create(CancellationToken ct){await s.Partners(ActorId,"Supplier",size:1,ct:ct);ViewBag.Kind="Purchase";ViewData["Title"]="New purchase";return View("~/Views/Shared/Cart.cshtml");}
    public async Task<IActionResult> Details(Guid id,CancellationToken ct){var d=await s.Document(ActorId,id,ct);if(d.Kind!=DocumentKind.Purchase)return NotFound();return View("~/Views/Shared/Document.cshtml",d);}
    [HttpPost]public async Task<IActionResult> Receive(Guid id,Guid key,CancellationToken ct){await s.Receive(ActorId,id,key,ct);TempData["Success"]="Purchase received. Stock and costing updated atomically.";return RedirectToAction(nameof(Details),new{id});}
    [HttpPost]public async Task<IActionResult> Cancel(Guid id,Guid key,CancellationToken ct){var d=await s.Document(ActorId,id,ct);if(d.Kind!=DocumentKind.Purchase)return NotFound();await s.Cancel(ActorId,id,key,ct);TempData["Success"]="Draft cancelled.";return RedirectToAction(nameof(Details),new{id});}
}
