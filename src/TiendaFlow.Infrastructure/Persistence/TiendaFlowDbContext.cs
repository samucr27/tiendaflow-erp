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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TiendaFlowDbContext).Assembly);
    }
}
