using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Infrastructure;
namespace StockPilot.Web.Controllers;
[Authorize(Roles="Admin,Manager")]
public sealed class ReportsController(BusinessService s):Controller
{
    public async Task<IActionResult> Index(DateTime? from,DateTime? to,string? period,CancellationToken ct)
    {
        if(period=="day"){from=DateTime.UtcNow.Date;to=from;}else if(period=="week"){from=DateTime.UtcNow.Date.AddDays(-6);to=DateTime.UtcNow.Date;}else if(period=="month"){from=new DateTime(DateTime.UtcNow.Year,DateTime.UtcNow.Month,1);to=DateTime.UtcNow.Date;}
        return View(await s.Report(User.FindFirstValue(ClaimTypes.NameIdentifier)!,from,to,ct));
    }
}
