using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class SalesInvoiceServiceTests
{
    private readonly Mock<IInvoiceRepository> _invoices = new();
    private readonly Mock<IInvoiceQuery> _query = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly FakeCurrentUser _user = new(userId: 9);

    private SalesInvoiceService CreateService(IAccessControl? access = null) =>
        new(_invoices.Object, _query.Object, access ?? new AllowAllAccessControl(), _audit, _user);

    private SalesInvoiceService CreateRestrictedService() =>
        CreateService(new AccessControl(
            new FakeCurrentUser(userId: 9, permissions: new HashSet<string>()),
            _audit));

    private static SalesInvoice Invoice(int id = 1) =>
        new(id, "Cliente", "123456", new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc));

    private static InvoiceLineRequest Line(string sku = "SKU-1") =>
        new(sku, "Producto", 2, 2500m);

    private static InvoiceRow Row(int id) =>
        new(id, "Cliente", "123456", DateTime.UtcNow, InvoiceStatus.Issued, 1, 2500m);

    [Fact]
    public void List_ReturnsTheRowsOfTheQuery()
    {
        var rows = new[] { Row(1), Row(2) };
        _query.Setup(query => query.List()).Returns(rows);

        Assert.Equal(rows, CreateService().List());
    }

    [Fact]
    public void Get_ReturnsTheDetail()
    {
        var detail = new InvoiceDetail(
            1, "Cliente", "123456", DateTime.UtcNow, InvoiceStatus.Issued,
            new[] { new InvoiceLineRow("SKU-1", "Producto", 2, 2500m) });
        _query.Setup(query => query.GetById(1)).Returns(detail);

        var result = CreateService().Get(1);

        Assert.Equal(detail, result);
        Assert.Equal(5000m, result.Total);
    }

    [Fact]
    public void Get_WithUnknownId_Throws()
    {
        Assert.Throws<InvoiceNotFoundException>(() => CreateService().Get(99));
    }

    [Fact]
    public void GetForEdit_WithUnknownId_Throws()
    {
        Assert.Throws<InvoiceNotFoundException>(() => CreateService().GetForEdit(99));
    }

    [Fact]
    public void Create_RegistersTheInvoiceWithItsLinesAndAudits()
    {
        SalesInvoice? added = null;
        _invoices.Setup(repository => repository.Add(It.IsAny<SalesInvoice>()))
            .Callback<SalesInvoice>(invoice =>
            {
                TestIds.Assign(invoice, 7);
                added = invoice;
            });

        var id = CreateService().Create(new CreateInvoiceRequest(
            "Cliente", "123456", new DateTime(2026, 11, 10), new[] { Line() }));

        Assert.Equal(7, id);
        Assert.NotNull(added);
        Assert.Single(added!.Lines);
        Assert.Equal(5000m, added.Total);

        var entry = _audit.Entries.Single();
        Assert.Equal("InvoiceCreated", entry.Action);
        Assert.Equal("sales", entry.Module);
        Assert.Equal(9, entry.UserId);
    }

    [Fact]
    public void Create_WithoutLines_ThrowsAndDoesNotSave()
    {
        Assert.Throws<InvalidInvoiceException>(() => CreateService().Create(
            new CreateInvoiceRequest("Cliente", "123456", DateTime.UtcNow, Array.Empty<InvoiceLineRequest>())));

        _invoices.Verify(repository => repository.Add(It.IsAny<SalesInvoice>()), Times.Never);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Update_ReplacesTheLinesAndAudits()
    {
        var invoice = Invoice(3);
        invoice.AddLine("SKU-1", "Producto", 1, 1000m);
        _invoices.Setup(repository => repository.GetById(3)).Returns(invoice);

        CreateService().Update(3, new UpdateInvoiceRequest(
            "Cliente actualizado", "999", new DateTime(2026, 11, 9), new[] { Line("SKU-2") }));

        Assert.Equal("Cliente actualizado", invoice.CustomerName);
        Assert.Single(invoice.Lines);
        Assert.Equal("SKU-2", invoice.Lines[0].ProductSku);
        Assert.Equal(5000m, invoice.Total);
        _invoices.Verify(repository => repository.Update(invoice), Times.Once);

        Assert.Equal("InvoiceUpdated", _audit.Entries.Single().Action);
    }

    [Fact]
    public void Update_WithoutLines_Throws()
    {
        _invoices.Setup(repository => repository.GetById(3)).Returns(Invoice(3));

        Assert.Throws<InvalidInvoiceException>(() => CreateService().Update(
            3,
            new UpdateInvoiceRequest("Cliente", "123", DateTime.UtcNow, Array.Empty<InvoiceLineRequest>())));
    }

    [Fact]
    public void Update_OnACancelledInvoice_Throws()
    {
        var invoice = Invoice(3);
        invoice.Cancel();
        _invoices.Setup(repository => repository.GetById(3)).Returns(invoice);

        Assert.Throws<InvalidInvoiceException>(() => CreateService().Update(
            3,
            new UpdateInvoiceRequest("Cliente", "123", DateTime.UtcNow, new[] { Line() })));
    }

    [Fact]
    public void Cancel_ChangesTheStatusAndAudits()
    {
        var invoice = Invoice(3);
        _invoices.Setup(repository => repository.GetById(3)).Returns(invoice);

        CreateService().Cancel(3);

        Assert.Equal(InvoiceStatus.Cancelled, invoice.Status);
        _invoices.Verify(repository => repository.Update(invoice), Times.Once);
        Assert.Equal("InvoiceCancelled", _audit.Entries.Single().Action);
    }

    [Fact]
    public void Cancel_AnAlreadyCancelledInvoice_Throws()
    {
        var invoice = Invoice(3);
        invoice.Cancel();
        _invoices.Setup(repository => repository.GetById(3)).Returns(invoice);

        Assert.Throws<InvalidInvoiceException>(() => CreateService().Cancel(3));
    }

    [Fact]
    public void List_WithoutPermission_Throws()
    {
        var exception = Assert.Throws<AccessDeniedException>(() => CreateRestrictedService().List());

        Assert.Equal(Permissions.SalesInvoicesView, exception.Permission);
    }

    [Fact]
    public void Create_WithoutPermission_ThrowsAndDoesNotSave()
    {
        var exception = Assert.Throws<AccessDeniedException>(() => CreateRestrictedService().Create(
            new CreateInvoiceRequest("Cliente", "123", DateTime.UtcNow, new[] { Line() })));

        Assert.Equal(Permissions.SalesInvoicesManage, exception.Permission);
        _invoices.Verify(repository => repository.Add(It.IsAny<SalesInvoice>()), Times.Never);
    }

    [Fact]
    public void Cancel_WithoutPermission_Throws()
    {
        var exception = Assert.Throws<AccessDeniedException>(() => CreateRestrictedService().Cancel(1));

        Assert.Equal(Permissions.SalesInvoicesManage, exception.Permission);
    }
}
