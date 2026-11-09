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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TiendaFlowDbContext).Assembly);
    }
}
