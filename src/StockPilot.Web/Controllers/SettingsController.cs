using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class SettingsController(BusinessService s) : Controller
{
    [HttpGet] public async Task<IActionResult> Index(CancellationToken ct) => View(await s.Settings(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct));
    [HttpPost]
    public async Task<IActionResult> Index(SettingsInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
            try
            {
                await s.SaveSettings(User.FindFirstValue(ClaimTypes.NameIdentifier)!, input, ct);
                TempData["Success"] = "Business settings saved.";
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessException ex) { ModelState.AddModelError("", ex.Message); }
        return View(input);
    }
}
