using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TiendaFlow.Infrastructure.Persistence;

public class TiendaFlowDbContextFactory : IDesignTimeDbContextFactory<TiendaFlowDbContext>
{
    public TiendaFlowDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TiendaFlowDbContext>();
        
        // Cadena de conexión por defecto para migraciones en desarrollo
        optionsBuilder.UseSqlServer("Server=localhost,1433;Database=TiendaFlow;User Id=sa;Password=TiendaFlow_Dev#2026;TrustServerCertificate=True;");

        return new TiendaFlowDbContext(optionsBuilder.Options);
    }
}