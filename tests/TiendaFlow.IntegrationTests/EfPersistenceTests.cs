using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

public class EfPersistenceTests : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime MovementDate = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    private readonly SqlServerDatabaseFixture _database;

    public EfPersistenceTests(SqlServerDatabaseFixture database) => _database = database;

    private static string NewSku() => $"SKU-{Guid.NewGuid():N}";

    private static string NewBarcode() => $"770{Guid.NewGuid():N}";

    [SqlServerFact]
    public void Add_ThenGetBySku_RoundTripsProduct()
    {
        var sku = NewSku();
        var barcode = NewBarcode();
        const decimal price = 3500.50m;

        using (var context = _database.CreateContext())
            new EfProductRepository(context).Add(new Product(sku, barcode, "Soda 400 ml", price));

        using (var context = _database.CreateContext())
        {
            var product = new EfProductRepository(context).GetBySku(sku);

            Assert.NotNull(product);
            Assert.Equal(sku, product.Sku);
            Assert.Equal(barcode, product.Barcode);
            Assert.Equal("Soda 400 ml", product.Name);
            Assert.Equal(price, product.SalePrice);
        }
    }

    [SqlServerFact]
    public void ExistsBySkuAndBarcode_AfterAdd_ReturnTrue()
    {
        var sku = NewSku();
        var barcode = NewBarcode();

        using (var context = _database.CreateContext())
            new EfProductRepository(context).Add(new Product(sku, barcode, "Water 600 ml", 2500m));

        using (var context = _database.CreateContext())
        {
            var repository = new EfProductRepository(context);

            Assert.True(repository.ExistsBySku(sku));
            Assert.True(repository.ExistsByBarcode(barcode));
            Assert.False(repository.ExistsBySku(NewSku()));
            Assert.False(repository.ExistsByBarcode(NewBarcode()));
        }
    }

    [SqlServerFact]
    public void Add_DuplicateSku_IsRejectedByPrimaryKey()
    {
        var sku = NewSku();

        using (var context = _database.CreateContext())
            new EfProductRepository(context).Add(new Product(sku, NewBarcode(), "Bread", 3000m));

        using (var context = _database.CreateContext())
        {
            var repository = new EfProductRepository(context);

            Assert.Throws<DbUpdateException>(() =>
                repository.Add(new Product(sku, NewBarcode(), "Other bread", 3200m)));
        }
    }

    [SqlServerFact]
    public void Add_DuplicateBarcode_IsRejectedByUniqueIndex()
    {
        var barcode = NewBarcode();

        using (var context = _database.CreateContext())
            new EfProductRepository(context).Add(new Product(NewSku(), barcode, "Milk 1 L", 4200m));

        using (var context = _database.CreateContext())
        {
            var repository = new EfProductRepository(context);

            Assert.Throws<DbUpdateException>(() =>
                repository.Add(new Product(NewSku(), barcode, "Other milk", 4400m)));
        }
    }

    [SqlServerFact]
    public void UpdatePrice_PersistsTheNewSalePrice()
    {
        var sku = NewSku();

        using (var context = _database.CreateContext())
            new EfProductRepository(context).Add(new Product(sku, NewBarcode(), "Coffee 250 g", 8000m));

        Product product;
        using (var context = _database.CreateContext())
            product = new EfProductRepository(context).GetBySku(sku)!;

        product.ChangePrice(8500m);

        using (var context = _database.CreateContext())
            new EfProductRepository(context).Update(product);

        using (var context = _database.CreateContext())
            Assert.Equal(8500m, new EfProductRepository(context).GetBySku(sku)!.SalePrice);
    }

    [SqlServerFact]
    public void StockSaleAndReceipt_ArePersistedPerCompositeKey()
    {
        var sku = NewSku();

        using (var context = _database.CreateContext())
        {
            var repository = new EfStockRepository(context);
            repository.Add(new StockItem(sku, 1, 10, "Aisle 3", minStock: 2));
            repository.Add(new StockItem(sku, 2, 20, "Aisle 4", minStock: 5));
        }

        using (var context = _database.CreateContext())
        {
            var repository = new EfStockRepository(context);
            var item = repository.Get(1, sku)!;
            item.RegisterSale(3, MovementDate);
            repository.Update(item);
        }

        using (var context = _database.CreateContext())
        {
            var repository = new EfStockRepository(context);
            var item = repository.Get(2, sku)!;
            item.RegisterReceipt(5, MovementDate);
            repository.Update(item);
        }

        using (var context = _database.CreateContext())
        {
            var repository = new EfStockRepository(context);
            var store1 = repository.Get(1, sku);
            var store2 = repository.Get(2, sku);

            Assert.NotNull(store1);
            Assert.Equal(7, store1.QuantityAvailable);
            Assert.Equal("Aisle 3", store1.Location);
            Assert.Equal(2, store1.MinStock);
            Assert.Equal(new InventoryMovement(MovementType.Sale, 3, MovementDate), store1.LastMovement);

            Assert.NotNull(store2);
            Assert.Equal(25, store2.QuantityAvailable);
            Assert.Equal("Aisle 4", store2.Location);
            Assert.Equal(5, store2.MinStock);
            Assert.Equal(new InventoryMovement(MovementType.Receipt, 5, MovementDate), store2.LastMovement);

            Assert.Null(repository.Get(3, sku));
        }
    }

    [SqlServerFact]
    public void Add_DuplicateCompositeKey_IsRejectedByPrimaryKey()
    {
        var sku = NewSku();

        using (var context = _database.CreateContext())
            new EfStockRepository(context).Add(new StockItem(sku, 1, 10, "Aisle 3"));

        using (var context = _database.CreateContext())
        {
            var repository = new EfStockRepository(context);

            Assert.Throws<DbUpdateException>(() =>
                repository.Add(new StockItem(sku, 1, 20, "Aisle 9")));
        }
    }

    [SqlServerFact]
    public void AuditEntry_IsPersistedWithUtcTimestamp()
    {
        const string detail = "Sale price changed from 3500 to 3900";
        var entityId = NewSku();

        using (var context = _database.CreateContext())
            new EfAuditLog(context).Record(
                new AuditEntry("PriceChanged", entityId, detail, "user-1", MovementDate));

        using (var context = _database.CreateContext())
        {
            var entry = context.AuditEntries.Single(candidate => candidate.EntityId == entityId);

            Assert.Equal("PriceChanged", entry.Action);
            Assert.Equal(detail, entry.Detail);
            Assert.Equal("user-1", entry.UserId);
            Assert.Equal(MovementDate, entry.Date);
            Assert.Equal(DateTimeKind.Utc, entry.Date.Kind);
        }
    }

    [SqlServerFact]
    public void ProductPriceService_WithEfRepositories_PersistsPriceAndAuditEntry()
    {
        var sku = NewSku();

        using (var context = _database.CreateContext())
            new EfProductRepository(context).Add(new Product(sku, NewBarcode(), "Tea x20", 6000m));

        using (var context = _database.CreateContext())
        {
            var service = new ProductPriceService(new EfProductRepository(context), new EfAuditLog(context));
            service.UpdatePrice(sku, 6500m, "user-7", MovementDate);
        }

        using (var context = _database.CreateContext())
        {
            Assert.Equal(6500m, new EfProductRepository(context).GetBySku(sku)!.SalePrice);

            var entry = context.AuditEntries.Single(candidate => candidate.EntityId == sku);
            Assert.Equal("PriceChanged", entry.Action);
            Assert.Equal("user-7", entry.UserId);
        }
    }

    [SqlServerFact]
    public void UpdatePrice_WhenAuditWriteFails_KeepsThePreviousPrice()
    {
        var sku = NewSku();

        using (var context = _database.CreateContext())
            new EfProductRepository(context).Add(new Product(sku, NewBarcode(), "Tea x20", 6000m));

        using (var context = _database.CreateContext())
        {
            var service = new ProductPriceService(
                new EfProductRepository(context),
                new ThrowingAuditLog());

            Assert.Throws<InvalidOperationException>(
                () => service.UpdatePrice(sku, 6500m, "user-7", MovementDate));
        }

        using (var context = _database.CreateContext())
            Assert.Equal(6000m, new EfProductRepository(context).GetBySku(sku)!.SalePrice);
    }

    private sealed class ThrowingAuditLog : IAuditLog
    {
        public void Record(AuditEntry entry) =>
            throw new InvalidOperationException("Simulated audit failure.");
    }
}
