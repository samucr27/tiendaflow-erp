using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class SupplierTests
{
    [Fact]
    public void New_TrimsTextAndKeepsTheDefaults()
    {
        var supplier = new Supplier(1, " 900123 ", " Andina ", " Calle 1 ");

        Assert.Equal("900123", supplier.Nit);
        Assert.Equal("Andina", supplier.BusinessName);
        Assert.Equal("Calle 1", supplier.Address);
        Assert.Equal(100m, supplier.Rating);
        Assert.True(supplier.Active);
    }

    [Theory]
    [InlineData("", "Andina", "Calle 1")]
    [InlineData("900123", " ", "Calle 1")]
    [InlineData("900123", "Andina", "")]
    public void New_WithMissingText_Throws(string nit, string name, string address)
    {
        Assert.Throws<InvalidSupplierException>(() => new Supplier(1, nit, name, address));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void New_WithRatingOutOfRange_Throws(int rating)
    {
        Assert.Throws<InvalidSupplierException>(
            () => new Supplier(1, "900123", "Andina", "Calle 1", (decimal)rating));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void ChangeRating_AcceptsTheBoundaries(int rating)
    {
        var supplier = new Supplier(1, "900123", "Andina", "Calle 1");

        supplier.ChangeRating(rating);

        Assert.Equal(rating, supplier.Rating);
    }

    [Fact]
    public void ChangeRating_OutOfRange_Throws()
    {
        var supplier = new Supplier(1, "900123", "Andina", "Calle 1");

        Assert.Throws<InvalidSupplierException>(() => supplier.ChangeRating(150m));
    }

    [Fact]
    public void UpdateDetails_ChangesNameAndAddress()
    {
        var supplier = new Supplier(1, "900123", "Andina", "Calle 1");

        supplier.UpdateDetails(" Andina SAS ", " Carrera 2 ");

        Assert.Equal("Andina SAS", supplier.BusinessName);
        Assert.Equal("Carrera 2", supplier.Address);
    }

    [Fact]
    public void UpdateDetails_WithBlankName_Throws()
    {
        var supplier = new Supplier(1, "900123", "Andina", "Calle 1");

        Assert.Throws<InvalidSupplierException>(() => supplier.UpdateDetails(" ", "Carrera 2"));
    }

    [Fact]
    public void DeactivateAndActivate_ToggleTheFlag()
    {
        var supplier = new Supplier(1, "900123", "Andina", "Calle 1");

        supplier.Deactivate();
        Assert.False(supplier.Active);

        supplier.Activate();
        Assert.True(supplier.Active);
    }
}