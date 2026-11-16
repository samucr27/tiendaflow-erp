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
    private readonly Mock<IStockRepository> _stock = new();
    private readonly Dictionary<string, StockItem> _shelf = new(StringComparer.OrdinalIgnoreCase);
    private readonly RecordingAuditLog _audit = new();
    private readonly FakeCurrentUser _user = new(userId: 9);

    public SalesInvoiceServiceTests()
    {
        _stock.Setup(repository => repository.Get(1, It.IsAny<string>()))
            .Returns<int, string>((_, sku) => _shelf.GetValueOrDefault(sku));
        GivenStock("SKU-1", 10);
        GivenStock("SKU-2", 10);
    }

    private StockItem GivenStock(string sku, int quantity)
    {
        var item = new StockItem(sku, 1, quantity, "Pasillo 1", minStock: 2);
        _shelf[sku] = item;
        return item;
    }

    private SalesInvoiceService CreateService(IAccessControl? access = null) =>
        new(_invoices.Object, _query.Object, _stock.Object, NoOpUnitOfWork.Instance,
            access ?? new AllowAllAccessControl(), _audit, _user);

    private SalesInvoiceService CreateRestrictedService() =>
        CreateService(new AccessControl(
            new FakeCurrentUser(userId: 9, permissions: new HashSet<string>()),
            _audit));

    private static SalesInvoice Invoice(int id = 1, int? storeId = null) =>
        new(id, "Cliente", "123456", new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc), storeId);

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
    public void Create_TakesTheUnitsOutOfTheStoreStock()
    {
        CreateService().Create(new CreateInvoiceRequest(
            "Cliente", "123456", new DateTime(2026, 11, 10), new[] { Line("SKU-1"), Line("SKU-2") }));

        Assert.Equal(8, _shelf["SKU-1"].QuantityAvailable);
        Assert.Equal(8, _shelf["SKU-2"].QuantityAvailable);
        Assert.Equal(MovementType.Sale, _shelf["SKU-1"].LastMovement!.Type);
        _stock.Verify(repository => repository.Update(It.IsAny<StockItem>()), Times.Exactly(2));
    }

    [Fact]
    public void Create_StoresTheStoreOfTheUserWhoIssuedIt()
    {
        SalesInvoice? added = null;
        _invoices.Setup(repository => repository.Add(It.IsAny<SalesInvoice>()))
            .Callback<SalesInvoice>(invoice => added = invoice);

        CreateService().Create(new CreateInvoiceRequest(
            "Cliente", "123456", new DateTime(2026, 11, 10), new[] { Line() }));

        Assert.Equal(1, added!.StoreId);
    }

    [Fact]
    public void Create_WithMoreUnitsThanTheStock_ThrowsAndDoesNotSave()
    {
        GivenStock("SKU-1", 1);

        Assert.Throws<InsufficientStockException>(() => CreateService().Create(
            new CreateInvoiceRequest("Cliente", "123456", DateTime.UtcNow, new[] { Line("SKU-1") })));

        _invoices.Verify(repository => repository.Add(It.IsAny<SalesInvoice>()), Times.Never);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Create_ForAProductWithoutStockInTheStore_Throws()
    {
        Assert.Throws<StockNotFoundException>(() => CreateService().Create(
            new CreateInvoiceRequest("Cliente", "123456", DateTime.UtcNow, new[] { Line("SKU-SIN-STOCK") })));

        _invoices.Verify(repository => repository.Add(It.IsAny<SalesInvoice>()), Times.Never);
    }

    [Fact]
    public void Create_ByAUserWithoutStore_Throws()
    {
        var service = new SalesInvoiceService(_invoices.Object, _query.Object, _stock.Object,
            NoOpUnitOfWork.Instance, new AllowAllAccessControl(), _audit, new FakeCurrentUser(storeId: null));

        Assert.Throws<InvalidInvoiceException>(() => service.Create(
            new CreateInvoiceRequest("Cliente", "123456", DateTime.UtcNow, new[] { Line() })));
    }

    [Fact]
    public void Cancel_PutsTheUnitsBackOnTheShelf()
    {
        var invoice = Invoice(3, storeId: 1);
        invoice.AddLine("SKU-1", "Producto", 4, 1000m);
        _invoices.Setup(repository => repository.GetById(3)).Returns(invoice);

        CreateService().Cancel(3);

        Assert.Equal(14, _shelf["SKU-1"].QuantityAvailable);
        Assert.Equal(MovementType.Return, _shelf["SKU-1"].LastMovement!.Type);
        Assert.Contains("devolvió 4 unidades", _audit.Entries.Single().Detail);
    }

    [Fact]
    public void Cancel_AnInvoiceIssuedBeforeStockMovements_DoesNotTouchTheStock()
    {
        var invoice = Invoice(3);
        invoice.AddLine("SKU-1", "Producto", 4, 1000m);
        _invoices.Setup(repository => repository.GetById(3)).Returns(invoice);

        CreateService().Cancel(3);

        Assert.Equal(10, _shelf["SKU-1"].QuantityAvailable);
        _stock.Verify(repository => repository.Update(It.IsAny<StockItem>()), Times.Never);
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
