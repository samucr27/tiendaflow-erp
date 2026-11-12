using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class PurchaseOrderServiceTests
{
    private static readonly DateTime Date = new(2026, 11, 7, 15, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IPurchaseOrderRepository> _orders = new();
    private readonly Mock<ISupplierRepository> _suppliers = new();
    private readonly Mock<IStockQuery> _stockQuery = new();
    private readonly Mock<IStockRepository> _stockRepository = new();
    private readonly RecordingUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly FakeCurrentUser _user = new(userId: 5);

    private PurchaseOrderService CreateService(IAccessControl? access = null) =>
        new(
            _orders.Object,
            _suppliers.Object,
            _stockQuery.Object,
            new StockService(_stockRepository.Object, new AllowAllAccessControl()),
            _unitOfWork,
            access ?? new AllowAllAccessControl(),
            _audit,
            _user);

    private PurchaseOrder GivenOrder(Action<PurchaseOrder>? prepare = null)
    {
        var order = new PurchaseOrder(7, 5, 1, Date);
        order.AddLine("SKU-1", 10, 1000m);
        order.AddLine("SKU-2", 5, 500m);
        prepare?.Invoke(order);
        _orders.Setup(o => o.GetById(7)).Returns(order);
        return order;
    }

    private static void Send(PurchaseOrder order)
    {
        order.Approve();
        order.Send();
    }

    // ---- SuggestReorder ----

    [Fact]
    public void SuggestReorder_ListsOnlyLowStockRowsSortedByQuantityThenName()
    {
        _stockQuery.Setup(q => q.ListByStore(1)).Returns(new[]
        {
            new StockRow("SKU-1", "Arroz", 1, 2, 10, "A1", 5000m, null),
            new StockRow("SKU-2", "Aceite", 1, 20, 10, "A2", 9000m, null),
            new StockRow("SKU-3", "Sal", 1, 0, 5, "A3", 1500m, null),
            new StockRow("SKU-4", "Azúcar", 1, 0, 0, "A4", 3000m, null)
        });

        var suggestions = CreateService().SuggestReorder(1);

        Assert.Equal(new[] { "SKU-4", "SKU-3", "SKU-1" }, suggestions.Select(s => s.Sku));
        Assert.Equal(new[] { 1, 10, 18 }, suggestions.Select(s => s.SuggestedQuantity));
        Assert.Equal(5000m, suggestions.Single(s => s.Sku == "SKU-1").ReferenceUnitCost);
    }

    [Fact]
    public void SuggestReorder_WithoutPermission_Throws()
    {
        var exception = Assert.Throws<AccessDeniedException>(
            () => CreateRestrictedService().SuggestReorder(1));

        Assert.Equal(Permissions.PurchasingOrdersCreate, exception.Permission);
    }

    // ---- Create ----

    [Fact]
    public void Create_BuildsTheOrderWithItsLinesAndAudits()
    {
        _suppliers.Setup(r => r.GetById(5)).Returns(new Supplier(5, "900123", "Andina", "Calle 1"));
        PurchaseOrder? added = null;
        _orders.Setup(o => o.Add(It.IsAny<PurchaseOrder>())).Callback<PurchaseOrder>(o => added = o);

        CreateService().Create(new CreatePurchaseOrderRequest(
            5,
            1,
            Date,
            new[]
            {
                new CreatePurchaseOrderLine("SKU-1", 10, 1000m),
                new CreatePurchaseOrderLine("SKU-2", 5, 500m)
            }));

        Assert.NotNull(added);
        Assert.Equal(5, added!.SupplierId);
        Assert.Equal(PurchaseOrderStatus.Draft, added.Status);
        Assert.Equal(2, added.Lines.Count);
        Assert.Equal(12500m, added.Total);

        var entry = _audit.Entries.Single();
        Assert.Equal("PurchaseOrderCreated", entry.Action);
        Assert.Equal("purchasing", entry.Module);
        Assert.Equal(5, entry.UserId);
        Assert.Contains("2 líneas", entry.Detail);
    }

    [Fact]
    public void Create_WithUnknownSupplier_Throws()
    {
        Assert.Throws<SupplierNotFoundException>(() => CreateService().Create(
            new CreatePurchaseOrderRequest(99, 1, Date, new[] { new CreatePurchaseOrderLine("SKU-1", 1, 100m) })));
    }

    [Fact]
    public void Create_WithInactiveSupplier_Throws()
    {
        _suppliers.Setup(r => r.GetById(5))
            .Returns(new Supplier(5, "900123", "Andina", "Calle 1", active: false));

        Assert.Throws<InvalidPurchaseOrderException>(() => CreateService().Create(
            new CreatePurchaseOrderRequest(5, 1, Date, new[] { new CreatePurchaseOrderLine("SKU-1", 1, 100m) })));

        _orders.Verify(o => o.Add(It.IsAny<PurchaseOrder>()), Times.Never);
    }

    [Fact]
    public void Create_WithoutLines_Throws()
    {
        _suppliers.Setup(r => r.GetById(5)).Returns(new Supplier(5, "900123", "Andina", "Calle 1"));

        Assert.Throws<InvalidPurchaseOrderException>(() => CreateService().Create(
            new CreatePurchaseOrderRequest(5, 1, Date, Array.Empty<CreatePurchaseOrderLine>())));

        _orders.Verify(o => o.Add(It.IsAny<PurchaseOrder>()), Times.Never);
    }

    [Fact]
    public void Create_WithAnInvalidLine_ThrowsAndDoesNotSave()
    {
        _suppliers.Setup(r => r.GetById(5)).Returns(new Supplier(5, "900123", "Andina", "Calle 1"));

        Assert.Throws<InvalidPurchaseOrderException>(() => CreateService().Create(
            new CreatePurchaseOrderRequest(5, 1, Date, new[] { new CreatePurchaseOrderLine("SKU-1", 0, 100m) })));

        _orders.Verify(o => o.Add(It.IsAny<PurchaseOrder>()), Times.Never);
    }

    // ---- Status transitions ----

    [Fact]
    public void SubmitForApproval_MovesTheOrderAndAudits()
    {
        var order = GivenOrder();

        CreateService().SubmitForApproval(7);

        Assert.Equal(PurchaseOrderStatus.PendingApproval, order.Status);
        _orders.Verify(o => o.Update(order), Times.Once);
        Assert.Equal("PurchaseOrderSubmitted", _audit.Entries.Single().Action);
    }

    [Fact]
    public void Approve_MovesTheOrderAndAudits()
    {
        var order = GivenOrder(o => o.SubmitForApproval());

        CreateService().Approve(7);

        Assert.Equal(PurchaseOrderStatus.Approved, order.Status);
        var entry = _audit.Entries.Single();
        Assert.Equal("PurchaseOrderApproved", entry.Action);
        Assert.Equal("7", entry.EntityId);
    }

    [Fact]
    public void Send_MovesTheOrderAndAudits()
    {
        var order = GivenOrder(o => o.Approve());

        CreateService().Send(7);

        Assert.Equal(PurchaseOrderStatus.Sent, order.Status);
        Assert.Equal("PurchaseOrderSent", _audit.Entries.Single().Action);
    }

    [Fact]
    public void Confirm_MovesTheOrderAndAudits()
    {
        var order = GivenOrder(Send);

        CreateService().Confirm(7);

        Assert.Equal(PurchaseOrderStatus.Confirmed, order.Status);
        Assert.Equal("PurchaseOrderConfirmed", _audit.Entries.Single().Action);
    }

    [Fact]
    public void Confirm_WhenTheOrderWasNotSent_ThrowsAndDoesNotSave()
    {
        GivenOrder(o => o.Approve());

        Assert.Throws<InvalidPurchaseOrderException>(() => CreateService().Confirm(7));

        _orders.Verify(o => o.Update(It.IsAny<PurchaseOrder>()), Times.Never);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Cancel_MovesTheOrderAndAudits()
    {
        var order = GivenOrder();

        CreateService().Cancel(7);

        Assert.Equal(PurchaseOrderStatus.Cancelled, order.Status);
        Assert.Equal("PurchaseOrderCancelled", _audit.Entries.Single().Action);
    }

    [Fact]
    public void Send_WhenTheOrderIsNotApproved_ThrowsAndDoesNotSave()
    {
        GivenOrder();

        Assert.Throws<InvalidPurchaseOrderException>(() => CreateService().Send(7));

        _orders.Verify(o => o.Update(It.IsAny<PurchaseOrder>()), Times.Never);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Approve_WithUnknownOrder_Throws()
    {
        Assert.Throws<PurchaseOrderNotFoundException>(() => CreateService().Approve(99));
    }

    [Fact]
    public void Approve_WithoutTheApprovePermission_Throws()
    {
        var access = new AccessControl(
            new FakeCurrentUser(
                userId: 5,
                permissions: new HashSet<string> { Permissions.PurchasingOrdersCreate }),
            _audit);
        GivenOrder(o => o.SubmitForApproval());

        var exception = Assert.Throws<AccessDeniedException>(() => CreateService(access).Approve(7));

        Assert.Equal(Permissions.PurchasingOrdersApprove, exception.Permission);
        _orders.Verify(o => o.Update(It.IsAny<PurchaseOrder>()), Times.Never);
    }

    // ---- Receive ----

    [Fact]
    public void Receive_Partially_IncreasesStockAndUpdatesTheOrder()
    {
        var order = GivenOrder(Send);
        var stock = new StockItem("SKU-1", 1, 5, "Aisle 3");
        _stockRepository.Setup(r => r.Get(1, "SKU-1")).Returns(stock);

        CreateService().Receive(7, new[] { new ReceivePurchaseOrderLine("SKU-1", 4) }, Date);

        Assert.Equal(9, stock.QuantityAvailable);
        Assert.Equal(PurchaseOrderStatus.PartiallyReceived, order.Status);
        Assert.Equal(1, _unitOfWork.Executions);
        _orders.Verify(o => o.Update(order), Times.Once);

        var entry = _audit.Entries.Single();
        Assert.Equal("PurchaseOrderReceived", entry.Action);
        Assert.Contains("4 unidades", entry.Detail);
    }

    [Fact]
    public void Receive_EveryLine_MarksTheOrderAsReceived()
    {
        var order = GivenOrder(Send);
        _stockRepository.Setup(r => r.Get(1, "SKU-1")).Returns(new StockItem("SKU-1", 1, 0, "A"));
        _stockRepository.Setup(r => r.Get(1, "SKU-2")).Returns(new StockItem("SKU-2", 1, 0, "A"));

        CreateService().Receive(
            7,
            new[]
            {
                new ReceivePurchaseOrderLine("SKU-1", 10),
                new ReceivePurchaseOrderLine("SKU-2", 5)
            },
            Date);

        Assert.Equal(PurchaseOrderStatus.Received, order.Status);
        Assert.Contains("15 unidades", _audit.Entries.Single().Detail);
    }

    [Fact]
    public void Receive_WhenTheProductHasNoStockRow_CreatesIt()
    {
        GivenOrder(Send);

        CreateService().Receive(7, new[] { new ReceivePurchaseOrderLine("SKU-1", 4) }, Date);

        _stockRepository.Verify(
            r => r.Add(It.Is<StockItem>(item =>
                item.Sku == "SKU-1"
                && item.StoreId == 1
                && item.QuantityAvailable == 4
                && item.Location == "Recepción")),
            Times.Once);
    }

    [Fact]
    public void Receive_WithoutLines_Throws()
    {
        GivenOrder(Send);

        Assert.Throws<InvalidPurchaseOrderException>(
            () => CreateService().Receive(7, Array.Empty<ReceivePurchaseOrderLine>(), Date));

        Assert.Equal(0, _unitOfWork.Executions);
    }

    [Fact]
    public void Receive_WithUnknownOrder_Throws()
    {
        Assert.Throws<PurchaseOrderNotFoundException>(() => CreateService().Receive(
            99,
            new[] { new ReceivePurchaseOrderLine("SKU-1", 1) },
            Date));
    }

    [Fact]
    public void Receive_WhenTheOrderWasNotSent_ThrowsAndTouchesNothing()
    {
        GivenOrder();

        Assert.Throws<InvalidPurchaseOrderException>(() => CreateService().Receive(
            7,
            new[] { new ReceivePurchaseOrderLine("SKU-1", 1) },
            Date));

        _stockRepository.Verify(r => r.Update(It.IsAny<StockItem>()), Times.Never);
        _stockRepository.Verify(r => r.Add(It.IsAny<StockItem>()), Times.Never);
        _orders.Verify(o => o.Update(It.IsAny<PurchaseOrder>()), Times.Never);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Receive_WithoutThePermission_Throws()
    {
        GivenOrder(Send);

        var exception = Assert.Throws<AccessDeniedException>(() => CreateRestrictedService().Receive(
            7,
            new[] { new ReceivePurchaseOrderLine("SKU-1", 1) },
            Date));

        Assert.Equal(Permissions.PurchasingOrdersReceive, exception.Permission);
    }

    private PurchaseOrderService CreateRestrictedService() =>
        CreateService(new AccessControl(
            new FakeCurrentUser(userId: 5, permissions: new HashSet<string>()),
            _audit));

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