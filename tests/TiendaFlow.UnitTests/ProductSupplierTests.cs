using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class ProductSupplierTests
{
    [Fact]
    public void New_TrimsTheSkuAndKeepsTheCost()
    {
        var relation = new ProductSupplier(" SKU-1 ", 5, 1500m, isDefault: true);

        Assert.Equal("SKU-1", relation.ProductSku);
        Assert.Equal(5, relation.SupplierId);
        Assert.Equal(1500m, relation.LastCost);
        Assert.True(relation.IsDefault);
    }

    [Theory]
    [InlineData("", 5, 100)]
    [InlineData("  ", 5, 100)]
    [InlineData("SKU-1", 0, 100)]
    [InlineData("SKU-1", -1, 100)]
    [InlineData("SKU-1", 5, 0)]
    [InlineData("SKU-1", 5, -1)]
    public void New_WithInvalidData_Throws(string sku, int supplierId, decimal cost)
    {
        Assert.Throws<InvalidProductSupplierException>(
            () => new ProductSupplier(sku, supplierId, cost));
    }

    [Fact]
    public void ChangeCost_UpdatesTheLastCost()
    {
        var relation = new ProductSupplier("SKU-1", 5, 1000m);

        relation.ChangeCost(1200m);

        Assert.Equal(1200m, relation.LastCost);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ChangeCost_WithANonPositiveValue_Throws(decimal cost)
    {
        var relation = new ProductSupplier("SKU-1", 5, 1000m);

        Assert.Throws<InvalidProductSupplierException>(() => relation.ChangeCost(cost));
        Assert.Equal(1000m, relation.LastCost);
    }

    [Fact]
    public void MarkAndClearDefault_ToggleTheFlag()
    {
        var relation = new ProductSupplier("SKU-1", 5, 1000m);

        relation.MarkAsDefault();
        Assert.True(relation.IsDefault);

        relation.ClearDefault();
        Assert.False(relation.IsDefault);
    }
}
