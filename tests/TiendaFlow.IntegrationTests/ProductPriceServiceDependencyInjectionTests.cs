using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// Resolves the price service from the same registration Program.cs uses and
/// verifies the transaction still protects the price when the audit write fails.
/// </summary>
public class ProductPriceServiceDependencyInjectionTests : IClassFixture<SqlServerDatabaseFixture>
{
    private readonly SqlServerDatabaseFixture _database;

    public ProductPriceServiceDependencyInjectionTests(SqlServerDatabaseFixture database) =>
        _database = database;

    [SqlServerFact]
    public void UpdatePrice_WhenAuditFails_RollsBackThroughTheApplicationWiring()
    {
        var connectionString = _database.ConnectionString;
        Assert.NotNull(connectionString);

        var sku = $"SKU-{Guid.NewGuid():N}";
        var barcode = $"770{Guid.NewGuid():N}";

        using (var context = _database.CreateContext())
            new EfProductRepository(context).Add(new Product(sku, barcode, "Tea x20", 6000m));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTiendaFlowPersistence(connectionString!);
        services.AddScoped<ICurrentUser, TestCurrentUser>();
        services.AddScoped<IAccessControl, AllowAllAccessControl>();
        services.AddScoped<IAuditLog, ThrowingAuditLog>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ProductPriceService>();

        Assert.Throws<InvalidOperationException>(
            () => service.UpdatePrice(sku, 6500m, 7, MovementDate));

        using var contextAfterFailure = _database.CreateContext();
        Assert.Equal(6000m, new EfProductRepository(contextAfterFailure).GetBySku(sku)!.SalePrice);
    }

    [SqlServerFact]
    public void UpdatePrice_WithTheRealWiring_PersistsPriceAndAuditEntry()
    {
        var connectionString = _database.ConnectionString;
        Assert.NotNull(connectionString);

        var sku = $"SKU-{Guid.NewGuid():N}";
        var barcode = $"770{Guid.NewGuid():N}";

        using (var context = _database.CreateContext())
            new EfProductRepository(context).Add(new Product(sku, barcode, "Coffee 250 g", 8000m));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTiendaFlowPersistence(connectionString!);
        services.AddScoped<ICurrentUser, TestCurrentUser>();
        services.AddScoped<IAccessControl, AllowAllAccessControl>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ProductPriceService>();

        service.UpdatePrice(sku, 8500m, 9, MovementDate);

        using var contextAfterUpdate = _database.CreateContext();
        Assert.Equal(8500m, new EfProductRepository(contextAfterUpdate).GetBySku(sku)!.SalePrice);

        var entry = contextAfterUpdate.AuditEntries.Single(candidate => candidate.EntityId == sku);
        Assert.Equal("PriceChanged", entry.Action);
        Assert.Equal(9, entry.UserId);
        Assert.Equal(DateTimeKind.Utc, entry.Date.Kind);
    }

    private static readonly DateTime MovementDate = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    private sealed class ThrowingAuditLog : IAuditLog
    {
        public void Record(AuditEntry entry) =>
            throw new InvalidOperationException("Simulated audit failure.");
    }
}
