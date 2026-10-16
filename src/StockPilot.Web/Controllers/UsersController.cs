using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Infrastructure;
namespace StockPilot.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class UsersController(UserService users) : Controller
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index(CancellationToken ct) => View(await users.List(ActorId, ct));
    [HttpGet] public IActionResult Create() => View(new UserForm());
    [HttpPost]
    public async Task<IActionResult> Create(UserForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(form);
        try
        {
            await users.Create(ActorId, form.Email, form.DisplayName, form.Password, form.Role, ct);
        }
        catch (StockPilot.Core.BusinessException e) { ModelState.AddModelError("", e.Message); return View(form); }
        TempData["Success"] = "User created.";
        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> Change(string id, string role, bool active, CancellationToken ct)
    {
        await users.Change(ActorId, id, role, active, ct);
        TempData["Success"] = "Permissions updated; existing sessions revoked.";
        return RedirectToAction(nameof(Index));
    }
}
public sealed class UserForm
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, StringLength(160)] public string DisplayName { get; set; } = "";
    [Required, MinLength(12), DataType(DataType.Password)] public string Password { get; set; } = "";
    [Required] public string Role { get; set; } = "SalesStaff";
}
