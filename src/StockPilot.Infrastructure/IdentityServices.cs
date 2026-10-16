using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using StockPilot.Core;
namespace StockPilot.Infrastructure;
public sealed class ActiveSignInManager(UserManager<ApplicationUser> users, IHttpContextAccessor accessor, IUserClaimsPrincipalFactory<ApplicationUser> factory, IOptions<IdentityOptions> options, ILogger<SignInManager<ApplicationUser>> logger, IAuthenticationSchemeProvider schemes, IUserConfirmation<ApplicationUser> confirmation)
    : SignInManager<ApplicationUser>(users, accessor, factory, options, logger, schemes, confirmation)
{
    public override async Task<bool> CanSignInAsync(ApplicationUser user) => user.Active && await base.CanSignInAsync(user);
}
public sealed class UserService(AppDb db, LedgerGate gate, UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles)
{
    public async Task<Actor> Actor(string id, CancellationToken ct = default)
    {
        var u = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (u is null || !u.Active) throw new BusinessException("Please sign in again.", 401);
        var names = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == id select r.Name!).ToListAsync(ct);
        return new Actor(id, names);
    }
    public Task<Guid> Bootstrap(string email, string password, CancellationToken ct = default) => gate.Run(async () =>
    {
        foreach (var name in Core.Roles.All) if (!await roles.RoleExistsAsync(name)) Check(await roles.CreateAsync(new IdentityRole(name)));
        if ((await users.GetUsersInRoleAsync(Core.Roles.Admin)).Count > 0) throw new BusinessException("Administrator already initialized. Use account administration.", 409);
        var u = new ApplicationUser { UserName = email, Email = email, DisplayName = "Administrator" };
        Check(await users.CreateAsync(u, password)); Check(await users.AddToRoleAsync(u, Core.Roles.Admin));
        Audit(u.Id, "Bootstrap", u.Id); return Guid.Empty;
    }, ct);
    public Task<Guid> Create(string actorId, string email, string name, string password, string role, CancellationToken ct = default) => gate.Run(async () =>
    {
        (await Actor(actorId, ct)).Demand(); ValidateRole(role);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160 || email.Length > 254 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)) throw new BusinessException("A valid email and display name are required.");
        var u = new ApplicationUser { UserName = email.Trim(), Email = email.Trim(), DisplayName = name.Trim() };
        Check(await users.CreateAsync(u, password)); Check(await users.AddToRoleAsync(u, role));
        Audit(actorId, "UserCreated", u.Id); return Guid.Empty;
    }, ct);
    public Task<Guid> Change(string actorId, string userId, string role, bool active, CancellationToken ct = default) => gate.Run(async () =>
    {
        (await Actor(actorId, ct)).Demand(); ValidateRole(role);
        var u = await users.FindByIdAsync(userId) ?? throw new BusinessException("User not found.", 404);
        var old = await users.GetRolesAsync(u);
        if (old.Contains(Core.Roles.Admin) && (!active || role != Core.Roles.Admin))
        {
            var count = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id join person in db.Users on ur.UserId equals person.Id where r.Name == Core.Roles.Admin && person.Active select person.Id).CountAsync(ct);
            if (count <= 1) throw new BusinessException("The last active administrator cannot be disabled or demoted.", 409);
        }
        Check(await users.RemoveFromRolesAsync(u, old)); Check(await users.AddToRoleAsync(u, role));
        u.Active = active; Check(await users.UpdateAsync(u)); Check(await users.UpdateSecurityStampAsync(u));
        Audit(actorId, "UserPermissionsChanged", u.Id); return Guid.Empty;
    }, ct);
    public Task<Guid> Profile(string actorId, string name, CancellationToken ct = default) => gate.Run(async () =>
    {
        await Actor(actorId, ct);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160) throw new BusinessException("Display name must contain 1–160 characters.");
        var u = await users.FindByIdAsync(actorId) ?? throw new BusinessException("User not found.", 404);
        u.DisplayName = name.Trim(); Check(await users.UpdateAsync(u)); Audit(actorId, "ProfileUpdated", u.Id); return Guid.Empty;
    }, ct);
    public async Task<IReadOnlyList<UserDto>> List(string actorId, CancellationToken ct = default)
    {
        (await Actor(actorId, ct)).Demand();
        var people = await db.Users.AsNoTracking().OrderBy(x => x.Email).Take(200).ToListAsync(ct);
        var map = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id select new { ur.UserId, r.Name }).ToListAsync(ct);
        return people.Select(x => new UserDto(x.Id, x.Email!, x.DisplayName, x.Active, map.Where(y => y.UserId == x.Id).Select(y => y.Name!).ToArray())).ToArray();
    }
    private static void ValidateRole(string role) { if (!Core.Roles.All.Contains(role)) throw new BusinessException("Unknown role."); }
    private static void Check(IdentityResult result) { if (!result.Succeeded) throw new BusinessException(string.Join(" ", result.Errors.Select(x => x.Description))); }
    private void Audit(string actor, string action, string id) => db.Activities.Add(new ActivityLog { ActorId = actor, Action = action, EntityType = "User", EntityId = id, Summary = action });
}
public sealed record UserDto(string Id, string Email, string DisplayName, bool Active, string[] Roles);
