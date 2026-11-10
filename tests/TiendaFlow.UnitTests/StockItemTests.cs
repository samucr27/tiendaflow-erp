using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class StockItemTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0);

    private static StockItem CreateStock(int quantity = 10) =>
        new("SKU-001", 1, quantity, "Aisle 3");

    [Fact]
    public void NewStockItem_HasLocationAndNoLastMovement()
    {
        var stock = CreateStock();

        Assert.Equal(10, stock.QuantityAvailable);
        Assert.Equal("Aisle 3", stock.Location);
        Assert.Null(stock.LastMovement);
    }

    [Fact]
    public void RegisterSale_ReducesQuantityAndRecordsLastMovement()
    {
        var stock = CreateStock();

        stock.RegisterSale(4, Now);

        Assert.Equal(6, stock.QuantityAvailable);
        Assert.Equal(new InventoryMovement(MovementType.Sale, 4, Now), stock.LastMovement);
    }

    [Fact]
    public void RegisterSale_MoreThanAvailable_ThrowsAndKeepsQuantity()
    {
        var stock = CreateStock(3);

        Assert.Throws<InsufficientStockException>(() => stock.RegisterSale(5, Now));
        Assert.Equal(3, stock.QuantityAvailable);
    }

    [Fact]
    public void RegisterReceipt_IncreasesQuantityAndRecordsLastMovement()
    {
        var stock = CreateStock();

        stock.RegisterReceipt(5, Now);

        Assert.Equal(15, stock.QuantityAvailable);
        Assert.Equal(new InventoryMovement(MovementType.Receipt, 5, Now), stock.LastMovement);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RegisterSale_WithNonPositiveQuantity_ThrowsArgumentOutOfRange(int quantity)
    {
        var stock = CreateStock();

        Assert.Throws<ArgumentOutOfRangeException>(() => stock.RegisterSale(quantity, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RegisterReceipt_WithNonPositiveQuantity_ThrowsArgumentOutOfRange(int quantity)
    {
        var stock = CreateStock();

        Assert.Throws<ArgumentOutOfRangeException>(() => stock.RegisterReceipt(quantity, Now));
    }
}
