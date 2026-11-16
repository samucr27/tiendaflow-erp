using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class SalesInvoiceTests
{
    private static SalesInvoice Invoice() =>
        new(1, "Cliente", "123456", new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void AddLine_AccumulatesTheTotal()
    {
        var invoice = Invoice();

        invoice.AddLine("SKU-1", "Agua", 2, 2500m);
        invoice.AddLine("SKU-2", "Pan", 1, 5500m);

        Assert.Equal(2, invoice.Lines.Count);
        Assert.Equal(10500m, invoice.Total);
        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
    }

    [Fact]
    public void AddLine_WithADuplicateSku_Throws()
    {
        var invoice = Invoice();
        invoice.AddLine("SKU-1", "Agua", 1, 2500m);

        Assert.Throws<InvalidInvoiceException>(
            () => invoice.AddLine(" SKU-1 ", "Otra agua", 1, 2500m));
    }

    [Fact]
    public void AddLine_WithInvalidQuantityOrPrice_Throws()
    {
        var invoice = Invoice();

        Assert.Throws<InvalidInvoiceException>(
            () => invoice.AddLine("SKU-1", "Agua", 0, 2500m));
        Assert.Throws<InvalidInvoiceException>(
            () => invoice.AddLine("SKU-1", "Agua", 1, 0m));
    }

    [Fact]
    public void Cancel_ChangesTheStatusAndBlocksFurtherEdits()
    {
        var invoice = Invoice();
        invoice.AddLine("SKU-1", "Agua", 1, 2500m);
        invoice.Cancel();

        Assert.Equal(InvoiceStatus.Cancelled, invoice.Status);
        Assert.Throws<InvalidInvoiceException>(
            () => invoice.AddLine("SKU-2", "Pan", 1, 5500m));
        Assert.Throws<InvalidInvoiceException>(() => invoice.Cancel());
    }

    [Fact]
    public void AnIssuedInvoiceOffersNoWayToEditItsData()
    {
        var editMethods = typeof(SalesInvoice).GetMethods()
            .Select(method => method.Name)
            .Where(name => name is "UpdateDetails" or "ClearLines" or "RemoveLine");

        Assert.Empty(editMethods);
    }

    [Fact]
    public void Constructor_KeepsTheStoreAndRejectsAnInvalidOne()
    {
        var invoice = new SalesInvoice(0, "Cliente", "123", DateTime.UtcNow, storeId: 2);

        Assert.Equal(2, invoice.StoreId);
        Assert.Throws<InvalidInvoiceException>(
            () => new SalesInvoice(0, "Cliente", "123", DateTime.UtcNow, storeId: 0));
    }

    [Fact]
    public void Constructor_RequiresCustomerNameAndDocument()
    {
        Assert.Throws<InvalidInvoiceException>(
            () => new SalesInvoice(1, " ", "123", DateTime.UtcNow));
        Assert.Throws<InvalidInvoiceException>(
            () => new SalesInvoice(1, "Cliente", "", DateTime.UtcNow));
    }
}
