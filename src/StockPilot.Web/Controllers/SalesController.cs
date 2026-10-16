using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.Web.Controllers;
[Authorize(Roles="Admin,Manager,SalesStaff")]
public sealed class SalesController(BusinessService s):Controller
{
    private string ActorId=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index(string? search,Guid? partnerId,DocumentState? state,DateTime? from,DateTime? to,int page=1,CancellationToken ct=default)=>View("~/Views/Shared/Documents.cshtml",await s.Documents(ActorId,DocumentKind.Sale,search,partnerId,state,from,to,page,ct:ct));
    public IActionResult Create(){ViewBag.Kind="Sale";ViewData["Title"]="Point of sale";return View("~/Views/Shared/Cart.cshtml");}
    public async Task<IActionResult> Details(Guid id,CancellationToken ct){var d=await s.Document(ActorId,id,ct);if(d.Kind!=DocumentKind.Sale)return NotFound();return View("~/Views/Shared/Document.cshtml",d);}
    [HttpPost]public async Task<IActionResult> Confirm(Guid id,Guid key,CancellationToken ct){await s.Confirm(ActorId,id,key,ct);TempData["Success"]="Sale confirmed. Stock deducted once.";return RedirectToAction(nameof(Details),new{id});}
    [HttpPost]public async Task<IActionResult> Cancel(Guid id,Guid key,CancellationToken ct){var d=await s.Document(ActorId,id,ct);if(d.Kind!=DocumentKind.Sale)return NotFound();await s.Cancel(ActorId,id,key,ct);TempData["Success"]="Draft cancelled.";return RedirectToAction(nameof(Details),new{id});}
    [HttpPost]public async Task<IActionResult> Pay(Guid id,PaymentInput input,CancellationToken ct){if(!ModelState.IsValid)return BadRequest();await s.RecordPayment(ActorId,id,input,ct:ct);TempData["Success"]="Payment recorded.";return RedirectToAction(nameof(Details),new{id});}
    [HttpPost,Authorize(Roles="Admin,Manager")]public async Task<IActionResult> Refund(Guid id,PaymentInput input,CancellationToken ct){if(!ModelState.IsValid)return BadRequest();await s.RecordPayment(ActorId,id,input,true,ct);TempData["Success"]="Refund recorded. No stock was moved.";return RedirectToAction(nameof(Details),new{id});}
    [HttpPost,Authorize(Roles="Admin,Manager")]public async Task<IActionResult> Reverse(Guid id,ReverseInput input,CancellationToken ct){if(!ModelState.IsValid)return BadRequest();await s.Reverse(ActorId,id,input,ct);TempData["Success"]="Entire sale physically returned. Record any outstanding refund separately.";return RedirectToAction(nameof(Details),new{id});}
}
