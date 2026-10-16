using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StockPilot.Infrastructure;
using System.Security.Claims;
namespace StockPilot.Web.Controllers;

public sealed class AccountController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users, UserService service) : Controller
{
    [AllowAnonymous, HttpGet] public IActionResult Login() => View(new LoginForm());
    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginForm form, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return View(form);
        var result = await signIn.PasswordSignInAsync(form.Email, form.Password, false, true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Sign-in failed. Check your credentials or try again later.");
            return View(form);
        }
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var u = await users.GetUserAsync(User);
        return View(new ProfileForm { DisplayName = u!.DisplayName });
    }
    [HttpPost]
    public async Task<IActionResult> Profile(ProfileForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(form);
        await service.Profile(User.FindFirstValue(ClaimTypes.NameIdentifier)!, form.DisplayName, ct);
        TempData["Success"] = "Profile saved.";
        return RedirectToAction(nameof(Profile));
    }
    [AllowAnonymous]
    public IActionResult Denied()
    {
        Response.StatusCode = 403;
        return View();
    }
}
public sealed class LoginForm
{
    [Required, EmailAddress] public string Email { get; set; } = ""; [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
}
public sealed class ProfileForm
{
    [Required, StringLength(160)] public string DisplayName { get; set; } = "";
}
