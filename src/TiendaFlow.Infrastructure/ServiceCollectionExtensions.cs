using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TiendaFlow.Application;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.Infrastructure;

/// <summary>
/// Registers the EF Core implementations and the Application services.
/// Program.cs and the integration tests share this method so the container
/// wiring is verified instead of duplicated.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTiendaFlowPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<TiendaFlowDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddScoped<IStockRepository, EfStockRepository>();
        services.AddScoped<IAuditLog, EfAuditLog>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<IProductCatalogQuery, EfProductCatalogQuery>();
        services.AddScoped<IStockQuery, EfStockQuery>();

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<InventoryDashboardService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<StockService>();
        services.AddScoped<ProductPriceService>();

        return services;
    }
}
