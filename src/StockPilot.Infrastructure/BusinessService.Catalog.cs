using Microsoft.EntityFrameworkCore;
using StockPilot.Core;
namespace StockPilot.Infrastructure;
public sealed partial class BusinessService
{
    public async Task<Page<ProductDto>> Products(string actorId, string? search = null, Guid? categoryId = null, int page = 1, int size = 20, string sort = "name", bool low = false, bool archived = false, CancellationToken ct = default)
    {
        var actor = await Actor(actorId, ct); var paging = Paging(page, size); var costs = !actor.Roles.Contains(Roles.Sales);
        var q = db.Products.AsNoTracking().Where(x => x.Active != archived);
        if (!string.IsNullOrWhiteSpace(search)) { var s = search.Trim(); q = q.Where(x => x.Name.Contains(s) || x.Sku.Contains(s) || (x.Barcode != null && x.Barcode.Contains(s))); }
        if (categoryId.HasValue) q = q.Where(x => x.CategoryId == categoryId);
        if (low) q = q.Where(x => x.Stock <= x.MinimumStock);
        var total = await q.CountAsync(ct);
        q = sort switch { "sku" => q.OrderBy(x => x.Sku).ThenBy(x => x.Id), "newest" => q.OrderByDescending(x => x.Id), _ => q.OrderBy(x => x.Name).ThenBy(x => x.Id) };
        var list = await q.Skip((paging.Page - 1) * paging.Size).Take(paging.Size).Select(x => new ProductDto(x.Id,x.Sku,x.Name,x.CategoryId,x.UnitId,x.SellingPrice,costs ? x.AverageCost : null,x.Stock,x.MinimumStock,x.Active,x.Description,x.Barcode)).ToListAsync(ct);
        return new Page<ProductDto>(list, total, paging.Page, paging.Size);
    }
    public async Task<ProductDto> Product(string actorId, Guid id, CancellationToken ct = default)
    {
        var a = await Actor(actorId, ct); var x = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Product not found.", 404);
        return new ProductDto(x.Id,x.Sku,x.Name,x.CategoryId,x.UnitId,x.SellingPrice,a.Roles.Contains(Roles.Sales) ? null : x.AverageCost,x.Stock,x.MinimumStock,x.Active,x.Description,x.Barcode);
    }
    public Task<Guid> SaveProduct(string actorId, Guid? id, ProductInput input, CancellationToken ct = default) => gate.Run(async () =>
    {
        (await Actor(actorId, ct)).Demand(Roles.Manager, Roles.Inventory);
        var sku = Text(input.Sku, "SKU", 64, true).ToUpperInvariant(); var name = Text(input.Name, "Name", 160, true);
        Money.Amount(input.SellingPrice, "Selling price"); if (input.MinimumStock < 0 || input.MinimumStock > 1000000 || decimal.Round(input.MinimumStock,3) != input.MinimumStock) throw new BusinessException("Minimum stock must be 0–1000000 with at most 3 decimals.");
        if (!await db.Categories.AnyAsync(x => x.Id == input.CategoryId, ct) || !await db.Units.AnyAsync(x => x.Id == input.UnitId, ct)) throw new BusinessException("Select an existing category and unit.");
        if (await db.Products.AnyAsync(x => x.Sku == sku && x.Id != id, ct)) throw new BusinessException("SKU already exists.", 409);
        var p = id.HasValue ? await db.Products.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Product not found.", 404) : new Product();
        if (!id.HasValue) db.Products.Add(p);
        var oldPrice = p.SellingPrice; p.Sku = sku; p.Name = name; p.CategoryId = input.CategoryId; p.UnitId = input.UnitId; p.SellingPrice = input.SellingPrice; p.MinimumStock = input.MinimumStock; p.Description = Text(input.Description,"Description",1000); p.Barcode = Text(input.Barcode,"Barcode",64);
        Audit(actorId, id.HasValue ? "ProductUpdated" : "ProductCreated", "Product", p.Id, $"Price {oldPrice:0.00} → {p.SellingPrice:0.00}"); return p.Id;
    }, ct);
    public Task<Guid> ArchiveProduct(string actorId, Guid id, CancellationToken ct = default) => gate.Run(async () =>
    {
        (await Actor(actorId, ct)).Demand(Roles.Manager, Roles.Inventory);
        var p = await db.Products.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Product not found.", 404);
        if (p.Stock != 0) throw new BusinessException("Adjust or sell remaining stock before archiving.",409);
        p.Active = false; Audit(actorId,"ProductArchived","Product",id); return id;
    },ct);
    public async Task<IReadOnlyList<LookupDto>> Lookups(string actorId, string kind, CancellationToken ct = default)
    {
        await Actor(actorId,ct);
        return kind switch { "categories" => await db.Categories.AsNoTracking().OrderBy(x => x.Name).Select(x => new LookupDto(x.Id,x.Name)).ToListAsync(ct), "units" => await db.Units.AsNoTracking().OrderBy(x => x.Name).Select(x => new LookupDto(x.Id,x.Name)).ToListAsync(ct), _ => throw new BusinessException("Unknown lookup.",404) };
    }
    public Task<Guid> SaveLookup(string actorId, string kind, Guid? id, string name, CancellationToken ct = default) => gate.Run(async () =>
    {
        (await Actor(actorId,ct)).Demand(Roles.Manager,Roles.Inventory); name = Text(name,"Name",100,true);
        Guid result;
        if (kind == "categories") { if (await db.Categories.AnyAsync(x => x.Name == name && x.Id != id,ct)) throw new BusinessException("Name already exists.",409); var x = id.HasValue ? await db.Categories.SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw new BusinessException("Category not found.",404) : new Category(); x.Name=name; if (!id.HasValue) db.Categories.Add(x); result=x.Id; }
        else if (kind == "units") { if (await db.Units.AnyAsync(x => x.Name == name && x.Id != id,ct)) throw new BusinessException("Name already exists.",409); var x = id.HasValue ? await db.Units.SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw new BusinessException("Unit not found.",404) : new Unit(); x.Name=name; if (!id.HasValue) db.Units.Add(x); result=x.Id; }
        else throw new BusinessException("Unknown lookup.",404);
        Audit(actorId,"LookupSaved",kind,result); return result;
    },ct);
    public Task<Guid> DeleteLookup(string actorId, string kind, Guid id, CancellationToken ct = default) => gate.Run(async () =>
    {
        (await Actor(actorId,ct)).Demand(Roles.Manager,Roles.Inventory);
        if (kind=="categories") { if(await db.Products.AnyAsync(x=>x.CategoryId==id,ct)) throw new BusinessException("Category is referenced by products; reassign them first.",409); db.Categories.Remove(await db.Categories.SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw new BusinessException("Not found.",404)); }
        else if(kind=="units") { if(await db.Products.AnyAsync(x=>x.UnitId==id,ct)) throw new BusinessException("Unit is referenced by products; reassign them first.",409); db.Units.Remove(await db.Units.SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw new BusinessException("Not found.",404)); }
        else throw new BusinessException("Unknown lookup.",404);
        Audit(actorId,"LookupDeleted",kind,id); return id;
    },ct);
    private static void PartnerPermission(Actor actor, string kind)
    {
        if(kind=="Supplier") actor.Demand(Roles.Manager,Roles.Inventory);
        else if(kind=="Customer") actor.Demand(Roles.Manager,Roles.Sales);
        else throw new BusinessException("Partner kind must be Supplier or Customer.");
    }
    public async Task<Page<PartnerDto>> Partners(string actorId, string kind, string? search = null, int page = 1, int size = 20, bool archived = false, CancellationToken ct = default)
    {
        PartnerPermission(await Actor(actorId,ct),kind); var pg=Paging(page,size);
        var q=db.Partners.AsNoTracking().Where(x=>x.Kind==kind && x.Active != archived); if(!string.IsNullOrWhiteSpace(search)) q=q.Where(x=>x.Name.Contains(search) || x.Contact.Contains(search));
        var total=await q.CountAsync(ct); var rows=await q.OrderBy(x=>x.Name).ThenBy(x=>x.Id).Skip((pg.Page-1)*pg.Size).Take(pg.Size).Select(x=>new PartnerDto(x.Id,x.Kind,x.Name,x.Contact,x.Address,x.Notes,x.Active)).ToListAsync(ct); return new Page<PartnerDto>(rows,total,pg.Page,pg.Size);
    }
    public async Task<PartnerDto> Partner(string actorId, Guid id, CancellationToken ct=default)
    {
        var x=await db.Partners.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw new BusinessException("Partner not found.",404); PartnerPermission(await Actor(actorId,ct),x.Kind); return new PartnerDto(x.Id,x.Kind,x.Name,x.Contact,x.Address,x.Notes,x.Active);
    }
    public Task<Guid> SavePartner(string actorId, string kind, Guid? id, PartnerInput input, CancellationToken ct=default) => gate.Run(async () =>
    {
        PartnerPermission(await Actor(actorId,ct),kind);
        var x=id.HasValue ? await db.Partners.SingleOrDefaultAsync(x=>x.Id==id && x.Kind==kind,ct) ?? throw new BusinessException("Partner not found.",404) : new Partner { Kind=kind };
        x.Name=Text(input.Name,"Name",160,true); x.Contact=Text(input.Contact,"Contact",160); x.Address=Text(input.Address,"Address",500); x.Notes=Text(input.Notes,"Notes",1000);
        if(!id.HasValue) db.Partners.Add(x); Audit(actorId,"PartnerSaved",kind,x.Id); return x.Id;
    },ct);
    public Task<Guid> ArchivePartner(string actorId, Guid id, CancellationToken ct=default) => gate.Run(async () =>
    {
        var x=await db.Partners.SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw new BusinessException("Partner not found.",404); PartnerPermission(await Actor(actorId,ct),x.Kind); x.Active=false; Audit(actorId,"PartnerArchived",x.Kind,x.Id); return x.Id;
    },ct);
}
