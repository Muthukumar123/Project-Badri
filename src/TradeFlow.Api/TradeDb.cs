using Microsoft.EntityFrameworkCore;
namespace Badri.TradeFlow;
public class TradeDb(DbContextOptions<TradeDb> options) : DbContext(options)
{
    public DbSet<Retailer> Retailers => Set<Retailer>();
    public DbSet<RetailerBill> Bills => Set<RetailerBill>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CashHandover> CashHandovers => Set<CashHandover>();
    public DbSet<EarlierCollection> EarlierCollections => Set<EarlierCollection>();
    public DbSet<DocumentDraft> DocumentDrafts => Set<DocumentDraft>();
    public DbSet<AuditEvent> Audits => Set<AuditEvent>();
    public DbSet<TraderMembership> TraderMemberships => Set<TraderMembership>();
    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Retailer>().Property(x=>x.UnspecifiedOpeningDue).HasPrecision(18,2);
        m.Entity<RetailerBill>().Property(x=>x.Amount).HasPrecision(18,2);
        m.Entity<RetailerBill>().HasIndex(x=>new{x.TenantId,x.BillNo}).IsUnique();
        m.Entity<Collection>().Property(x=>x.Amount).HasPrecision(18,2);
        m.Entity<Collection>().Property(x=>x.Mode).HasConversion<string>();
        m.Entity<Collection>().Property(x=>x.State).HasConversion<string>();
        m.Entity<Collection>().HasIndex(x=>new{x.TenantId,x.IdempotencyKey}).IsUnique();
        m.Entity<Collection>().HasIndex(x=>new{x.TenantId,x.RetailerId});
        m.Entity<CashHandover>().Property(x=>x.Expected).HasPrecision(18,2);
        m.Entity<CashHandover>().Property(x=>x.Counted).HasPrecision(18,2);
        m.Entity<CashHandover>().Property(x=>x.Difference).HasPrecision(18,2);
        m.Entity<CashHandover>().HasIndex(x=>new{x.TenantId,x.SalesmanId,x.Date}).IsUnique();
        m.Entity<EarlierCollection>().Property(x=>x.Amount).HasPrecision(18,2);
        m.Entity<TraderMembership>().Property(x=>x.Role).HasConversion<string>();
        m.Entity<TraderMembership>().HasIndex(x=>x.EntraObjectId).IsUnique();
        m.Entity<TraderMembership>().HasIndex(x=>new{x.TenantId,x.Active});
        m.Entity<DocumentDraft>().Property(x=>x.Type).HasConversion<string>();
        m.Entity<DocumentDraft>().Property(x=>x.State).HasConversion<string>();
    }
}
