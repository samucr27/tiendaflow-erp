using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class ProductChangePriceTests
{
    [Fact]
    public void ChangePrice_WithValidPrice_UpdatesSalePrice()
    {
        var product = new Product("SKU-007", "7701234567895", "Coffee 250 g", 8000m);

        product.ChangePrice(8500m);

        Assert.Equal(8500m, product.SalePrice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ChangePrice_WithNonPositivePrice_ThrowsInvalidProductException(decimal newPrice)
    {
        var product = new Product("SKU-008", "7701234567896", "Tea x20", 6000m);

        Assert.Throws<InvalidProductException>(() => product.ChangePrice(newPrice));
        Assert.Equal(6000m, product.SalePrice);
    }
}
