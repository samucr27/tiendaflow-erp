using TiendaFlow.Application;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// Blazor Server keeps one DbContext per circuit, so two cashiers on two
/// browser tabs hold two long-lived contexts at the same time. Stock changes
/// made by one of them must not be overwritten by the other.
/// </summary>
public class LongLivedContextStockTests : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime SaleDate = new(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);

    private readonly SqlServerDatabaseFixture _database;

    public LongLivedContextStockTests(SqlServerDatabaseFixture database) => _database = database;

    [SqlServerFact]
    public void SalesFromTwoCircuits_DiscountEveryUnit()
    {
        var sku = $"SKU-{Guid.NewGuid():N}";
        var storeId = Random.Shared.Next(1000, 9000);

        using (var setup = _database.CreateContext())
        {
            new EfProductRepository(setup).Add(new Product(sku, $"770{Guid.NewGuid():N}", "Gaseosa 400 ml", 3000m));
            new EfStockRepository(setup).Add(new StockItem(sku, storeId, 10, "Nevera 1", minStock: 2));
        }

        using var cashierA = _database.CreateContext();
        using var cashierB = _database.CreateContext();
        var serviceA = new StockService(new EfStockRepository(cashierA), new AllowAllAccessControl());
        var serviceB = new StockService(new EfStockRepository(cashierB), new AllowAllAccessControl());

        serviceA.RegisterSale(storeId, sku, 1, SaleDate); // 10 -> 9
        serviceB.RegisterSale(storeId, sku, 1, SaleDate); //  9 -> 8
        serviceA.RegisterSale(storeId, sku, 1, SaleDate); //  8 -> 7

        using var check = _database.CreateContext();
        Assert.Equal(7, new EfStockRepository(check).Get(storeId, sku)!.QuantityAvailable);
    }

    [SqlServerFact]
    public void SimultaneousSales_RejectTheSecondWriteInsteadOfLosingAUnit()
    {
        var sku = $"SKU-{Guid.NewGuid():N}";
        var storeId = Random.Shared.Next(1000, 9000);

        using (var setup = _database.CreateContext())
        {
            new EfProductRepository(setup).Add(new Product(sku, $"770{Guid.NewGuid():N}", "Agua 600 ml", 2000m));
            new EfStockRepository(setup).Add(new StockItem(sku, storeId, 10, "Nevera 1", minStock: 2));
        }

        using var cashierA = _database.CreateContext();
        using var cashierB = _database.CreateContext();
        var repositoryA = new EfStockRepository(cashierA);
        var repositoryB = new EfStockRepository(cashierB);

        // Both cashiers read the row before either one saves.
        var itemA = repositoryA.Get(storeId, sku)!;
        var itemB = repositoryB.Get(storeId, sku)!;

        itemA.RegisterSale(1, SaleDate);
        repositoryA.Update(itemA);

        itemB.RegisterSale(1, SaleDate);
        Assert.Throws<Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException>(() => repositoryB.Update(itemB));

        // The rejected circuit recovers: its next sale reads the current row.
        new StockService(repositoryB, new AllowAllAccessControl()).RegisterSale(storeId, sku, 1, SaleDate);

        using var check = _database.CreateContext();
        Assert.Equal(8, new EfStockRepository(check).Get(storeId, sku)!.QuantityAvailable);
    }

    [SqlServerFact]
    public void ARejectedSale_DoesNotPoisonTheNextOperationOfTheCircuit()
    {
        var sku = $"SKU-{Guid.NewGuid():N}";
        var storeId = Random.Shared.Next(1000, 9000);

        using (var setup = _database.CreateContext())
        {
            new EfProductRepository(setup).Add(new Product(sku, $"770{Guid.NewGuid():N}", "Pan tajado", 5200m));
            new EfStockRepository(setup).Add(new StockItem(sku, storeId, 3, "Gondola 4", minStock: 1));
        }

        using var circuit = _database.CreateContext();
        var service = new StockService(new EfStockRepository(circuit), new AllowAllAccessControl());

        Assert.ThrowsAny<Exception>(() => service.RegisterSale(storeId, sku, 50, SaleDate));
        service.RegisterSale(storeId, sku, 1, SaleDate);

        using var check = _database.CreateContext();
        Assert.Equal(2, new EfStockRepository(check).Get(storeId, sku)!.QuantityAvailable);
    }
}
