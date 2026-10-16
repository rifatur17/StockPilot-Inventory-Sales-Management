using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockPilot.Core;
using StockPilot.Infrastructure;
namespace StockPilot.Web.Controllers;
public sealed class ProductsController(BusinessService svc) : Controller
{
    private string ActorId=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index(string? search,Guid? categoryId,int page=1,string sort="name",bool low=false,bool archived=false,CancellationToken ct=default)
    { ViewBag.Categories=await svc.Lookups(ActorId,"categories",ct); return View(await svc.Products(ActorId,search,categoryId,page,20,sort,low,archived,ct)); }
    public async Task<IActionResult> Details(Guid id,CancellationToken ct)=>View(await svc.Product(ActorId,id,ct));
    [Authorize(Roles="Admin,Manager,InventoryStaff"),HttpGet] public async Task<IActionResult> Create(CancellationToken ct) { await Lists(ct); return View("Edit",new ProductForm()); }
    [Authorize(Roles="Admin,Manager,InventoryStaff"),HttpGet] public async Task<IActionResult> Edit(Guid id,CancellationToken ct)
    { var p=await svc.Product(ActorId,id,ct); await Lists(ct); return View(new ProductForm{Id=p.Id,Sku=p.Sku,Name=p.Name,CategoryId=p.CategoryId,UnitId=p.UnitId,SellingPrice=p.SellingPrice,MinimumStock=p.MinimumStock,Description=p.Description,Barcode=p.Barcode}); }
    [Authorize(Roles="Admin,Manager,InventoryStaff"),HttpPost] public Task<IActionResult> Create(ProductForm form,CancellationToken ct)=>Save(form,null,ct);
    [Authorize(Roles="Admin,Manager,InventoryStaff"),HttpPost] public Task<IActionResult> Edit(Guid id,ProductForm form,CancellationToken ct)=>Save(form,id,ct);
    private async Task<IActionResult> Save(ProductForm f,Guid? id,CancellationToken ct)
    {
        f.Id=id;
        if(ModelState.IsValid) try { var result=await svc.SaveProduct(ActorId,id,new ProductInput(f.Sku,f.Name,f.CategoryId,f.UnitId,f.SellingPrice,f.MinimumStock,f.Description,f.Barcode),ct); TempData["Success"]="Product saved."; return RedirectToAction(nameof(Details),new{id=result}); } catch(BusinessException e){ModelState.AddModelError("",e.Message);}
        await Lists(ct); return View("Edit",f);
    }
    [Authorize(Roles="Admin,Manager,InventoryStaff"),HttpPost] public async Task<IActionResult> Archive(Guid id,CancellationToken ct) { await svc.ArchiveProduct(ActorId,id,ct); TempData["Success"]="Product archived; history retained."; return RedirectToAction(nameof(Index)); }
    private async Task Lists(CancellationToken ct){ViewBag.Categories=await svc.Lookups(ActorId,"categories",ct);ViewBag.Units=await svc.Lookups(ActorId,"units",ct);}
}
public sealed class ProductForm
{
    public Guid? Id {get;set;}
    [Required,StringLength(64)] public string Sku {get;set;}="";
    [Required,StringLength(160)] public string Name {get;set;}="";
    public Guid CategoryId {get;set;} public Guid UnitId {get;set;}
    public decimal SellingPrice {get;set;} public decimal MinimumStock {get;set;}
    [StringLength(1000)] public string? Description {get;set;}
    [StringLength(64)] public string? Barcode {get;set;}
}
public sealed record Pager(int Number,int Size,int Total);
