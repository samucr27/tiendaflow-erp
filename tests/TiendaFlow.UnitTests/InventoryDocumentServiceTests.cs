using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class InventoryCountServiceTests
{
    private static readonly DateTime Date = new(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IInventoryCountRepository> _counts = new();
    private readonly Mock<IStockRepository> _stock = new();
    private readonly Mock<IStockQuery> _stockQuery = new();
    private readonly Mock<IStoreRepository> _stores = new();
    private readonly RecordingUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly FakeCurrentUser _user = new(userId: 5, ip: "10.0.0.7");
    private readonly FixedClock _clock = new(Date);

    private InventoryCountService CreateService(IAccessControl? access = null) =>
        new(
            _counts.Object,
            _stock.Object,
            _stockQuery.Object,
            _stores.Object,
            _unitOfWork,
            access ?? new AllowAllAccessControl(),
            _audit,
            _user,
            _clock);

    private static StockRow Row(
        string sku,
        string name,
        int quantity,
        string location = "Bodega") =>
        new(sku, name, 1, quantity, 0, location, 1000m, null);

    private InventoryCount GivenCount(
        Action<InventoryCount>? prepare = null,
        int storeId = 1)
    {
        var count = new InventoryCount(9, storeId, "Bodega", Date);
        count.AddLine("SKU-1", 10);
        count.AddLine("SKU-2", 5);
        prepare?.Invoke(count);
        _counts.Setup(repository => repository.GetById(9)).Returns(count);
        return count;
    }

    // ---- Stores ----

    [Fact]
    public void Stores_ReturnsOnlyActiveStoresSortedByName()
    {
        _stores.Setup(repository => repository.List()).Returns(new[]
        {
            new Store(2, "Tienda Norte", "Calle 2"),
            new Store(1, "Tienda Centro", "Calle 1"),
            new Store(3, "Tienda Cerrada", "Calle 3", active: false)
        });

        var stores = CreateService().Stores();

        Assert.Equal(new[] { 1, 2 }, stores.Select(store => store.Id));
    }

    [Fact]
    public void Stores_WithoutPermission_ThrowsAndDoesNotQuery()
    {
        var exception = Assert.Throws<AccessDeniedException>(() => Restricted().Stores());

        Assert.Equal(Permissions.InventoryStockCount, exception.Permission);
        _stores.Verify(repository => repository.List(), Times.Never);
    }

    // ---- Create ----

    [Fact]
    public void Create_SnapshotsTheStockRowsAndAudits()
    {
        _stores.Setup(repository => repository.GetById(1))
            .Returns(new Store(1, "Tienda Centro", "Calle 1"));
        _stockQuery.Setup(query => query.ListByStore(1)).Returns(new[]
        {
            Row("SKU-2", "Arroz", 8),
            Row("SKU-1", "Aceite", 10)
        });

        InventoryCount? added = null;
        _counts.Setup(repository => repository.Add(It.IsAny<InventoryCount>()))
            .Callback<InventoryCount>(count =>
            {
                added = count;
                TestIds.Assign(count, 42);
            });

        var id = CreateService().Create(1, "  Bodega  ");

        Assert.Equal(42, id);
        Assert.NotNull(added);
        Assert.Equal("Bodega", added!.Location);
        Assert.Equal(new[] { "SKU-1", "SKU-2" }, added.Lines.Select(line => line.ProductSku));
        Assert.Equal(new[] { 10, 8 }, added.Lines.Select(line => line.SystemQuantity));
        Assert.Equal(1, _unitOfWork.Executions);

        var entry = _audit.Entries.Single();
        Assert.Equal("InventoryCountOpened", entry.Action);
        Assert.Equal("inventory", entry.Module);
        Assert.Equal(5, entry.UserId);
        Assert.Equal("10.0.0.7", entry.Ip);
        Assert.Contains("2 productos", entry.Detail);
    }

    [Fact]
    public void Create_FiltersByLocationIgnoringCase()
    {
        _stores.Setup(repository => repository.GetById(1))
            .Returns(new Store(1, "Tienda Centro", "Calle 1"));
        _stockQuery.Setup(query => query.ListByStore(1)).Returns(new[]
        {
            Row("SKU-1", "Aceite", 10, "Bodega"),
            Row("SKU-2", "Arroz", 8, "Pasillo 2")
        });

        CreateService().Create(1, "bodega");

        _counts.Verify(repository => repository.Add(It.Is<InventoryCount>(count =>
            count.Lines.Count == 1 && count.Lines[0].ProductSku == "SKU-1")), Times.Once);
    }

    [Fact]
    public void Create_WithoutPermission_ThrowsBeforeReadingRepositories()
    {
        var exception = Assert.Throws<AccessDeniedException>(() => Restricted().Create(1, null));

        Assert.Equal(Permissions.InventoryStockCount, exception.Permission);
        _stores.Verify(repository => repository.GetById(It.IsAny<int>()), Times.Never);
        _stockQuery.Verify(query => query.ListByStore(It.IsAny<int>()), Times.Never);
        _counts.Verify(repository => repository.Add(It.IsAny<InventoryCount>()), Times.Never);
    }

    [Fact]
    public void Create_WithUnknownStore_Throws()
    {
        Assert.Throws<StoreNotFoundException>(() => CreateService().Create(99, null));
    }

    [Fact]
    public void Create_WithAnInactiveStore_Throws()
    {
        _stores.Setup(repository => repository.GetById(1))
            .Returns(new Store(1, "Tienda Centro", "Calle 1", active: false));

        Assert.Throws<InvalidInventoryCountException>(() => CreateService().Create(1, null));
        _counts.Verify(repository => repository.Add(It.IsAny<InventoryCount>()), Times.Never);
    }

    [Fact]
    public void Create_WithoutStockRows_Throws()
    {
        _stores.Setup(repository => repository.GetById(1))
            .Returns(new Store(1, "Tienda Centro", "Calle 1"));
        _stockQuery.Setup(query => query.ListByStore(1)).Returns(Array.Empty<StockRow>());

        Assert.Throws<InvalidInventoryCountException>(() => CreateService().Create(1, null));
        Assert.Equal(0, _unitOfWork.Executions);
    }

    // ---- SetCounted ----

    [Fact]
    public void SetCounted_UpdatesTheDocument()
    {
        var count = GivenCount();

        CreateService().SetCounted(9, "SKU-1", 7);

        Assert.Equal(7, count.Lines.Single(line => line.ProductSku == "SKU-1").CountedQuantity);
        _counts.Verify(repository => repository.Update(count), Times.Once);
    }

    [Fact]
    public void SetCounted_WithAWholeSheet_UpdatesEveryLine()
    {
        var count = GivenCount();

        CreateService().SetCounted(9, new Dictionary<string, int>
        {
            ["SKU-1"] = 8,
            ["SKU-2"] = 5
        });

        Assert.Equal(8, count.Lines[0].CountedQuantity);
        Assert.Equal(5, count.Lines[1].CountedQuantity);
    }

    [Fact]
    public void SetCounted_WithAnInvalidSheet_DoesNotMutateOrSave()
    {
        var count = GivenCount();

        Assert.Throws<InvalidInventoryCountException>(() => CreateService().SetCounted(
            9,
            new Dictionary<string, int>
            {
                ["SKU-1"] = 8,
                ["SKU-404"] = 1
            }));

        Assert.Null(count.Lines.Single(line => line.ProductSku == "SKU-1").CountedQuantity);
        _counts.Verify(repository => repository.Update(It.IsAny<InventoryCount>()), Times.Never);
    }

    [Fact]
    public void SetCounted_WithAnEmptySheet_Throws()
    {
        GivenCount();

        Assert.Throws<InvalidInventoryCountException>(() => CreateService().SetCounted(
            9,
            new Dictionary<string, int>()));
    }

    [Fact]
    public void SetCounted_WithAnUnknownCount_Throws()
    {
        Assert.Throws<InventoryCountNotFoundException>(
            () => CreateService().SetCounted(99, "SKU-1", 1));
    }

    [Fact]
    public void SetCounted_WithoutPermission_ThrowsAndDoesNotRead()
    {
        var exception = Assert.Throws<AccessDeniedException>(
            () => Restricted().SetCounted(9, "SKU-1", 1));

        Assert.Equal(Permissions.InventoryStockCount, exception.Permission);
        _counts.Verify(repository => repository.GetById(It.IsAny<int>()), Times.Never);
    }

    // ---- Close ----

    [Fact]
    public void Close_MovesTheCountAndAudits()
    {
        var count = GivenCount(item =>
        {
            item.SetCounted("SKU-1", 10);
            item.SetCounted("SKU-2", 4);
        });

        CreateService().Close(9);

        Assert.Equal(InventoryCountStatus.Closed, count.Status);
        Assert.Equal(1, _unitOfWork.Executions);
        _counts.Verify(repository => repository.Update(count), Times.Once);

        var entry = _audit.Entries.Single();
        Assert.Equal("InventoryCountClosed", entry.Action);
        Assert.Contains("1 con diferencia", entry.Detail);
    }

    [Fact]
    public void Close_WithUncountedLines_ThrowsAndDoesNotSave()
    {
        GivenCount();

        Assert.Throws<InvalidInventoryCountException>(() => CreateService().Close(9));

        _counts.Verify(repository => repository.Update(It.IsAny<InventoryCount>()), Times.Never);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Close_WithAnUnknownCount_Throws()
    {
        Assert.Throws<InventoryCountNotFoundException>(() => CreateService().Close(99));
    }

    // ---- Adjust ----

    [Fact]
    public void Adjust_RequiresAClosedCount()
    {
        GivenCount();

        Assert.Throws<InvalidInventoryCountException>(() => CreateService().Adjust(9));

        _stock.Verify(repository => repository.Get(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Adjust_WhenAStockRowIsMissing_ThrowsBeforeTouchingAnyStock()
    {
        GivenCount(item =>
        {
            item.SetCounted("SKU-1", 10);
            item.SetCounted("SKU-2", 5);
            item.Close(Date);
        });
        _stock.Setup(repository => repository.Get(1, "SKU-1"))
            .Returns(new StockItem("SKU-1", 1, 10, "Bodega"));
        _stock.Setup(repository => repository.Get(1, "SKU-2")).Returns((StockItem?)null);

        Assert.Throws<StockNotFoundException>(() => CreateService().Adjust(9));

        _stock.Verify(repository => repository.Update(It.IsAny<StockItem>()), Times.Never);
        Assert.Equal(0, _unitOfWork.Executions);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Adjust_SetsEveryStockRowToTheCountedQuantityAndAudits()
    {
        var count = GivenCount(item =>
        {
            item.SetCounted("SKU-1", 10);
            item.SetCounted("SKU-2", 7);
            item.Close(Date);
        });
        var first = new StockItem("SKU-1", 1, 10, "Bodega");
        var second = new StockItem("SKU-2", 1, 5, "Bodega");
        _stock.Setup(repository => repository.Get(1, "SKU-1")).Returns(first);
        _stock.Setup(repository => repository.Get(1, "SKU-2")).Returns(second);

        CreateService().Adjust(9);

        Assert.Equal(10, first.QuantityAvailable);
        Assert.Equal(7, second.QuantityAvailable);
        Assert.Equal(new InventoryMovement(MovementType.Adjustment, 2, Date), second.LastMovement);
        Assert.Equal(InventoryCountStatus.Adjusted, count.Status);
        Assert.Equal(1, _unitOfWork.Executions);
        _stock.Verify(repository => repository.Update(first), Times.Once);
        _stock.Verify(repository => repository.Update(second), Times.Once);

        var entry = _audit.Entries.Single();
        Assert.Equal("InventoryCountAdjusted", entry.Action);
        Assert.Contains("SKU-2 +2", entry.Detail);
        Assert.Contains("diferencia neta +2", entry.Detail);
    }

    [Fact]
    public void Adjust_WithoutDifferences_AuditsTheZeroNet()
    {
        GivenCount(item =>
        {
            item.SetCounted("SKU-1", 10);
            item.SetCounted("SKU-2", 5);
            item.Close(Date);
        });
        _stock.Setup(repository => repository.Get(1, "SKU-1"))
            .Returns(new StockItem("SKU-1", 1, 10, "Bodega"));
        _stock.Setup(repository => repository.Get(1, "SKU-2"))
            .Returns(new StockItem("SKU-2", 1, 5, "Bodega"));

        CreateService().Adjust(9);

        Assert.Contains("sin diferencias", _audit.Entries.Single().Detail);
    }

    [Fact]
    public void Adjust_Twice_ThrowsAndKeepsTheStock()
    {
        GivenCount(item =>
        {
            item.SetCounted("SKU-1", 10);
            item.SetCounted("SKU-2", 5);
            item.Close(Date);
            item.Adjust(Date);
        });
        _stock.Setup(repository => repository.Get(1, "SKU-1"))
            .Returns(new StockItem("SKU-1", 1, 10, "Bodega"));
        _stock.Setup(repository => repository.Get(1, "SKU-2"))
            .Returns(new StockItem("SKU-2", 1, 5, "Bodega"));

        Assert.Throws<InvalidInventoryCountException>(() => CreateService().Adjust(9));

        Assert.Equal(0, _unitOfWork.Executions);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Adjust_WithoutPermission_ThrowsAndDoesNotRead()
    {
        var exception = Assert.Throws<AccessDeniedException>(() => Restricted().Adjust(9));

        Assert.Equal(Permissions.InventoryStockCount, exception.Permission);
        _counts.Verify(repository => repository.GetById(It.IsAny<int>()), Times.Never);
    }

    private InventoryCountService Restricted() =>
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

public class InventoryTransferServiceTests
{
    private static readonly DateTime Date = new(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IInventoryTransferRepository> _transfers = new();
    private readonly Mock<IStockRepository> _stock = new();
    private readonly Mock<IStoreRepository> _stores = new();
    private readonly RecordingUnitOfWork _unitOfWork = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly FakeCurrentUser _user = new(userId: 5, ip: "10.0.0.7");
    private readonly FixedClock _clock = new(Date);

    private InventoryTransferService CreateService(IAccessControl? access = null) =>
        new(
            _transfers.Object,
            _stock.Object,
            _stores.Object,
            _unitOfWork,
            access ?? new AllowAllAccessControl(),
            _audit,
            _user,
            _clock);

    private static InventoryTransfer GivenTransfer(Action<InventoryTransfer>? prepare = null)
    {
        var transfer = new InventoryTransfer(9, 1, 2, Date);
        transfer.AddLine("SKU-1", 4);
        transfer.AddLine("SKU-2", 2);
        prepare?.Invoke(transfer);
        return transfer;
    }

    private void SetupTransfer(InventoryTransfer transfer) =>
        _transfers.Setup(repository => repository.GetById(9)).Returns(transfer);

    private void SetupStores()
    {
        _stores.Setup(repository => repository.GetById(1))
            .Returns(new Store(1, "Tienda Centro", "Calle 1"));
        _stores.Setup(repository => repository.GetById(2))
            .Returns(new Store(2, "Tienda Norte", "Calle 2"));
    }

    // ---- Stores ----

    [Fact]
    public void Stores_ReturnsOnlyActiveStoresSortedByName()
    {
        _stores.Setup(repository => repository.List()).Returns(new[]
        {
            new Store(2, "Tienda Norte", "Calle 2"),
            new Store(1, "Tienda Centro", "Calle 1"),
            new Store(3, "Tienda Cerrada", "Calle 3", active: false)
        });

        var stores = CreateService().Stores();

        Assert.Equal(new[] { 1, 2 }, stores.Select(store => store.Id));
    }

    [Fact]
    public void Stores_WithoutPermission_ThrowsAndDoesNotQuery()
    {
        var exception = Assert.Throws<AccessDeniedException>(() => Restricted().Stores());

        Assert.Equal(Permissions.InventoryStockTransfer, exception.Permission);
        _stores.Verify(repository => repository.List(), Times.Never);
    }

    // ---- Create ----

    [Fact]
    public void Create_BuildsADraftAndAudits()
    {
        SetupStores();
        InventoryTransfer? added = null;
        _transfers.Setup(repository => repository.Add(It.IsAny<InventoryTransfer>()))
            .Callback<InventoryTransfer>(transfer =>
            {
                added = transfer;
                TestIds.Assign(transfer, 21);
            });

        var id = CreateService().Create(new CreateInventoryTransferRequest(
            1,
            2,
            new[]
            {
                new CreateInventoryTransferLine("SKU-1", 4),
                new CreateInventoryTransferLine("SKU-2", 2)
            }));

        Assert.Equal(21, id);
        Assert.NotNull(added);
        Assert.Equal(InventoryTransferStatus.Draft, added!.Status);
        Assert.Equal(6, added.TotalUnits);
        Assert.Equal(1, _unitOfWork.Executions);

        var entry = _audit.Entries.Single();
        Assert.Equal("InventoryTransferCreated", entry.Action);
        Assert.Contains("6 unidades", entry.Detail);
        Assert.Equal("inventory", entry.Module);
        Assert.Equal("10.0.0.7", entry.Ip);
    }

    [Fact]
    public void Create_WithoutPermission_ThrowsBeforeReadingRepositories()
    {
        var exception = Assert.Throws<AccessDeniedException>(() => Restricted().Create(
            new CreateInventoryTransferRequest(
                1,
                2,
                new[] { new CreateInventoryTransferLine("SKU-1", 1) })));

        Assert.Equal(Permissions.InventoryStockTransfer, exception.Permission);
        _stores.Verify(repository => repository.GetById(It.IsAny<int>()), Times.Never);
        _transfers.Verify(repository => repository.Add(It.IsAny<InventoryTransfer>()), Times.Never);
    }

    [Fact]
    public void Create_WithAnUnknownOrigin_Throws()
    {
        _stores.Setup(repository => repository.GetById(1)).Returns((Store?)null);

        Assert.Throws<StoreNotFoundException>(() => CreateService().Create(
            new CreateInventoryTransferRequest(
                1,
                2,
                new[] { new CreateInventoryTransferLine("SKU-1", 1) })));
    }

    [Fact]
    public void Create_WithAnUnknownDestination_Throws()
    {
        _stores.Setup(repository => repository.GetById(1))
            .Returns(new Store(1, "Tienda Centro", "Calle 1"));
        _stores.Setup(repository => repository.GetById(2)).Returns((Store?)null);

        Assert.Throws<StoreNotFoundException>(() => CreateService().Create(
            new CreateInventoryTransferRequest(
                1,
                2,
                new[] { new CreateInventoryTransferLine("SKU-1", 1) })));
    }

    [Fact]
    public void Create_WithAnInactiveStore_Throws()
    {
        _stores.Setup(repository => repository.GetById(1))
            .Returns(new Store(1, "Tienda Centro", "Calle 1"));
        _stores.Setup(repository => repository.GetById(2))
            .Returns(new Store(2, "Tienda Norte", "Calle 2", active: false));

        Assert.Throws<InvalidInventoryTransferException>(() => CreateService().Create(
            new CreateInventoryTransferRequest(
                1,
                2,
                new[] { new CreateInventoryTransferLine("SKU-1", 1) })));
    }

    [Fact]
    public void Create_WithoutLines_Throws()
    {
        SetupStores();

        Assert.Throws<InvalidInventoryTransferException>(() => CreateService().Create(
            new CreateInventoryTransferRequest(
                1,
                2,
                Array.Empty<CreateInventoryTransferLine>())));

        _transfers.Verify(repository => repository.Add(It.IsAny<InventoryTransfer>()), Times.Never);
    }

    [Fact]
    public void Create_WithRepeatedLines_Throws()
    {
        SetupStores();

        Assert.Throws<InvalidInventoryTransferException>(() => CreateService().Create(
            new CreateInventoryTransferRequest(
                1,
                2,
                new[]
                {
                    new CreateInventoryTransferLine("SKU-1", 1),
                    new CreateInventoryTransferLine("SKU-1", 2)
                })));

        _transfers.Verify(repository => repository.Add(It.IsAny<InventoryTransfer>()), Times.Never);
    }

    // ---- Send ----

    [Fact]
    public void Send_DeductsTheOriginAndAudits()
    {
        var transfer = GivenTransfer();
        SetupTransfer(transfer);
        var first = new StockItem("SKU-1", 1, 10, "Bodega");
        var second = new StockItem("SKU-2", 1, 3, "Bodega");
        _stock.Setup(repository => repository.Get(1, "SKU-1")).Returns(first);
        _stock.Setup(repository => repository.Get(1, "SKU-2")).Returns(second);

        CreateService().Send(9);

        Assert.Equal(InventoryTransferStatus.Sent, transfer.Status);
        Assert.Equal(6, first.QuantityAvailable);
        Assert.Equal(1, second.QuantityAvailable);
        Assert.Equal(new InventoryMovement(MovementType.TransferOut, 4, Date), first.LastMovement);
        Assert.Equal(1, _unitOfWork.Executions);
        _stock.Verify(repository => repository.Update(first), Times.Once);
        _stock.Verify(repository => repository.Update(second), Times.Once);
        _transfers.Verify(repository => repository.Update(transfer), Times.Once);

        var entry = _audit.Entries.Single();
        Assert.Equal("InventoryTransferSent", entry.Action);
        Assert.Contains("6 unidades", entry.Detail);
    }

    [Fact]
    public void Send_WithInsufficientStock_ThrowsAndDoesNotTouchAnything()
    {
        SetupTransfer(GivenTransfer());
        _stock.Setup(repository => repository.Get(1, "SKU-1"))
            .Returns(new StockItem("SKU-1", 1, 3, "Bodega"));
        _stock.Setup(repository => repository.Get(1, "SKU-2"))
            .Returns(new StockItem("SKU-2", 1, 3, "Bodega"));

        Assert.Throws<InsufficientStockException>(() => CreateService().Send(9));

        _stock.Verify(repository => repository.Update(It.IsAny<StockItem>()), Times.Never);
        _transfers.Verify(repository => repository.Update(It.IsAny<InventoryTransfer>()), Times.Never);
        Assert.Equal(0, _unitOfWork.Executions);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Send_WithAMissingStockRow_ThrowsAndDoesNotTouchAnything()
    {
        SetupTransfer(GivenTransfer());
        _stock.Setup(repository => repository.Get(1, "SKU-1"))
            .Returns(new StockItem("SKU-1", 1, 10, "Bodega"));
        _stock.Setup(repository => repository.Get(1, "SKU-2")).Returns((StockItem?)null);

        Assert.Throws<InsufficientStockException>(() => CreateService().Send(9));

        _stock.Verify(repository => repository.Update(It.IsAny<StockItem>()), Times.Never);
        Assert.Equal(0, _unitOfWork.Executions);
    }

    [Fact]
    public void Send_WhenNotDraft_Throws()
    {
        SetupTransfer(GivenTransfer(transfer => transfer.Send(Date)));

        Assert.Throws<InvalidInventoryTransferException>(() => CreateService().Send(9));
        Assert.Equal(0, _unitOfWork.Executions);
    }

    [Fact]
    public void Send_WithAnUnknownTransfer_Throws()
    {
        Assert.Throws<InventoryTransferNotFoundException>(() => CreateService().Send(99));
    }

    [Fact]
    public void Send_WithoutPermission_ThrowsAndDoesNotRead()
    {
        var exception = Assert.Throws<AccessDeniedException>(() => Restricted().Send(9));

        Assert.Equal(Permissions.InventoryStockTransfer, exception.Permission);
        _transfers.Verify(repository => repository.GetById(It.IsAny<int>()), Times.Never);
    }

    // ---- Receive ----

    [Fact]
    public void Receive_AddsToTheDestinationStockAndAudits()
    {
        var transfer = GivenTransfer(item => item.Send(Date));
        SetupTransfer(transfer);
        var first = new StockItem("SKU-1", 2, 1, "Bodega");
        var second = new StockItem("SKU-2", 2, 0, "Bodega");
        _stock.Setup(repository => repository.Get(2, "SKU-1")).Returns(first);
        _stock.Setup(repository => repository.Get(2, "SKU-2")).Returns(second);

        CreateService().Receive(9);

        Assert.Equal(InventoryTransferStatus.Received, transfer.Status);
        Assert.Equal(5, first.QuantityAvailable);
        Assert.Equal(2, second.QuantityAvailable);
        Assert.Equal(new InventoryMovement(MovementType.TransferIn, 4, Date), first.LastMovement);
        _stock.Verify(repository => repository.Update(first), Times.Once);
        _stock.Verify(repository => repository.Update(second), Times.Once);
        _stock.Verify(repository => repository.Add(It.IsAny<StockItem>()), Times.Never);

        Assert.Equal("InventoryTransferReceived", _audit.Entries.Single().Action);
        Assert.Equal(1, _unitOfWork.Executions);
    }

    [Fact]
    public void Receive_CreatesTheDestinationStockRowWhenItDoesNotExist()
    {
        SetupTransfer(GivenTransfer(item => item.Send(Date)));
        _stock.Setup(repository => repository.Get(2, It.IsAny<string>())).Returns((StockItem?)null);

        CreateService().Receive(9);

        _stock.Verify(repository => repository.Add(It.Is<StockItem>(item =>
            item.Sku == "SKU-1"
            && item.StoreId == 2
            && item.QuantityAvailable == 4
            && item.Location == "Recepción de traslado")), Times.Once);
        _stock.Verify(repository => repository.Add(It.Is<StockItem>(item =>
            item.Sku == "SKU-2" && item.QuantityAvailable == 2)), Times.Once);
        _stock.Verify(repository => repository.Update(It.IsAny<StockItem>()), Times.Never);
    }

    [Fact]
    public void Receive_WhenNotSent_Throws()
    {
        SetupTransfer(GivenTransfer());

        Assert.Throws<InvalidInventoryTransferException>(() => CreateService().Receive(9));
        Assert.Equal(0, _unitOfWork.Executions);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void Receive_WithAnUnknownTransfer_Throws()
    {
        Assert.Throws<InventoryTransferNotFoundException>(() => CreateService().Receive(99));
    }

    [Fact]
    public void Receive_WithoutPermission_ThrowsAndDoesNotRead()
    {
        var exception = Assert.Throws<AccessDeniedException>(() => Restricted().Receive(9));

        Assert.Equal(Permissions.InventoryStockTransfer, exception.Permission);
        _transfers.Verify(repository => repository.GetById(It.IsAny<int>()), Times.Never);
    }

    // ---- Cancel ----

    [Fact]
    public void Cancel_ADraft_DoesNotTouchStock()
    {
        var transfer = GivenTransfer();
        SetupTransfer(transfer);

        CreateService().Cancel(9);

        Assert.Equal(InventoryTransferStatus.Cancelled, transfer.Status);
        _stock.Verify(repository => repository.Get(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        _stock.Verify(repository => repository.Update(It.IsAny<StockItem>()), Times.Never);
        _transfers.Verify(repository => repository.Update(transfer), Times.Once);

        var entry = _audit.Entries.Single();
        Assert.Equal("InventoryTransferCancelled", entry.Action);
        Assert.Contains("Draft", entry.Detail);
    }

    [Fact]
    public void Cancel_ASentTransfer_ReturnsTheUnitsToTheOrigin()
    {
        var transfer = GivenTransfer(item => item.Send(Date));
        SetupTransfer(transfer);
        var first = new StockItem("SKU-1", 1, 6, "Bodega");
        var second = new StockItem("SKU-2", 1, 1, "Bodega");
        _stock.Setup(repository => repository.Get(1, "SKU-1")).Returns(first);
        _stock.Setup(repository => repository.Get(1, "SKU-2")).Returns(second);

        CreateService().Cancel(9);

        Assert.Equal(InventoryTransferStatus.Cancelled, transfer.Status);
        Assert.Equal(10, first.QuantityAvailable);
        Assert.Equal(3, second.QuantityAvailable);
        Assert.Equal(new InventoryMovement(MovementType.TransferIn, 4, Date), first.LastMovement);
        _stock.Verify(repository => repository.Update(first), Times.Once);
        _stock.Verify(repository => repository.Update(second), Times.Once);

        var entry = _audit.Entries.Single();
        Assert.Contains("6 unidades devueltas", entry.Detail);
    }

    [Fact]
    public void Cancel_WhenReceived_Throws()
    {
        SetupTransfer(GivenTransfer(item =>
        {
            item.Send(Date);
            item.Receive(Date);
        }));

        Assert.Throws<InvalidInventoryTransferException>(() => CreateService().Cancel(9));
        Assert.Equal(0, _unitOfWork.Executions);
    }

    [Fact]
    public void Cancel_WithAnUnknownTransfer_Throws()
    {
        Assert.Throws<InventoryTransferNotFoundException>(() => CreateService().Cancel(99));
    }

    [Fact]
    public void Cancel_WithoutPermission_ThrowsAndDoesNotRead()
    {
        var exception = Assert.Throws<AccessDeniedException>(() => Restricted().Cancel(9));

        Assert.Equal(Permissions.InventoryStockTransfer, exception.Permission);
        _transfers.Verify(repository => repository.GetById(It.IsAny<int>()), Times.Never);
    }

    private InventoryTransferService Restricted() =>
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
