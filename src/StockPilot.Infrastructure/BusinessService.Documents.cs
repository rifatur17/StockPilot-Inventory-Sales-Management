using Microsoft.EntityFrameworkCore;
using StockPilot.Core;
namespace StockPilot.Infrastructure;
public sealed partial class BusinessService
{
    private static void DocumentPermission(Actor actor, DocumentKind kind)
    {if(kind==DocumentKind.Purchase) actor.Demand(Roles.Manager,Roles.Inventory);else if(kind==DocumentKind.Sale) actor.Demand(Roles.Manager,Roles.Sales);else throw new BusinessException("Unknown document kind.");}
    private static void Ownership(Actor actor, Document doc)
    {DocumentPermission(actor,doc.Kind);if(doc.Kind==DocumentKind.Sale && !actor.Supervises && doc.ActorId!=actor.Id)throw new BusinessException("Document not found.",404);}
    public Task<Guid> Draft(string actorId,DraftInput input,CancellationToken ct=default)=>gate.Run(async()=>
    {
        var actor=await Actor(actorId,ct);DocumentPermission(actor,input.Kind);
        return await Once(actor,input.Key,"draft",input,async()=>
        {
            if(!await db.Settings.AnyAsync(x=>x.CurrencyCode.Length==3,ct))throw new BusinessException("An administrator must configure the ledger currency in Business settings before creating transactions.",409);
            if(input.Items is null || input.Items.Count<1 || input.Items.Count>100 || input.Items.Select(x=>x.ProductId).Distinct().Count()!=input.Items.Count) throw new BusinessException("Provide 1–100 distinct product lines.");
            var doc=new Document{Kind=input.Kind,ActorId=actorId,PartnerId=input.PartnerId,Discount=input.Discount,TaxRate=input.TaxRate,Reference=Text(input.Reference,"Reference",120)};
            doc.Number=(input.Kind==DocumentKind.Purchase?"PO-":"INV-")+DateTime.UtcNow.ToString("yyyyMMdd")+"-"+doc.Id.ToString("N");
            if(input.PartnerId.HasValue)
            {var expected=input.Kind==DocumentKind.Purchase?"Supplier":"Customer";var partner=await db.Partners.SingleOrDefaultAsync(x=>x.Id==input.PartnerId && x.Active && x.Kind==expected,ct)??throw new BusinessException("Select an active "+expected.ToLowerInvariant()+".");doc.PartnerName=partner.Name;}
            else if(input.Kind==DocumentKind.Purchase)throw new BusinessException("A supplier is required for purchases.");
            var ids=input.Items.Select(x=>x.ProductId).ToArray();var products=await db.Products.Where(x=>ids.Contains(x.Id)&&x.Active).ToDictionaryAsync(x=>x.Id,ct);
            var categories=await db.Categories.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.Name,ct);
            foreach(var line in input.Items)
            {
                Money.Quantity(line.Quantity);if(!products.TryGetValue(line.ProductId,out var p))throw new BusinessException("An active product was not found.");
                var price=p.SellingPrice;if(input.Kind==DocumentKind.Purchase){Money.Amount(line.UnitCost,"Unit cost");price=line.UnitCost;}
                doc.Items.Add(new DocumentItem{ProductId=p.Id,ProductName=p.Name,Sku=p.Sku,CategoryId=p.CategoryId,CategoryName=categories[p.CategoryId],Quantity=line.Quantity,UnitPrice=price,UnitCost=input.Kind==DocumentKind.Purchase?price:0,LineTotal=Money.Round(line.Quantity*price)});
            }
            var t=Money.Totals(doc.Items.Select(x=>x.LineTotal),doc.Discount,doc.TaxRate);doc.Subtotal=t.Subtotal;doc.Tax=t.Tax;doc.Total=t.Total;
            db.Documents.Add(doc);Audit(actorId,"DraftCreated",doc.Kind.ToString(),doc.Id);return doc.Id;
        },ct);
    },ct);
    public async Task<Page<DocumentSummary>> Documents(string actorId,DocumentKind kind,string? search=null,Guid? partnerId=null,DocumentState? state=null,DateTime? from=null,DateTime? to=null,int page=1,int size=20,CancellationToken ct=default)
    {
        var a=await Actor(actorId,ct);DocumentPermission(a,kind);var pg=Paging(page,size);var q=db.Documents.AsNoTracking().Where(x=>x.Kind==kind);
        if(kind==DocumentKind.Sale&&!a.Supervises)q=q.Where(x=>x.ActorId==actorId);
        if(!string.IsNullOrWhiteSpace(search))q=q.Where(x=>x.Number.Contains(search)||x.PartnerName.Contains(search)||x.Reference.Contains(search));
        if(partnerId.HasValue)q=q.Where(x=>x.PartnerId==partnerId);if(state.HasValue)q=q.Where(x=>x.State==state);
        if(from.HasValue)q=q.Where(x=>x.CreatedUtc>=from.Value);if(to.HasValue)q=q.Where(x=>x.CreatedUtc<to.Value.Date.AddDays(1));
        var total=await q.CountAsync(ct);return new Page<DocumentSummary>(await q.OrderByDescending(x=>x.CreatedUtc).ThenBy(x=>x.Id).Skip((pg.Page-1)*pg.Size).Take(pg.Size).Select(x=>new DocumentSummary(x.Id,x.Number,x.Kind,x.State,x.PartnerName,x.CreatedUtc,x.Total)).ToListAsync(ct),total,pg.Page,pg.Size);
    }
    public Task<DocumentDto> Document(string actorId,Guid id,CancellationToken ct=default)=>gate.Run(async()=>
    {
        var a=await Actor(actorId,ct);var d=await db.Documents.AsNoTracking().Include(x=>x.Items).SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new BusinessException("Document not found.",404);Ownership(a,d);
        var payments=await db.Payments.AsNoTracking().Where(x=>x.DocumentId==id).OrderBy(x=>x.CreatedUtc).Select(x=>new PaymentDto(x.Id,x.Amount,x.Refund,x.Method,x.Reason,x.CreatedUtc)).ToListAsync(ct);
        var paid=payments.Where(x=>!x.Refund).Sum(x=>x.Amount);var refund=payments.Where(x=>x.Refund).Sum(x=>x.Amount);var outstanding=d.State==DocumentState.Completed?Math.Max(0,d.Total-paid):0;
        return new DocumentDto(d.Id,d.Kind,d.State,d.Number,d.PartnerId,d.PartnerName,d.Reference,d.ActorId,d.CreatedUtc,d.CompletedUtc,d.Subtotal,d.Discount,d.TaxRate,d.Tax,d.Total,paid,refund,outstanding,Money.PaymentState(d.Total,paid,refund,d.State==DocumentState.Reversed),d.Items.OrderBy(x=>x.Sku).Select(x=>new DocumentLineDto(x.ProductId,x.ProductName,x.Sku,x.Quantity,x.UnitPrice,a.Supervises||d.Kind==DocumentKind.Purchase?x.UnitCost:null,x.LineTotal)).ToArray(),payments);
    },ct);
    public Task<Guid> Cancel(string actorId,Guid id,Guid key,CancellationToken ct=default)=>gate.Run(async()=>
    {
        var a=await Actor(actorId,ct);var d=await db.Documents.SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new BusinessException("Document not found.",404);Ownership(a,d);
        return await Once(a,key,"cancel",new{id},()=>
        {if(d.State!=DocumentState.Draft)throw new BusinessException("Only unprocessed drafts can be cancelled. Completed sales require a documented return/reversal.",409);d.State=DocumentState.Cancelled;Audit(actorId,"DraftCancelled",d.Kind.ToString(),id);return Task.FromResult(id);},ct);
    },ct);
    public Task<Guid> Receive(string actorId,Guid id,Guid key,CancellationToken ct=default)=>gate.Run(async()=>
    {
        var a=await Actor(actorId,ct);a.Demand(Roles.Manager,Roles.Inventory);
        return await Once(a,key,"receive",new{id},async()=>
        {
            var d=await db.Documents.Include(x=>x.Items).SingleOrDefaultAsync(x=>x.Id==id&&x.Kind==DocumentKind.Purchase,ct)??throw new BusinessException("Purchase not found.",404);
            if(d.State!=DocumentState.Draft)throw new BusinessException("Only a draft purchase can be received. This purchase was already processed.",409);
            var ids=d.Items.Select(x=>x.ProductId).ToArray();var products=await db.Products.Where(x=>ids.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,ct);
            foreach(var line in d.Items.OrderBy(x=>x.ProductId))
            {
                var p=products[line.ProductId];if(!p.Active)throw new BusinessException("An archived product prevents receiving.",409);
                line.UnitCost=decimal.Round(line.UnitPrice*(d.Subtotal==0?1:1-d.Discount/d.Subtotal),6,MidpointRounding.AwayFromZero);
                p.AverageCost=Money.Average(p.Stock,p.AverageCost,line.Quantity,line.UnitCost);Move(p,line.Quantity,actorId,"PurchaseReceived",d.Number,id);
            }
            d.State=DocumentState.Completed;d.CompletedUtc=DateTime.UtcNow;Audit(actorId,"PurchaseReceived","Purchase",id);return id;
        },ct);
    },ct);
}
