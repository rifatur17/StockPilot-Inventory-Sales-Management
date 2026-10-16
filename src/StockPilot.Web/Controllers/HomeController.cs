using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Infrastructure;
using StockPilot.Core;
namespace StockPilot.Web.Controllers;
public sealed class HomeController(BusinessService s):Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var id=User.FindFirstValue(ClaimTypes.NameIdentifier)!;var actor=await s.Actor(id,ct);
        if(actor.Supervises)return View(new Overview(await s.Report(id,ct:ct),null,null));
        if(actor.Has(Roles.Inventory))return View(new Overview(null,await s.Products(id,low:true,size:5,ct:ct),null));
        return View(new Overview(null,null,await s.Documents(id,DocumentKind.Sale,size:5,ct:ct)));
    }
}
public sealed record Overview(ReportDto? Report,Page<ProductDto>? LowStock,Page<DocumentSummary>? Sales);
