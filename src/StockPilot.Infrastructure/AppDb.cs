using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StockPilot.Core;
namespace StockPilot.Infrastructure;
public sealed class ApplicationUser : IdentityUser { public bool Active { get; set; } = true; public string DisplayName { get; set; } = ""; }
public sealed class AppDb(DbContextOptions<AppDb> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentItem> DocumentItems => Set<DocumentItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<StockMovement> Movements => Set<StockMovement>();
    public DbSet<ActivityLog> Activities => Set<ActivityLog>();
    public DbSet<Operation> Operations => Set<Operation>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<ApplicationUser>().Property(x => x.DisplayName).HasMaxLength(160);
        b.Entity<ApplicationUser>().Property(x => x.Id).HasMaxLength(128);
        b.Entity<IdentityRole>().Property(x => x.Id).HasMaxLength(128);
        b.Entity<IdentityUserLogin<string>>(e => { e.Property(x => x.LoginProvider).HasMaxLength(128); e.Property(x => x.ProviderKey).HasMaxLength(128); });
        b.Entity<IdentityUserToken<string>>(e => { e.Property(x => x.LoginProvider).HasMaxLength(128); e.Property(x => x.Name).HasMaxLength(128); });
        foreach (var e in b.Model.GetEntityTypes()) foreach (var p in e.GetProperties())
        {
            if (p.ClrType == typeof(decimal)) { p.SetPrecision(19); p.SetScale(6); }
            if (p.ClrType == typeof(string) && p.GetMaxLength() == null) p.SetMaxLength(1000);
        }
        b.Entity<Product>(e => {
            e.Property(x => x.Sku).HasMaxLength(64); e.Property(x => x.Name).HasMaxLength(160); e.Property(x => x.Barcode).HasMaxLength(64);
            e.HasIndex(x => x.Sku).IsUnique(); e.HasIndex(x => new { x.CategoryId, x.Active });
            e.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.ToTable(t => t.HasCheckConstraint("CK_Product_Nonnegative", "[Stock] >= 0 AND [AverageCost] >= 0 AND [SellingPrice] >= 0 AND [MinimumStock] >= 0"));
        });
        b.Entity<Category>().Property(x => x.Name).HasMaxLength(100); b.Entity<Category>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Unit>().Property(x => x.Name).HasMaxLength(100); b.Entity<Unit>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Partner>().Property(x => x.Kind).HasMaxLength(20);
        b.Entity<Partner>().HasIndex(x => new { x.Kind, x.Name });
        b.Entity<Partner>().ToTable(t => t.HasCheckConstraint("CK_Partner_Kind", "[Kind] IN ('Supplier','Customer')"));
        b.Entity<Document>(e => {
            e.Property(x => x.Number).HasMaxLength(50); e.Property(x => x.Reference).HasMaxLength(120); e.Property(x => x.ActorId).HasMaxLength(128);
            e.HasIndex(x => x.Number).IsUnique(); e.HasIndex(x => new { x.Kind, x.State, x.CreatedUtc }); e.HasIndex(x => x.PartnerId);
            e.HasOne<Partner>().WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_Document_Totals", "[Subtotal] >= 0 AND [Discount] >= 0 AND [Discount] <= [Subtotal] AND [TaxRate] >= 0 AND [TaxRate] <= 100 AND [Tax] >= 0 AND [Total] = [Subtotal] - [Discount] + [Tax]"));
        });
        b.Entity<DocumentItem>(e => {
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.DocumentId, x.ProductId }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("CK_Item_Quantity", "[Quantity] > 0 AND [UnitPrice] >= 0 AND [UnitCost] >= 0 AND [LineTotal] >= 0"));
        });
        b.Entity<Payment>(e => { e.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict); e.ToTable(t => t.HasCheckConstraint("CK_Payment_Amount", "[Amount] > 0")); });
        b.Entity<StockMovement>(e => {
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ProductId, x.CreatedUtc });
            e.ToTable(t => t.HasCheckConstraint("CK_Movement_Balance", "[Before] >= 0 AND [After] >= 0 AND [After] = [Before] + [Change] AND [Change] <> 0"));
        });
        b.Entity<Operation>(e => { e.Property(x => x.ActorId).HasMaxLength(128); e.Property(x => x.Fingerprint).HasMaxLength(64); e.HasIndex(x => new { x.ActorId, x.Key }).IsUnique(); });
        b.Entity<ActivityLog>().HasIndex(x => x.CreatedUtc);
    }
}
