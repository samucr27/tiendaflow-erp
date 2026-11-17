using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class SalesReturnServiceTests
{
    private static readonly DateTime Date = new(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ISalesReturnRepository> _returns = new();
    private readonly Mock<ISalesReturnQuery> _query = new();
    private readonly Mock<IInvoiceRepository> _invoices = new();
    private readonly Mock<IStockRepository> _stock = new();
    private readonly Mock<IStoreRepository> _stores = new();
    private readonly RecordingUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly FakeCurrentUser _user = new(userId: 5, ip: "10.0.0.7");
    private readonly FixedClock _clock = new(Date);
    private readonly Store _store = new(1, "Tienda Centro", "Calle 10");
    private readonly SalesInvoice _invoice;

    public SalesReturnServiceTests()
    {
        _invoice = new SalesInvoice(0, "Ana Torres", "1020304050", Date.AddDays(-1));
        _invoice.AddLine("SKU-1", "Agua 600 ml", 3, 2500m);
        _invoice.AddLine("SKU-2", "Gaseosa 400 ml", 2, 3500m);
        TestIds.Assign(_invoice, 12);

        _invoices.Setup(repository => repository.GetById(12)).Returns(_invoice);
        _stores.Setup(repository => repository.GetById(1)).Returns(_store);
        _returns.Setup(repository => repository.ListByInvoice(It.IsAny<int>()))
            .Returns(Array.Empty<SalesReturn>());
        _returns.Setup(repository => repository.Add(It.IsAny<SalesReturn>()))
            .Callback<SalesReturn>(item => TestIds.Assign(item, 21));
    }

    [Fact]
    public void CreateSnapshotsTheInvoiceLineAndTotalsTheReturn()
    {
        var service = CreateService();

        var id = service.Create(Request(("SKU-1", 2), ("SKU-2", 1)));

        Assert.Equal(21, id);
        _returns.Verify(repository => repository.Add(It.Is<SalesReturn>(item =>
            item.InvoiceId == 12
            && item.StoreId == 1
            && item.Status == SalesReturnStatus.Pending
            && item.CustomerName == "Ana Torres"
            && item.Lines.Count == 2
            && item.Lines[0].ProductSku == "SKU-1"
            && item.Lines[0].Description == "Agua 600 ml"
            && item.Lines[0].UnitPrice == 2500m
            && item.Lines[1].Quantity == 1
            && item.Total == 8500m)), Times.Once);

        Assert.Equal(1, _unitOfWork.Executions);
        Assert.Equal("SalesReturnCreated", _audit.Entries[^1].Action);
        Assert.Equal("sales", _audit.Entries[^1].Module);
        Assert.Equal("10.0.0.7", _audit.Entries[^1].Ip);
        Assert.Equal(5, _audit.Entries[^1].UserId);
    }

    [Fact]
    public void CreateRejectsAQuantityGreaterThanTheInvoicedUnits()
    {
        var service = CreateService();

        var exception = Assert.Throws<InvalidSalesReturnException>(() =>
            service.Create(Request(("SKU-1", 4))));

        Assert.Contains("Only 3 units", exception.Message);
        _returns.Verify(repository => repository.Add(It.IsAny<SalesReturn>()), Times.Never);
        Assert.Equal(0, _unitOfWork.Executions);
    }

    [Fact]
    public void CreateDiscountsTheUnitsAlreadyAuthorized()
    {
        var authorized = Pending(id: 7, sku: "SKU-1", quantity: 2);
        authorized.Authorize(Date, "NC-000007");
        _returns.Setup(repository => repository.ListByInvoice(12))
            .Returns(new[] { authorized });

        var service = CreateService();

        var exception = Assert.Throws<InvalidSalesReturnException>(() =>
            service.Create(Request(("SKU-1", 2))));

        Assert.Contains("Only 1 units", exception.Message);
    }

    [Fact]
    public void CreateRejectsAProductThatIsNotInTheInvoice()
    {
        var service = CreateService();

        Assert.Throws<InvalidSalesReturnException>(() =>
            service.Create(Request(("SKU-9", 1))));
    }

    [Fact]
    public void CreateRejectsTheSameProductTwice()
    {
        var service = CreateService();

        Assert.Throws<InvalidSalesReturnException>(() =>
            service.Create(Request(("SKU-1", 1), ("SKU-1", 1))));
    }

    [Fact]
    public void CreateRejectsAnEmptyLineList()
    {
        var service = CreateService();

        Assert.Throws<InvalidSalesReturnException>(() =>
            service.Create(new CreateSalesReturnRequest(12, 1, "Dañado", Array.Empty<SalesReturnLineRequest>())));
    }

    [Fact]
    public void CreateRejectsACancelledInvoice()
    {
        _invoice.Cancel();
        var service = CreateService();

        Assert.Throws<InvalidSalesReturnException>(() =>
            service.Create(Request(("SKU-1", 1))));
    }

    [Fact]
    public void CreateRejectsAnInactiveStore()
    {
        var inactive = new Store(2, "Tienda Norte", "Carrera 15", active: false);
        _stores.Setup(repository => repository.GetById(2)).Returns(inactive);
        var service = CreateService();

        Assert.Throws<InvalidSalesReturnException>(() =>
            service.Create(new CreateSalesReturnRequest(12, 2, "Dañado", new[]
            {
                new SalesReturnLineRequest("SKU-1", 1)
            })));
    }

    [Fact]
    public void CreateRejectsAMissingInvoice()
    {
        var service = CreateService();

        Assert.Throws<InvoiceNotFoundException>(() =>
            service.Create(new CreateSalesReturnRequest(99, 1, "Dañado", new[]
            {
                new SalesReturnLineRequest("SKU-1", 1)
            })));
    }

    [Fact]
    public void AuthorizeAssignsTheCreditNoteAndReturnsTheUnitsToStock()
    {
        var salesReturn = Pending(id: 21, sku: "SKU-1", quantity: 2);
        var stock = new StockItem("SKU-1", 1, 10, "Pasillo 1");

        _returns.Setup(repository => repository.GetById(21)).Returns(salesReturn);
        _stock.Setup(repository => repository.Get(1, "SKU-1")).Returns(stock);

        var service = CreateService();
        service.Authorize(21);

        Assert.Equal(SalesReturnStatus.Authorized, salesReturn.Status);
        Assert.Equal("NC-000021", salesReturn.CreditNoteNumber);
        Assert.Equal(Date, salesReturn.AuthorizedAt);
        Assert.Equal(12, stock.QuantityAvailable);
        Assert.Equal(MovementType.Return, stock.LastMovement!.Type);
        Assert.Equal(2, stock.LastMovement.Quantity);

        _stock.Verify(repository => repository.Update(stock), Times.Once);
        _returns.Verify(repository => repository.Update(salesReturn), Times.Once);
        Assert.Equal(1, _unitOfWork.Executions);
        Assert.Equal("SalesReturnAuthorized", _audit.Entries[^1].Action);
    }

    [Fact]
    public void AuthorizeRevalidatesAgainstAnotherAuthorizedReturn()
    {
        var other = Pending(id: 7, sku: "SKU-1", quantity: 2);
        other.Authorize(Date, "NC-000007");
        _returns.Setup(repository => repository.ListByInvoice(12))
            .Returns(new[] { other });

        var salesReturn = Pending(id: 21, sku: "SKU-1", quantity: 2);
        _returns.Setup(repository => repository.GetById(21)).Returns(salesReturn);

        var service = CreateService();

        Assert.Throws<InvalidSalesReturnException>(() => service.Authorize(21));
        _stock.Verify(repository => repository.Get(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        Assert.Equal(0, _unitOfWork.Executions);
        Assert.Equal(SalesReturnStatus.Pending, salesReturn.Status);
    }

    [Fact]
    public void AuthorizeStopsBeforeMutatingWhenTheStockRowIsMissing()
    {
        var salesReturn = Pending(id: 21, sku: "SKU-9", quantity: 1);
        _invoice.AddLine("SKU-9", "Producto nuevo", 1, 1000m);
        _returns.Setup(repository => repository.GetById(21)).Returns(salesReturn);
        _stock.Setup(repository => repository.Get(1, "SKU-9")).Returns((StockItem?)null);

        var service = CreateService();

        Assert.Throws<StockNotFoundException>(() => service.Authorize(21));
        Assert.Equal(0, _unitOfWork.Executions);
        Assert.Equal(SalesReturnStatus.Pending, salesReturn.Status);
        _returns.Verify(repository => repository.Update(It.IsAny<SalesReturn>()), Times.Never);
    }

    [Fact]
    public void AuthorizeRejectsAReturnThatIsNotPending()
    {
        var salesReturn = Pending(id: 21, sku: "SKU-1", quantity: 1);
        salesReturn.Reject(Date);
        _returns.Setup(repository => repository.GetById(21)).Returns(salesReturn);

        var service = CreateService();

        Assert.Throws<InvalidSalesReturnException>(() => service.Authorize(21));
        Assert.Equal(0, _unitOfWork.Executions);
    }

    [Fact]
    public void RejectClosesTheReturnWithoutTouchingTheStock()
    {
        var salesReturn = Pending(id: 21, sku: "SKU-1", quantity: 1);
        _returns.Setup(repository => repository.GetById(21)).Returns(salesReturn);

        var service = CreateService();
        service.Reject(21);

        Assert.Equal(SalesReturnStatus.Rejected, salesReturn.Status);
        Assert.Equal(Date, salesReturn.RejectedAt);
        _stock.Verify(repository => repository.Get(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _returns.Verify(repository => repository.Update(salesReturn), Times.Once);
        Assert.Equal("SalesReturnRejected", _audit.Entries[^1].Action);
    }

    [Fact]
    public void CreateByAStoreManagerForAnotherStoreIsDeniedBeforeReadingTheInvoice()
    {
        var user = new FakeCurrentUser(
            userId: 5,
            role: RoleNames.StoreManager,
            storeId: 2);

        var exception = Assert.Throws<AccessDeniedException>(
            () => CreateService(user: user).Create(Request(("SKU-1", 1))));

        Assert.Equal("inventory.store.scope", exception.Permission);
        _invoices.Verify(repository => repository.GetById(It.IsAny<int>()), Times.Never);
        _returns.Verify(repository => repository.Add(It.IsAny<SalesReturn>()), Times.Never);
        Assert.Equal(0, _unitOfWork.Executions);
    }

    [Fact]
    public void CreateForAStoreOtherThanTheInvoicingStoreIsRejected()
    {
        var invoice = new SalesInvoice(0, "Ana Torres", "1020304050", Date.AddDays(-1), storeId: 2);
        invoice.AddLine("SKU-1", "Agua 600 ml", 3, 2500m);
        TestIds.Assign(invoice, 12);

        _invoices.Setup(repository => repository.GetById(12)).Returns(invoice);
        _stores.Setup(repository => repository.GetById(1)).Returns(_store);

        var service = CreateService();

        var exception = Assert.Throws<InvalidSalesReturnException>(
            () => service.Create(Request(("SKU-1", 1))));

        Assert.Contains("restock the store", exception.Message);
        _returns.Verify(repository => repository.Add(It.IsAny<SalesReturn>()), Times.Never);
    }

    [Fact]
    public void GetAFromAnotherStoreIsDenied()
    {
        var detail = new SalesReturnDetail(
            new SalesReturnRow(
                21, 12, 1, "Tienda Centro", "Ana Torres", "1020304050",
                "Producto en mal estado", SalesReturnStatus.Pending, null, 1, 2500m,
                Date.AddDays(-1), null),
            Array.Empty<SalesReturnLineRow>());

        _query.Setup(query => query.GetById(21)).Returns(detail);
        var user = new FakeCurrentUser(
            userId: 5,
            role: RoleNames.StockClerk,
            storeId: 2);

        var exception = Assert.Throws<AccessDeniedException>(
            () => CreateService(user: user).Get(21));

        Assert.Equal("inventory.store.scope", exception.Permission);
    }

    [Fact]
    public void ListRestrictsTheFilterToTheUsersStore()
    {
        _query.Setup(query => query.List(1)).Returns(Array.Empty<SalesReturnRow>());
        var user = new FakeCurrentUser(
            userId: 5,
            role: RoleNames.StoreManager,
            storeId: 1);

        CreateService(user: user).List();

        _query.Verify(query => query.List(1), Times.Once);
        _query.Verify(query => query.List(null), Times.Never);
    }

    [Fact]
    public void ListAsksForAnotherStoreAndIsDenied()
    {
        var user = new FakeCurrentUser(
            userId: 5,
            role: RoleNames.StoreManager,
            storeId: 1);

        Assert.Throws<AccessDeniedException>(() => CreateService(user: user).List(2));
        _query.Verify(query => query.List(It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public void AuthorizeAFromAnotherStoreIsDeniedAndDoesNotTouchStock()
    {
        var salesReturn = Pending(id: 21, sku: "SKU-1", quantity: 2);
        _returns.Setup(repository => repository.GetById(21)).Returns(salesReturn);
        var user = new FakeCurrentUser(
            userId: 5,
            role: RoleNames.StoreManager,
            storeId: 2);

        Assert.Throws<AccessDeniedException>(() => CreateService(user: user).Authorize(21));

        Assert.Equal(SalesReturnStatus.Pending, salesReturn.Status);
        _stock.Verify(repository => repository.Get(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        Assert.Equal(0, _unitOfWork.Executions);
    }

    [Fact]
    public void RejectAFromAnotherStoreIsDenied()
    {
        var salesReturn = Pending(id: 21, sku: "SKU-1", quantity: 2);
        _returns.Setup(repository => repository.GetById(21)).Returns(salesReturn);
        var user = new FakeCurrentUser(
            userId: 5,
            role: RoleNames.StockClerk,
            storeId: 2);

        Assert.Throws<AccessDeniedException>(() => CreateService(user: user).Reject(21));

        Assert.Equal(SalesReturnStatus.Pending, salesReturn.Status);
        _returns.Verify(repository => repository.Update(It.IsAny<SalesReturn>()), Times.Never);
    }

    [Fact]
    public void GetThrowsWhenTheReturnDoesNotExist()
    {
        _query.Setup(query => query.GetById(99)).Returns((SalesReturnDetail?)null);
        var service = CreateService();

        Assert.Throws<SalesReturnNotFoundException>(() => service.Get(99));
    }

    [Fact]
    public void StoresListOnlyTheActiveOnes()
    {
        var inactive = new Store(2, "Tienda Norte", "Carrera 15", active: false);
        _stores.Setup(repository => repository.List()).Returns(new[] { inactive, _store });

        var service = CreateService();
        var stores = service.Stores();

        Assert.Single(stores);
        Assert.Equal("Tienda Centro", stores[0].Name);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("get")]
    [InlineData("create")]
    [InlineData("authorize")]
    [InlineData("reject")]
    [InlineData("stores")]
    [InlineData("invoices")]
    public void EveryOperationDemandsTheReturnPermission(string operation)
    {
        var service = CreateService(new AlwaysDenyAccessControl());

        Action action = operation switch
        {
            "list" => () => service.List(),
            "get" => () => service.Get(21),
            "create" => () => service.Create(Request(("SKU-1", 1))),
            "authorize" => () => service.Authorize(21),
            "reject" => () => service.Reject(21),
            "stores" => () => service.Stores(),
            _ => () => service.Invoices()
        };

        var exception = Assert.Throws<AccessDeniedException>(action);

        Assert.Equal(Permissions.SalesReturnsAuthorize, exception.Permission);
        _invoices.Verify(repository => repository.GetById(It.IsAny<int>()), Times.Never);
        _returns.Verify(repository => repository.GetById(It.IsAny<int>()), Times.Never);
        _returns.Verify(repository => repository.Add(It.IsAny<SalesReturn>()), Times.Never);
    }

    private SalesReturnService CreateService(IAccessControl? access = null, ICurrentUser? user = null) =>
        new(
            _returns.Object,
            _query.Object,
            _invoices.Object,
            _stock.Object,
            _stores.Object,
            _unitOfWork,
            access ?? new AllowAllAccessControl(),
            _audit,
            user ?? _user,
            _clock);

    private static CreateSalesReturnRequest Request(params (string Sku, int Quantity)[] lines) =>
        new(
            12,
            1,
            "Producto en mal estado",
            lines.Select(line => new SalesReturnLineRequest(line.Sku, line.Quantity)).ToList());

    private static SalesReturn Pending(int id, string sku, int quantity)
    {
        var salesReturn = new SalesReturn(
            id,
            12,
            1,
            "Ana Torres",
            "1020304050",
            "Producto en mal estado",
            Date.AddDays(-1));

        salesReturn.AddLine(sku, "Agua 600 ml", quantity, 2500m);
        return salesReturn;
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int Executions { get; private set; }

        public void ExecuteInTransaction(Action action)
        {
            Executions++;
            action();
        }
    }
}
