using Microsoft.EntityFrameworkCore;
using StockPilot.Core;
namespace StockPilot.Infrastructure;
public sealed partial class BusinessService
{
    public Task<Guid> Confirm(string actorId,Guid id,Guid key,CancellationToken ct=default)=>gate.Run(async()=>
    {
        var a=await Actor(actorId,ct);a.Demand(Roles.Manager,Roles.Sales);
        var d=await db.Documents.Include(x=>x.Items).SingleOrDefaultAsync(x=>x.Id==id&&x.Kind==DocumentKind.Sale,ct)??throw new BusinessException("Sale not found.",404);Ownership(a,d);
        return await Once(a,key,"confirm",new{id},async()=>
        {
            if(d.State!=DocumentState.Draft)throw new BusinessException("Only a draft sale can be confirmed. This sale was already processed.",409);
            var ids=d.Items.Select(x=>x.ProductId).ToArray();var products=await db.Products.Where(x=>ids.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,ct);
            foreach(var line in d.Items.OrderBy(x=>x.ProductId))
            {
                var p=products[line.ProductId];if(!p.Active)throw new BusinessException("Sale contains an archived product.",409);
                Money.Quantity(line.Quantity);var price=p.SellingPrice;Money.Amount(price,"Selling price");
                if(line.UnitPrice!=price)throw new BusinessException("A selling price changed. Cancel this draft and create a new one to review current prices.",409);
                line.LineTotal=Money.Round(line.Quantity*price);line.UnitCost=p.AverageCost;
                Move(p,-line.Quantity,actorId,"Sale",d.Number,id);
            }
            var totals=Money.Totals(d.Items.Select(x=>x.LineTotal),d.Discount,d.TaxRate);d.Subtotal=totals.Subtotal;d.Tax=totals.Tax;d.Total=totals.Total;
            d.State=DocumentState.Completed;d.CompletedUtc=DateTime.UtcNow;Audit(actorId,"SaleConfirmed","Sale",id);return id;
        },ct);
    },ct);
    public Task<Guid> RecordPayment(string actorId,Guid id,PaymentInput input,bool refund=false,CancellationToken ct=default)=>gate.Run(async()=>
    {
        var a=await Actor(actorId,ct);a.Demand(Roles.Manager,Roles.Sales);if(refund)a.Demand(Roles.Manager);
        var d=await db.Documents.SingleOrDefaultAsync(x=>x.Id==id&&x.Kind==DocumentKind.Sale,ct)??throw new BusinessException("Sale not found.",404);Ownership(a,d);
        return await Once(a,input.Key,refund?"refund":"payment",new{id,input},async()=>
        {
            Money.Amount(input.Amount,refund?"Refund":"Payment",true);if(!Enum.IsDefined(input.Method))throw new BusinessException("Unknown payment method.");
            if(d.State!=DocumentState.Completed&&!(refund&&d.State==DocumentState.Reversed))throw new BusinessException("Sale state does not permit this settlement.",409);
            var rows=await db.Payments.Where(x=>x.DocumentId==id).ToListAsync(ct);var paid=rows.Where(x=>!x.Refund).Sum(x=>x.Amount);var refunded=rows.Where(x=>x.Refund).Sum(x=>x.Amount);
            if(refund&&input.Amount>paid-refunded)throw new BusinessException("Refund exceeds collected funds remaining.",409);
            if(!refund&&input.Amount>d.Total-paid)throw new BusinessException("Payment exceeds the outstanding invoice balance.",409);
            var reason=Text(input.Reason,"Reason",300,refund);
            var payment=new Payment{DocumentId=id,Amount=input.Amount,Refund=refund,Method=input.Method,Reason=reason,ActorId=actorId};db.Payments.Add(payment);Audit(actorId,refund?"RefundRecorded":"PaymentRecorded","Sale",id,reason.Length>0?reason:"Payment recorded");return payment.Id;
        },ct);
    },ct);
    public Task<Guid> Reverse(string actorId,Guid id,ReverseInput input,CancellationToken ct=default)=>gate.Run(async()=>
    {
        var a=await Actor(actorId,ct);a.Demand(Roles.Manager);
        return await Once(a,input.Key,"reverse",new{id,input},async()=>
        {
            var d=await db.Documents.Include(x=>x.Items).SingleOrDefaultAsync(x=>x.Id==id&&x.Kind==DocumentKind.Sale,ct)??throw new BusinessException("Sale not found.",404);
            if(d.State!=DocumentState.Completed)throw new BusinessException("Only a completed sale can be reversed, once.",409);var reason=Text(input.Reason,"Return reason",300,true);
            var ids=d.Items.Select(x=>x.ProductId).ToArray();var products=await db.Products.Where(x=>ids.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,ct);
            foreach(var line in d.Items.OrderBy(x=>x.ProductId))
            {
                var p=products[line.ProductId];p.AverageCost=Money.Average(p.Stock,p.AverageCost,line.Quantity,line.UnitCost);
                if(!p.Active){p.Active=true;Audit(actorId,"ProductReactivatedByReturn","Product",p.Id);}
                Move(p,line.Quantity,actorId,"SaleReversal",reason,id);
            }
            d.State=DocumentState.Reversed;Audit(actorId,"SaleReversedFullPhysicalReturn","Sale",id,reason);return id;
        },ct);
    },ct);
}
