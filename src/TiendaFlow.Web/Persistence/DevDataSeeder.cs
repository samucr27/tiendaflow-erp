using Microsoft.EntityFrameworkCore;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.Web.Persistence;

/// <summary>
/// Seeds a handful of catalog and stock rows for local development.
/// Registered only in the Development environment (see Program.cs).
/// </summary>
public sealed class DevDataSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DevDataSeeder(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TiendaFlowDbContext>();

        await context.Database.MigrateAsync(cancellationToken);

        if (await context.Products.AnyAsync(cancellationToken))
            return;

        context.Products.AddRange(
            new Product("SKU-1001", "7701234567001", "Agua 600 ml", 2500m),
            new Product("SKU-1002", "7701234567002", "Gaseosa 400 ml", 3500m),
            new Product("SKU-1003", "7701234567003", "Arroz 500 g", 3300m),
            new Product("SKU-1004", "7701234567004", "Pan tajado", 5500m));

        context.Stocks.AddRange(
            new StockItem("SKU-1001", 1, 40, "Pasillo 1", minStock: 10),
            new StockItem("SKU-1002", 1, 25, "Pasillo 1", minStock: 8),
            new StockItem("SKU-1003", 1, 18, "Pasillo 2", minStock: 6),
            new StockItem("SKU-1004", 1, 12, "Pasillo 3", minStock: 5));

        await context.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
