using Microsoft.EntityFrameworkCore;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public class TiendaFlowDbContext : DbContext
{
    public TiendaFlowDbContext(DbContextOptions<TiendaFlowDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TiendaFlowDbContext).Assembly);
    }
}