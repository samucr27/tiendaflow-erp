using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class ProductTests
{
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
