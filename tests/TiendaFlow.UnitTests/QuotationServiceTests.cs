using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure;
using Xunit;

namespace TiendaFlow.UnitTests;

public class QuotationRequestTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void New_IsPendingAndComputesTheEstimatedAmount()
    {
        var item = new QuotationRequest(5, " SKU-1 ", 10, 1200m, Now, new DateOnly(2026, 12, 10), " urgente ");

        Assert.Equal(QuotationStatus.Pending, item.Status);
        Assert.Equal("SKU-1", item.ProductSku);
        Assert.Equal(12000m, item.EstimatedAmount);
        Assert.Equal("urgente", item.Notes);
        Assert.True(item.CanChange);
        Assert.True(item.CanDelete);
    }

    [Theory]
    [InlineData(0, "SKU-1", 1, 100, "A supplier is required.")]
    [InlineData(5, " ", 1, 100, "A product is required.")]
    [InlineData(5, "SKU-1", 0, 100, "The quantity must be greater than zero.")]
    [InlineData(5, "SKU-1", 1, 0, "The quoted unit price must be greater than zero.")]
    public void New_RejectsInvalidData(int supplierId, string sku, int quantity, int price, string message)
    {
        var exception = Assert.Throws<InvalidQuotationException>(
            () => new QuotationRequest(supplierId, sku, quantity, price, Now));

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void Approve_RecordsWhoDecidedAndBlocksEditsAndDeletion()
    {
        var item = new QuotationRequest(5, "SKU-1", 10, 1200m, Now);

        item.Approve(7, "Carlos Ramírez", Now);

        Assert.Equal(QuotationStatus.Approved, item.Status);
        Assert.Equal(7, item.DecidedByUserId);
        Assert.Equal("Carlos Ramírez", item.DecidedByName);
        Assert.False(item.CanChange);
        Assert.False(item.CanDelete);
        Assert.Throws<InvalidQuotationException>(() => item.Update(5, "SKU-1", 2, 100m, null, null));
        Assert.Throws<InvalidQuotationException>(() => item.EnsureCanDelete());
    }

    [Fact]
    public void Reject_NeedsAReasonAndKeepsTheQuotationDeletable()
    {
        var item = new QuotationRequest(5, "SKU-1", 10, 1200m, Now);

        Assert.Throws<InvalidQuotationException>(() => item.Reject(7, "Carlos", " ", Now));
        item.Reject(7, "Carlos", "Precio alto", Now);

        Assert.Equal(QuotationStatus.Rejected, item.Status);
        Assert.Equal("Precio alto", item.RejectionReason);
        Assert.True(item.CanDelete);
        Assert.Throws<InvalidQuotationException>(() => item.Approve(7, "Carlos", Now));
    }

    [Fact]
    public void MarkOrdered_OnlyFromApproved()
    {
        var item = new QuotationRequest(5, "SKU-1", 10, 1200m, Now);

        Assert.Throws<InvalidQuotationException>(() => item.MarkOrdered(3));
        item.Approve(7, "Carlos", Now);
        item.MarkOrdered(3);

        Assert.Equal(QuotationStatus.Ordered, item.Status);
        Assert.Equal(3, item.PurchaseOrderId);
    }
}

public class QuotationServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 15, 0, 0, DateTimeKind.Utc);

    private readonly InMemoryEntityStore<QuotationRequest> _store = new();
    private readonly Mock<ISupplierRepository> _suppliers = new();
    private readonly Mock<IProductCatalogQuery> _products = new();
    private readonly Mock<IPurchaseOrderRepository> _orders = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly CountingUnitOfWork _unitOfWork = new();

    public QuotationServiceTests()
    {
        _suppliers.Setup(r => r.GetById(5)).Returns(new Supplier(5, "900123", "Andina", "Calle 1"));
        var inactive = new Supplier(6, "900456", "Cerrada", "Calle 2");
        inactive.Deactivate();
        _suppliers.Setup(r => r.GetById(6)).Returns(inactive);
        _suppliers.Setup(r => r.List()).Returns(new[] { new Supplier(5, "900123", "Andina", "Calle 1") });
        _products.Setup(q => q.GetBySku("SKU-1")).Returns(new ProductListItem("SKU-1", "770", "Arroz 500 g", 3300m));
        _orders.Setup(o => o.Add(It.IsAny<PurchaseOrder>()))
            .Callback<PurchaseOrder>(order => TestIds.Assign(order, 41));
    }

    private QuotationService CreateService(IReadOnlySet<string>? permissions = null)
    {
        var user = new FakeCurrentUser(userId: 9, permissions: permissions);
        var access = permissions is null ? (IAccessControl)new AllowAllAccessControl() : new AccessControl(user, _audit);
        var orders = new PurchaseOrderService(
            _orders.Object,
            _suppliers.Object,
            new Mock<IProductSupplierRepository>().Object,
            new Mock<IStockQuery>().Object,
            new StockService(new Mock<IStockRepository>().Object, new AllowAllAccessControl()),
            _unitOfWork,
            new AllowAllAccessControl(),
            _audit,
            user);

        return new QuotationService(_store, _suppliers.Object, _products.Object, orders, _unitOfWork,
            access, _audit, user, new FixedClock(Now));
    }

    private static QuotationInput Input(int supplierId = 5, string sku = "SKU-1", int quantity = 10, decimal price = 1200m) =>
        new(supplierId, sku, quantity, price, new DateOnly(2026, 12, 10), "Entrega en Chía");

    [Fact]
    public void Create_StoresAPendingRequestAndAudits()
    {
        var service = CreateService();

        var id = service.Create(Input());
        var row = service.Get(id);

        Assert.Equal("COT-00001", row.Code);
        Assert.Equal("Andina", row.SupplierName);
        Assert.Equal("Arroz 500 g", row.ProductName);
        Assert.Equal(12000m, row.EstimatedAmount);
        Assert.Equal(QuotationStatus.Pending, row.Status);
        Assert.Equal("QuotationCreated", _audit.Entries.Single().Action);
        Assert.Equal("purchasing", _audit.Entries.Single().Module);
    }

    [Fact]
    public void Create_RejectsInactiveSupplierUnknownProductAndMissingSupplier()
    {
        var service = CreateService();

        Assert.Throws<InvalidQuotationException>(() => service.Create(Input(supplierId: 6)));
        Assert.Throws<InvalidQuotationException>(() => service.Create(Input(sku: "NOPE")));
        Assert.Throws<SupplierNotFoundException>(() => service.Create(Input(supplierId: 99)));
    }

    [Fact]
    public void Update_ChangesAPendingRequest()
    {
        var service = CreateService();
        var id = service.Create(Input());

        service.Update(id, Input(quantity: 20, price: 1100m));

        Assert.Equal(22000m, service.Get(id).EstimatedAmount);
    }

    [Fact]
    public void Delete_RemovesPendingOrRejectedButNotApproved()
    {
        var service = CreateService();
        var pending = service.Create(Input());
        var approved = service.Create(Input());
        service.Approve(approved);

        service.Delete(pending);

        Assert.Single(service.List());
        Assert.Throws<InvalidQuotationException>(() => service.Delete(approved));
        Assert.Throws<QuotationNotFoundException>(() => service.Get(pending));
    }

    [Fact]
    public void ApproveAndReject_RecordTheDecider()
    {
        var service = CreateService();
        var first = service.Create(Input());
        var second = service.Create(Input());

        service.Approve(first);
        service.Reject(second, "Precio por encima del mercado");

        Assert.Equal("Usuario de prueba", service.Get(first).DecidedByName);
        Assert.Equal(QuotationStatus.Rejected, service.Get(second).Status);
        Assert.Equal("Precio por encima del mercado", service.Get(second).RejectionReason);
    }

    [Fact]
    public void GenerateOrder_CreatesADraftOrderAtTheQuotedPriceInOneTransaction()
    {
        var service = CreateService();
        var id = service.Create(Input());
        service.Approve(id);
        PurchaseOrder? created = null;
        _orders.Setup(o => o.Add(It.IsAny<PurchaseOrder>()))
            .Callback<PurchaseOrder>(order => { TestIds.Assign(order, 41); created = order; });

        var orderId = service.GenerateOrder(id, storeId: 1);

        Assert.Equal(41, orderId);
        Assert.NotNull(created);
        Assert.Equal(5, created!.SupplierId);
        Assert.Equal(PurchaseOrderStatus.Draft, created.Status);
        Assert.Equal(12000m, created.Total);
        Assert.Equal(1, _unitOfWork.Executions);
        var row = service.Get(id);
        Assert.Equal(QuotationStatus.Ordered, row.Status);
        Assert.Equal(41, row.PurchaseOrderId);
    }

    [Fact]
    public void GenerateOrder_NeedsAnApprovedQuotationAndAStore()
    {
        var service = CreateService();
        var id = service.Create(Input());

        Assert.Throws<InvalidQuotationException>(() => service.GenerateOrder(id, storeId: 1));
        service.Approve(id);
        Assert.Throws<InvalidQuotationException>(() => service.GenerateOrder(id, storeId: 0));
    }

    [Fact]
    public void Permissions_BuyerCannotApproveAndReaderCannotCreate()
    {
        var buyer = CreateService(new HashSet<string> { Permissions.PurchasingOrdersView, Permissions.PurchasingOrdersCreate });
        var id = buyer.Create(Input());

        var denied = Assert.Throws<AccessDeniedException>(() => buyer.Approve(id));
        Assert.Equal(Permissions.PurchasingOrdersApprove, denied.Permission);

        var reader = CreateService(new HashSet<string> { Permissions.PurchasingOrdersView });
        Assert.Throws<AccessDeniedException>(() => reader.Create(Input()));
        Assert.Single(reader.List());
    }

    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        public int Executions { get; private set; }

        public void ExecuteInTransaction(Action action)
        {
            Executions++;
            action();
        }
    }
}

public class DatabaseConnectionTests
{
    [Fact]
    public void EnvironmentVariableWinsOverTheConnectionStringSetting() =>
        Assert.Equal("Server=env", DatabaseConnection.Resolve(" Server=env ", "Server=settings"));

    [Fact]
    public void FallsBackToTheConnectionStringSetting() =>
        Assert.Equal("Server=settings", DatabaseConnection.Resolve(null, "Server=settings"));

    [Fact]
    public void ReturnsNullWhenNothingIsConfigured() =>
        Assert.Null(DatabaseConnection.Resolve(" ", ""));
}
