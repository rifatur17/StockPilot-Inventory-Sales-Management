using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StockPilot.Infrastructure;
namespace StockPilot.Api;

[ApiController, Route("api/v1/session")]
public sealed class SessionApiController(SignInManager<ApplicationUser> signIn, UserService users, IAntiforgery csrf) : ControllerBase
{
    [AllowAnonymous, HttpGet("csrf")] public IActionResult Token() => Ok(new { token = csrf.GetAndStoreTokens(HttpContext).RequestToken });
    [AllowAnonymous, HttpPost("login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest input)
    {
        var result = await signIn.PasswordSignInAsync(input.Email, input.Password, false, true);
        return result.Succeeded ? Ok(new
        {
            message = "Signed in. Fetch a new CSRF token."
        }) : Problem(statusCode: 401, title: "Sign-in failed. Check your credentials or try again later.");
    }
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return NoContent();
    }
    [HttpGet] public async Task<IActionResult> Me(CancellationToken ct) => Ok(await users.Actor(User.FindFirstValue(ClaimTypes.NameIdentifier)!, ct));
}
public sealed record LoginRequest([System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress] string Email, [System.ComponentModel.DataAnnotations.Required] string Password);
