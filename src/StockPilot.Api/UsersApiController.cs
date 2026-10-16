using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using StockPilot.Infrastructure;
namespace StockPilot.Api;
[Authorize(Roles = "Admin"), ApiController, Route("api/v1/users")]
public sealed class UsersApiController(UserService users) : ControllerBase
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await users.List(ActorId, ct));
    [HttpPost] public async Task<IActionResult> Create(NewUser input, CancellationToken ct) { await users.Create(ActorId, input.Email, input.DisplayName, input.Password, input.Role, ct); return StatusCode(201); }
    [HttpPut("{id}")] public async Task<IActionResult> Change(string id, ChangeUser input, CancellationToken ct) { await users.Change(ActorId, id, input.Role, input.Active, ct); return NoContent(); }
}
public sealed record NewUser(string Email, string DisplayName, string Password, string Role);
public sealed record ChangeUser(string Role, bool Active);
