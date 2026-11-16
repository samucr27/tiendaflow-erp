using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

public class InventoryQueryTests : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime MovementDate = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    private readonly SqlServerDatabaseFixture _database;

    public InventoryQueryTests(SqlServerDatabaseFixture database) => _database = database;

    private static string NewSku() => $"SKU-{Guid.NewGuid():N}";

    private static string NewBarcode() => $"770{Guid.NewGuid():N}";

    private static int NewStoreId() => Random.Shared.Next(1000, 9000);

    [SqlServerFact]
    public void ProductCatalogQuery_SearchesByNameSkuAndBarcode()
    {
        var sku = NewSku();
        var barcode = NewBarcode();
        var name = $"Cafe molido {Guid.NewGuid():N}";

        using (var context = _database.CreateContext())
            new EfProductRepository(context).Add(new Product(sku, barcode, name, 12500m));

        using (var context = _database.CreateContext())
        {
            var query = new EfProductCatalogQuery(context);

            Assert.Contains(query.Search(name), product => product.Sku == sku);
            Assert.Contains(query.Search(sku), product => product.Sku == sku);
            Assert.Contains(query.Search(barcode), product => product.Sku == sku);

            var bySku = query.GetBySku(sku);
            Assert.NotNull(bySku);
            Assert.Equal(name, bySku.Name);
            Assert.Equal(12500m, bySku.SalePrice);

            Assert.True(query.Count() >= 1);
        }
    }

    [SqlServerFact]
    public void StockQuery_JoinsTheCatalogAndFlagsLowStock()
    {
        var sku = NewSku();
        var storeId = NewStoreId();
        var otherStoreId = storeId + 1;

        using (var context = _database.CreateContext())
            new EfProductRepository(context).Add(new Product(sku, NewBarcode(), "Leche entera 1 L", 4200m));

        using (var context = _database.CreateContext())
        {
            var repository = new EfStockRepository(context);
            repository.Add(new StockItem(sku, storeId, 4, "Nevera 2", minStock: 8));
            repository.Add(new StockItem(sku, otherStoreId, 20, "Bodega", minStock: 5));
        }

        using (var context = _database.CreateContext())
        {
            var query = new EfStockQuery(context);

            var row = Assert.Single(query.ListByStore(storeId));
            Assert.Equal("Leche entera 1 L", row.ProductName);
            Assert.Equal(4, row.QuantityAvailable);
            Assert.Equal(8, row.MinStock);
            Assert.Equal(4200m, row.SalePrice);
            Assert.True(row.IsBelowMinimum);

            Assert.Equal(2, query.ListBySku(sku).Count);

            var other = Assert.Single(query.ListByStore(otherStoreId));
            Assert.False(other.IsBelowMinimum);
        }
    }

    [SqlServerFact]
    public void StockQuery_KeepsStockRowsWithoutCatalogProduct()
    {
        var sku = NewSku();
        var storeId = NewStoreId();

        using (var context = _database.CreateContext())
            new EfStockRepository(context).Add(new StockItem(sku, storeId, 7, "Bodega"));

        using (var context = _database.CreateContext())
        {
            var row = Assert.Single(new EfStockQuery(context).ListByStore(storeId));

            Assert.Equal(sku, row.ProductName);
            Assert.Equal(0m, row.SalePrice);
        }
    }

    [SqlServerFact]
    public void InventoryDashboardService_ComputesTotalsForTheStore()
    {
        var sku = NewSku();
        var lowSku = NewSku();
        var storeId = NewStoreId();

        using (var context = _database.CreateContext())
        {
            new EfProductRepository(context).Add(new Product(sku, NewBarcode(), "Arroz 500 g", 3300m));
            new EfProductRepository(context).Add(new Product(lowSku, NewBarcode(), "Pan tajado", 5500m));
        }

        using (var context = _database.CreateContext())
        {
            var repository = new EfStockRepository(context);
            repository.Add(new StockItem(sku, storeId, 10, "Pasillo 1", minStock: 3));
            repository.Add(new StockItem(lowSku, storeId, 2, "Pasillo 2", minStock: 6));
        }

        using (var context = _database.CreateContext())
        {
            var service = new InventoryDashboardService(
                new EfProductCatalogQuery(context),
                new EfStockQuery(context),
                new AllowAllAccessControl());

            var dashboard = service.Get(storeId);

            Assert.True(dashboard.ProductCount >= 2);
            Assert.Equal(12, dashboard.UnitsInStock);
            Assert.Equal(10 * 3300m + 2 * 5500m, dashboard.StockValue);
            Assert.Equal(2, dashboard.Rows.Count);
            var lowStock = Assert.Single(dashboard.LowStock);
            Assert.Equal(lowSku, lowStock.Sku);
        }
    }

    [SqlServerFact]
    public void StockQuery_ReturnsTheLastMovement()
    {
        var sku = NewSku();
        var storeId = NewStoreId();

        using (var context = _database.CreateContext())
            new EfProductRepository(context).Add(new Product(sku, NewBarcode(), "Gaseosa 400 ml", 3500m));

        using (var context = _database.CreateContext())
        {
            var repository = new EfStockRepository(context);
            var item = new StockItem(sku, storeId, 10, "Pasillo 1");
            item.RegisterSale(3, MovementDate);
            repository.Add(item);
        }

        using (var context = _database.CreateContext())
        {
            var row = Assert.Single(new EfStockQuery(context).ListByStore(storeId));

            Assert.Equal(new InventoryMovement(MovementType.Sale, 3, MovementDate), row.LastMovement);
        }
    }
}
