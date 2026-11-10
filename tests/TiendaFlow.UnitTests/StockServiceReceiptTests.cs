using Moq;
using TiendaFlow.Application;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class StockServiceReceiptTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0);

    private readonly Mock<IStockRepository> _repo = new();
    private readonly StockService _service;

    public StockServiceReceiptTests() =>
        _service = new StockService(_repo.Object, new AllowAllAccessControl());

    [Fact]
    public void ReceiveStock_WithExistingItem_AddsUnitsAndRecordsTheReceipt()
    {
        var item = new StockItem("SKU-001", 1, 10, "Pasillo 1", minStock: 4);
        _repo.Setup(r => r.Get(1, "SKU-001")).Returns(item);

        _service.ReceiveStock(1, "SKU-001", 6, "Pasillo 1", 4, Now);

        var info = _service.GetStock(1, "SKU-001");
        Assert.Equal(16, info.QuantityAvailable);
        Assert.Equal(new InventoryMovement(MovementType.Receipt, 6, Now), info.LastMovement);
        _repo.Verify(r => r.Update(item), Times.Once);
        _repo.Verify(r => r.Add(It.IsAny<StockItem>()), Times.Never);
    }

    [Fact]
    public void ReceiveStock_WithUnknownItem_CreatesTheStockRowAtTheRequestedLocation()
    {
        StockItem? added = null;
        _repo.Setup(r => r.Get(9, "SKU-404")).Returns((StockItem?)null);
        _repo.Setup(r => r.Add(It.IsAny<StockItem>()))
            .Callback<StockItem>(item => added = item);

        _service.ReceiveStock(9, "SKU-404", 12, "Bodega", minStock: 5, Now);

        Assert.NotNull(added);
        Assert.Equal("SKU-404", added.Sku);
        Assert.Equal(9, added.StoreId);
        Assert.Equal(12, added.QuantityAvailable);
        Assert.Equal("Bodega", added.Location);
        Assert.Equal(5, added.MinStock);
        Assert.Equal(new InventoryMovement(MovementType.Receipt, 12, Now), added.LastMovement);
        _repo.Verify(r => r.Update(It.IsAny<StockItem>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void ReceiveStock_WithInvalidQuantity_ThrowsAndDoesNotPersist(int quantity)
    {
        _repo.Setup(r => r.Get(1, "SKU-001")).Returns((StockItem?)null);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.ReceiveStock(1, "SKU-001", quantity, "Pasillo 1", 0, Now));

        _repo.Verify(r => r.Add(It.IsAny<StockItem>()), Times.Never);
        _repo.Verify(r => r.Update(It.IsAny<StockItem>()), Times.Never);
    }
}
