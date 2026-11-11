using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class CustomerTests
{
    [Fact]
    public void Constructor_TrimsAndNormalizesTheOptionalFields()
    {
        var customer = new Customer(
            1, " 123 ", "  Ana Torres  ", " ana@example.com ", " ", null);

        Assert.Equal("123", customer.DocumentNumber);
        Assert.Equal("Ana Torres", customer.FullName);
        Assert.Equal("ana@example.com", customer.Email);
        Assert.Null(customer.Phone);
        Assert.Null(customer.Address);
        Assert.True(customer.Active);
    }

    [Fact]
    public void UpdateDetails_ChangesTheContactData()
    {
        var customer = new Customer(1, "123", "Ana");

        customer.UpdateDetails("Ana María", "ana@example.com", "3001234567", "Calle 1");

        Assert.Equal("Ana María", customer.FullName);
        Assert.Equal("ana@example.com", customer.Email);
        Assert.Equal("3001234567", customer.Phone);
        Assert.Equal("Calle 1", customer.Address);
    }

    [Fact]
    public void UpdateDetails_WithAnEmptyName_Throws()
    {
        var customer = new Customer(1, "123", "Ana");

        Assert.Throws<InvalidCustomerException>(
            () => customer.UpdateDetails(" ", null, null, null));
    }

    [Fact]
    public void DeactivateAndActivate_ChangeTheStatus()
    {
        var customer = new Customer(1, "123", "Ana");

        customer.Deactivate();
        Assert.False(customer.Active);

        customer.Activate();
        Assert.True(customer.Active);
    }

    [Fact]
    public void Constructor_RequiresDocumentAndName()
    {
        Assert.Throws<InvalidCustomerException>(() => new Customer(1, " ", "Ana"));
        Assert.Throws<InvalidCustomerException>(() => new Customer(1, "123", " "));
    }
}
