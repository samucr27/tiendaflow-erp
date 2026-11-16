using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure;
using Xunit;

namespace TiendaFlow.UnitTests;

public class PosSaleServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 15, 30, 0, DateTimeKind.Utc);

    private readonly Mock<IStockRepository> _stock = new();
    private readonly InMemoryProductRepository _products = new();
    private readonly List<PosSale> _saved = new();
    private readonly Mock<IPosSaleRepository> _sales = new();
    private readonly RecordingAuditLog _audit = new();

    public PosSaleServiceTests()
    {
        _products.Add(new Product("SKU-1002", "7701234567002", "Gaseosa 400 ml", 3500m));
        _sales.Setup(repository => repository.Add(It.IsAny<PosSale>()))
            .Callback<PosSale>(sale =>
            {
                TestIds.Assign(sale, _saved.Count + 1);
                _saved.Add(sale);
            });
    }

    private StockItem GivenStock(int quantity, int minStock)
    {
        var item = new StockItem("SKU-1002", 1, quantity, "Nevera 1", minStock);
        _stock.Setup(repository => repository.Get(1, "SKU-1002")).Returns(item);
        return item;
    }

    private PosSaleService CreateService(IAccessControl? access = null, ICurrentUser? user = null) =>
        new(_stock.Object, _products, _sales.Object, NoOpUnitOfWork.Instance,
            access ?? new AllowAllAccessControl(), _audit,
            user ?? new FakeCurrentUser(userId: 4), new FixedClock(Now));

    [Fact]
    public void Register_DiscountsTheStockAndSavesTheSaleWithTheCatalogPrice()
    {
        var item = GivenStock(quantity: 25, minStock: 8);

        var receipt = CreateService().Register(1, "SKU-1002", 3);

        Assert.Equal(22, item.QuantityAvailable);
        var sale = Assert.Single(_saved);
        Assert.Equal(1, sale.StoreId);
        Assert.Equal(4, sale.CashierUserId);
        Assert.Equal(Now, sale.SoldAt);
        Assert.Equal(10500m, sale.Total);
        var line = Assert.Single(sale.Lines);
        Assert.Equal("Gaseosa 400 ml", line.ProductName);
        Assert.Equal(3500m, line.UnitPrice);

        Assert.Equal(sale.Id, receipt.SaleId);
        Assert.Equal(22, receipt.RemainingStock);
        Assert.False(receipt.LeftStockCritical);
        Assert.Equal("PosSaleRegistered", _audit.Entries.Single().Action);
    }

    [Fact]
    public void Register_RaisesTheCriticalStockAlertWhenTheSaleReachesTheMinimum()
    {
        GivenStock(quantity: 10, minStock: 8);

        var receipt = CreateService().Register(1, "SKU-1002", 2);

        Assert.True(receipt.LeftStockCritical);
        Assert.Equal(8, receipt.MinStock);
    }

    [Fact]
    public void Register_WithoutEnoughStock_ThrowsAndSavesNothing()
    {
        GivenStock(quantity: 1, minStock: 0);

        Assert.Throws<InsufficientStockException>(() => CreateService().Register(1, "SKU-1002", 5));

        Assert.Empty(_saved);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Register_ForAnUnknownProduct_Throws()
    {
        Assert.Throws<ProductNotFoundException>(() => CreateService().Register(1, "SKU-9999", 1));
    }

    [Fact]
    public void Register_WithoutAStore_Throws()
    {
        Assert.Throws<InvalidPosSaleException>(() => CreateService().Register(0, "SKU-1002", 1));
    }

    [Fact]
    public void Register_WithoutThePosPermission_IsRejected()
    {
        GivenStock(quantity: 10, minStock: 0);
        var denied = new AccessControl(
            new FakeCurrentUser(permissions: new HashSet<string>()),
            _audit);

        var exception = Assert.Throws<AccessDeniedException>(
            () => CreateService(denied).Register(1, "SKU-1002", 1));

        Assert.Equal(Permissions.SalesPosUse, exception.Permission);
        Assert.Empty(_saved);
    }
}

public class PosSaleTests
{
    [Fact]
    public void AddLine_AccumulatesTheTotalAndRejectsADuplicateProduct()
    {
        var sale = new PosSale("VTA-1", 1, 4, DateTime.UtcNow);

        sale.AddLine("SKU-1", "Agua", 2, 2500m);
        sale.AddLine("SKU-2", "Pan", 1, 5500m);

        Assert.Equal(10500m, sale.Total);
        Assert.Throws<InvalidPosSaleException>(() => sale.AddLine("sku-1", "Agua", 1, 2500m));
    }

    [Fact]
    public void Constructor_AndLines_ValidateTheirData()
    {
        Assert.Throws<InvalidPosSaleException>(() => new PosSale("", 1, 4, DateTime.UtcNow));
        Assert.Throws<InvalidPosSaleException>(() => new PosSale("VTA-1", 0, 4, DateTime.UtcNow));

        var sale = new PosSale("VTA-1", 1, null, DateTime.UtcNow);
        Assert.Throws<InvalidPosSaleException>(() => sale.AddLine("SKU-1", "Agua", 0, 2500m));
        Assert.Throws<InvalidPosSaleException>(() => sale.AddLine("SKU-1", "Agua", 1, 0m));
        Assert.Throws<InvalidPosSaleException>(() => sale.AddLine("SKU-1", " ", 1, 2500m));
    }

    [Fact]
    public void StockReturn_AddsTheUnitsBackAsAReturnMovement()
    {
        var item = new StockItem("SKU-1", 1, 3, "Pasillo 1");
        var date = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);

        item.RegisterReturn(2, date);

        Assert.Equal(5, item.QuantityAvailable);
        Assert.Equal(new InventoryMovement(MovementType.Return, 2, date), item.LastMovement);
        Assert.Throws<ArgumentOutOfRangeException>(() => item.RegisterReturn(0, date));
    }
}
