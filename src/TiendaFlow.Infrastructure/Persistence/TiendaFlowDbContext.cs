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

    public DbSet<ProductSupplier> ProductSuppliers => Set<ProductSupplier>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();

    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();

    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();

    public DbSet<SalesReturnLine> SalesReturnLines => Set<SalesReturnLine>();

    public DbSet<PosSale> PosSales => Set<PosSale>();

    public DbSet<PosSaleLine> PosSaleLines => Set<PosSaleLine>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<SystemParameter> SystemParameters => Set<SystemParameter>();

    public DbSet<InventoryCount> InventoryCounts => Set<InventoryCount>();

    public DbSet<InventoryCountLine> InventoryCountLines => Set<InventoryCountLine>();

    public DbSet<InventoryTransfer> InventoryTransfers => Set<InventoryTransfer>();

    public DbSet<InventoryTransferLine> InventoryTransferLines => Set<InventoryTransferLine>();

    public DbSet<EmploymentContract> EmployeeContracts => Set<EmploymentContract>();

    public DbSet<ContractAttachment> ContractAttachments => Set<ContractAttachment>();

    /// <summary>
    /// A failed save leaves its entities as Modified or Added in a context
    /// that lives as long as the browser tab. Outside an explicit transaction
    /// the failed changes are discarded so the next operation of the same user
    /// does not write them again. Inside a transaction, EfUnitOfWork discards
    /// them after the rollback.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        try
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        catch
        {
            if (Database.CurrentTransaction is null)
                ChangeTracker.Clear();

            throw;
        }
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch
        {
            if (Database.CurrentTransaction is null)
                ChangeTracker.Clear();

            throw;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TiendaFlowDbContext).Assembly);
    }
}
