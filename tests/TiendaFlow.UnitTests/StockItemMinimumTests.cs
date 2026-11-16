using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class StockItemMinimumTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0);

    [Fact]
    public void IsBelowMinimum_WhenStockDropsToTheMinimum_ReturnsTrue()
    {
        var stock = new StockItem("SKU-001", 1, 15, "Aisle 3", minStock: 10);

        stock.RegisterSale(5, Now);

        Assert.True(stock.IsBelowMinimum());
    }

    [Fact]
    public void IsBelowMinimum_WhenStockIsAboveTheMinimum_ReturnsFalse()
    {
        var stock = new StockItem("SKU-001", 1, 15, "Aisle 3", minStock: 10);

        stock.RegisterSale(4, Now);

        Assert.False(stock.IsBelowMinimum());
    }

    [Theory]
    [InlineData(11, 10, false)]
    [InlineData(10, 10, true)]
    [InlineData(9, 10, true)]
    public void IsBelowMinimum_ComparesQuantityWithMinimum(int quantity, int minStock, bool expected)
    {
        var stock = new StockItem("SKU-001", 1, quantity, "Aisle 3", minStock);

        Assert.Equal(expected, stock.IsBelowMinimum());
    }

    [Fact]
    public void Constructor_WithNegativeMinimum_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StockItem("SKU-001", 1, 10, "Aisle 3", minStock: -1));
    }
}
