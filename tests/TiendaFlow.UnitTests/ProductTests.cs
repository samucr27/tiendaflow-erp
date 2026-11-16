using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class ProductTests
{
    [Fact]
    public void Constructor_WithValidData_SetsProperties()
    {
        var product = new Product("SKU-006", "7701234567894", "Juice 1 L", 5200m);

        Assert.Equal("SKU-006", product.Sku);
        Assert.Equal("7701234567894", product.Barcode);
        Assert.Equal("Juice 1 L", product.Name);
        Assert.Equal(5200m, product.SalePrice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Constructor_WithNonPositivePrice_ThrowsInvalidProductException(decimal price)
    {
        Assert.Throws<InvalidProductException>(
            () => new Product("SKU-004", "7701234567892", "Cookies", price));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_ThrowsInvalidProductException(string name)
    {
        Assert.Throws<InvalidProductException>(
            () => new Product("SKU-005", "7701234567893", name, 1500m));
    }
}
