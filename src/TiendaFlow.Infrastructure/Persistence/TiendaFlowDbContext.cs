using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public class TiendaFlowDbContext : DbContext
{
    public TiendaFlowDbContext(DbContextOptions<TiendaFlowDbContext> options)
        : base(options) { }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<StockItem> Stocks => Set<StockItem>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<Store> Stores => Set<Store>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<AppUser> AppUsers => Set<AppUser>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();

    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();

    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<SystemParameter> SystemParameters => Set<SystemParameter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TiendaFlowDbContext).Assembly);
    }
}
