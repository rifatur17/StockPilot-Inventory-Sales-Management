using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.Web.Controllers;

[Authorize(Roles = "Admin,Manager,InventoryStaff")]
public sealed class InventoryController(BusinessService s) : Controller
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index(string? search, int page = 1, bool low = false, CancellationToken ct = default) => View(await s.Products(ActorId, search, page: page, low: low, ct: ct));
    public async Task<IActionResult> Movements(Guid? productId, int page = 1, CancellationToken ct = default) => View(await s.Movements(ActorId, productId, page, ct: ct));
    [HttpGet]
    public async Task<IActionResult> Adjust(Guid id, CancellationToken ct)
    {
        ViewBag.Product = await s.Product(ActorId, id, ct);
        return View(new AdjustmentInput(id, 0, "", 0, Guid.NewGuid()));
    }
    [HttpPost]
    public async Task<IActionResult> Adjust(AdjustmentInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
            try
            {
                await s.Adjust(ActorId, input, ct);
                TempData["Success"] = "Stock adjusted and audited.";
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessException e) { ModelState.AddModelError("", e.Message); }
        ViewBag.Product = await s.Product(ActorId, input.ProductId, ct);
        return View(input);
    }
}
[Authorize(Roles = "Admin,Manager")]
public sealed class AuditController(BusinessService s) : Controller
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) => View(await s.AuditHistory(User.FindFirstValue(ClaimTypes.NameIdentifier)!, page, ct: ct));
}
