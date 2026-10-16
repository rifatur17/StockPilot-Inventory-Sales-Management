using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Infrastructure;
using StockPilot.Core;
namespace StockPilot.Web.Controllers;
public sealed class PartnersController(BusinessService svc):Controller
{
    private string ActorId=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index(string kind="Customer",string? search=null,int page=1,bool archived=false,CancellationToken ct=default){ViewBag.Kind=kind;return View(await svc.Partners(ActorId,kind,search,page,20,archived,ct));}
    public async Task<IActionResult> Details(Guid id,CancellationToken ct)=>View(await svc.Partner(ActorId,id,ct));
    [HttpGet] public async Task<IActionResult> Create(string kind,CancellationToken ct){await svc.Partners(ActorId,kind,size:1,ct:ct);return View("Edit",new PartnerForm{Kind=kind});}
    [HttpGet] public async Task<IActionResult> Edit(Guid id,CancellationToken ct){var p=await svc.Partner(ActorId,id,ct);return View(new PartnerForm{Id=p.Id,Kind=p.Kind,Name=p.Name,Contact=p.Contact,Address=p.Address,Notes=p.Notes});}
    [HttpPost] public Task<IActionResult> Create(PartnerForm f,CancellationToken ct)=>Save(f,null,ct);
    [HttpPost] public Task<IActionResult> Edit(Guid id,PartnerForm f,CancellationToken ct)=>Save(f,id,ct);
    private async Task<IActionResult> Save(PartnerForm f,Guid? id,CancellationToken ct)
    {f.Id=id;if(ModelState.IsValid)try{var result=await svc.SavePartner(ActorId,f.Kind,id,new PartnerInput(f.Name,f.Contact,f.Address,f.Notes),ct);TempData["Success"]="Partner saved.";return RedirectToAction(nameof(Details),new{id=result});}catch(BusinessException e){ModelState.AddModelError("",e.Message);}return View("Edit",f);}
    [HttpPost] public async Task<IActionResult> Archive(Guid id,CancellationToken ct){var p=await svc.Partner(ActorId,id,ct);await svc.ArchivePartner(ActorId,id,ct);TempData["Success"]="Partner archived; transaction history retained.";return RedirectToAction(nameof(Index),new{kind=p.Kind});}
}
public sealed class PartnerForm
{
    public Guid? Id {get;set;} public string Kind {get;set;}="Customer";
    [Required,StringLength(160)] public string Name {get;set;}="";
    [StringLength(160)] public string? Contact {get;set;}
    [StringLength(500)] public string? Address {get;set;}
    [StringLength(1000)] public string? Notes {get;set;}
}
