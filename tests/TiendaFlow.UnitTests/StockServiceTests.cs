using Moq;
using TiendaFlow.Application;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class StockServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0);

    private readonly Mock<IStockRepository> _repo = new();
    private readonly StockService _service;

    public StockServiceTests() => _service = new StockService(_repo.Object);

    private StockItem GivenStock(int quantity = 10)
    {
        var item = new StockItem("SKU-001", 1, quantity, "Aisle 3");
        _repo.Setup(r => r.Get(1, "SKU-001")).Returns(item);
        return item;
    }

    [Fact]
    public void GetStock_ReturnsQuantityLocationAndLastMovement()
    {
        var item = GivenStock();
        item.RegisterSale(2, Now);

        var info = _service.GetStock(1, "SKU-001");

        Assert.Equal(8, info.QuantityAvailable);
        Assert.Equal("Aisle 3", info.Location);
        Assert.Equal(new InventoryMovement(MovementType.Sale, 2, Now), info.LastMovement);
    }

    [Fact]
    public void GetStock_WithUnknownItem_ThrowsStockNotFoundException()
    {
        _repo.Setup(r => r.Get(1, "SKU-404")).Returns((StockItem?)null);

        Assert.Throws<StockNotFoundException>(() => _service.GetStock(1, "SKU-404"));
    }

    [Fact]
    public void RegisterSale_ThenGetStock_ReflectsTheSale()
    {
        GivenStock();

        _service.RegisterSale(1, "SKU-001", 4, Now);
        var info = _service.GetStock(1, "SKU-001");

        Assert.Equal(6, info.QuantityAvailable);
        Assert.Equal(new InventoryMovement(MovementType.Sale, 4, Now), info.LastMovement);
        _repo.Verify(r => r.Update(It.IsAny<StockItem>()), Times.Once);
    }

    [Fact]
    public void RegisterSale_WithUnknownItem_ThrowsStockNotFoundException()
    {
        _repo.Setup(r => r.Get(1, "SKU-404")).Returns((StockItem?)null);

        Assert.Throws<StockNotFoundException>(() => _service.RegisterSale(1, "SKU-404", 1, Now));
    }

    [Fact]
    public void RegisterSale_MoreThanAvailable_ThrowsAndDoesNotSave()
    {
        GivenStock(3);

        Assert.Throws<InsufficientStockException>(() => _service.RegisterSale(1, "SKU-001", 5, Now));
        _repo.Verify(r => r.Update(It.IsAny<StockItem>()), Times.Never);
    }
}
