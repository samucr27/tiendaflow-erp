using TiendaFlow.Application;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// Real SQL Server coverage for the Phase 4 inventory documents: persistence,
/// list/detail read models, the stock movements of every transition and the
/// single-use adjustment. Authorization and validation edge cases live in the
/// unit tests; these tests prove the database behavior.
/// </summary>
public class InventoryDocumentsIntegrationTests : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);

    private readonly SqlServerDatabaseFixture _database;

    public InventoryDocumentsIntegrationTests(SqlServerDatabaseFixture database) =>
        _database = database;

    private static string NewSku() => $"SKU-{Guid.NewGuid():N}";

    private static string NewBarcode() => $"770{Guid.NewGuid():N}";

    private static (int Id, string Name) NewStore(TiendaFlowDbContext context)
    {
        var repository = new EfStoreRepository(context);
        var id = repository.NextId();
        var name = $"Tienda inventario {id}";

        repository.Add(new Store(id, name, "Calle 1 # 2-3"));

        return (id, name);
    }

    private static string NewProduct(TiendaFlowDbContext context, string name, decimal price)
    {
        var sku = NewSku();
        new EfProductRepository(context).Add(new Product(sku, NewBarcode(), name, price));
        return sku;
    }

    private static InventoryCountService CountService(TiendaFlowDbContext context) =>
        new(
            new EfInventoryCountRepository(context),
            new EfStockRepository(context),
            new EfStockQuery(context),
            new EfStoreRepository(context),
            new EfUnitOfWork(context),
            new AllowAllAccessControl(),
            new EfAuditLog(context),
            new TestCurrentUser(),
            new TestClock(Now));

    private static InventoryTransferService TransferService(TiendaFlowDbContext context) =>
        new(
            new EfInventoryTransferRepository(context),
            new EfStockRepository(context),
            new EfStoreRepository(context),
            new EfUnitOfWork(context),
            new AllowAllAccessControl(),
            new EfAuditLog(context),
            new TestCurrentUser(),
            new TestClock(Now));

    [SqlServerFact]
    public void ConfirmCount_KeepsTheSnapshotAndAppliesTheAdjustmentOnce()
    {
        int storeId;
        string sku;
        int countId;

        using (var context = _database.CreateContext())
        {
            (storeId, _) = NewStore(context);
            sku = NewProduct(context, "Producto de conteo", 5000m);
            new EfStockRepository(context).Add(new StockItem(sku, storeId, 12, "Bodega", minStock: 2));

            countId = CountService(context).Create(storeId, "  Bodega  ");

            var detail = new EfInventoryDocumentQuery(context).GetCount(countId);
            Assert.NotNull(detail);
            Assert.Equal("Bodega", detail!.Header.Location);
            var line = Assert.Single(detail.Lines);
            Assert.Equal(sku, line.ProductSku);
            Assert.Equal(12, line.SystemQuantity);
            Assert.Null(line.CountedQuantity);
        }

        // A receipt after opening the count must not rewrite the snapshot.
        using (var context = _database.CreateContext())
        {
            var stock = new EfStockRepository(context);
            var item = stock.Get(storeId, sku)!;
            item.RegisterReceipt(5, Now.AddHours(1));
            stock.Update(item);
        }

        using (var context = _database.CreateContext())
        {
            var service = CountService(context);
            service.SetCounted(countId, new Dictionary<string, int> { [sku] = 10 });
            service.Close(countId);
        }

        using (var context = _database.CreateContext())
        {
            var detail = new EfInventoryDocumentQuery(context).GetCount(countId)!;
            Assert.Equal(12, detail.Lines.Single().SystemQuantity);
            Assert.Equal(10, detail.Lines.Single().CountedQuantity);
            Assert.Equal(-2, detail.Lines.Single().Difference);
            Assert.Equal(InventoryCountStatus.Closed, detail.Header.Status);
        }

        using (var context = _database.CreateContext())
        {
            var service = CountService(context);
            service.Adjust(countId);

            var stock = new EfStockRepository(context).Get(storeId, sku)!;
            Assert.Equal(10, stock.QuantityAvailable);
            Assert.Equal(MovementType.Adjustment, stock.LastMovement!.Type);
            Assert.Equal(7, stock.LastMovement.Quantity);

            var count = new EfInventoryCountRepository(context).GetById(countId)!;
            Assert.Equal(InventoryCountStatus.Adjusted, count.Status);
            Assert.NotNull(count.AdjustedAt);

            Assert.Contains(
                context.AuditEntries,
                entry => entry.Action == "InventoryCountAdjusted"
                    && entry.EntityId == countId.ToString()
                    && entry.Module == "inventory");
        }

        using (var context = _database.CreateContext())
        {
            Assert.Throws<InvalidInventoryCountException>(
                () => CountService(context).Adjust(countId));
        }
    }

    [SqlServerFact]
    public void TransferFlow_SendAndReceiveMovesStockBetweenStores()
    {
        int originId;
        int destinationId;
        string sku;
        int transferId;

        using (var context = _database.CreateContext())
        {
            (originId, _) = NewStore(context);
            (destinationId, _) = NewStore(context);
            sku = NewProduct(context, "Producto de traslado", 8000m);
            new EfStockRepository(context).Add(new StockItem(sku, originId, 10, "Bodega"));

            transferId = TransferService(context).Create(new CreateInventoryTransferRequest(
                originId,
                destinationId,
                new[] { new CreateInventoryTransferLine(sku, 4) }));
        }

        using (var context = _database.CreateContext())
        {
            TransferService(context).Send(transferId);

            Assert.Equal(6, new EfStockRepository(context).Get(originId, sku)!.QuantityAvailable);
            Assert.Null(new EfStockRepository(context).Get(destinationId, sku));

            var transfer = new EfInventoryTransferRepository(context).GetById(transferId)!;
            Assert.Equal(InventoryTransferStatus.Sent, transfer.Status);
        }

        using (var context = _database.CreateContext())
        {
            TransferService(context).Receive(transferId);

            var destination = new EfStockRepository(context).Get(destinationId, sku)!;
            Assert.Equal(4, destination.QuantityAvailable);
            Assert.Equal(MovementType.TransferIn, destination.LastMovement!.Type);

            var transfer = new EfInventoryTransferRepository(context).GetById(transferId)!;
            Assert.Equal(InventoryTransferStatus.Received, transfer.Status);

            Assert.Contains(
                context.AuditEntries,
                entry => entry.Action == "InventoryTransferReceived"
                    && entry.EntityId == transferId.ToString());
        }

        using (var context = _database.CreateContext())
        {
            Assert.Throws<InvalidInventoryTransferException>(
                () => TransferService(context).Receive(transferId));
        }
    }

    [SqlServerFact]
    public void TransferFlow_CancelSentReturnsTheUnitsToTheOrigin()
    {
        int originId;
        int destinationId;
        string sku;
        int transferId;

        using (var context = _database.CreateContext())
        {
            (originId, _) = NewStore(context);
            (destinationId, _) = NewStore(context);
            sku = NewProduct(context, "Producto anulado", 3000m);
            new EfStockRepository(context).Add(new StockItem(sku, originId, 9, "Bodega"));

            transferId = TransferService(context).Create(new CreateInventoryTransferRequest(
                originId,
                destinationId,
                new[] { new CreateInventoryTransferLine(sku, 3) }));
        }

        using (var context = _database.CreateContext())
            TransferService(context).Send(transferId);

        using (var context = _database.CreateContext())
        {
            TransferService(context).Cancel(transferId);

            var origin = new EfStockRepository(context).Get(originId, sku)!;
            Assert.Equal(9, origin.QuantityAvailable);
            Assert.Equal(MovementType.TransferIn, origin.LastMovement!.Type);

            var transfer = new EfInventoryTransferRepository(context).GetById(transferId)!;
            Assert.Equal(InventoryTransferStatus.Cancelled, transfer.Status);
        }
    }

    [SqlServerFact]
    public void DocumentQuery_ListsAndDetailsJoinNamesAndRespectTheStoreFilter()
    {
        int originId;
        int destinationId;
        string originName;
        string destinationName;
        string firstSku;
        string secondSku;
        int countId;
        int transferId;

        using (var context = _database.CreateContext())
        {
            (originId, originName) = NewStore(context);
            (destinationId, destinationName) = NewStore(context);
            firstSku = NewProduct(context, "Aceite de integración", 9800m);
            secondSku = NewProduct(context, "Arroz de integración", 3300m);

            var count = new InventoryCount(0, originId, "Bodega", Now);
            count.AddLine(secondSku, 8);
            count.AddLine(firstSku, 10);
            count.SetCounted(firstSku, 7);
            new EfInventoryCountRepository(context).Add(count);
            countId = count.Id;

            var transfer = new InventoryTransfer(0, originId, destinationId, Now);
            transfer.AddLine(secondSku, 5);
            transfer.AddLine(firstSku, 2);
            new EfInventoryTransferRepository(context).Add(transfer);
            transferId = transfer.Id;
        }

        using (var context = _database.CreateContext())
        {
            var query = new EfInventoryDocumentQuery(context);

            var countRow = Assert.Single(query.ListCounts(originId));
            Assert.Equal(originName, countRow.StoreName);
            Assert.Equal("Bodega", countRow.Location);
            Assert.Equal(2, countRow.LineCount);
            Assert.Equal(1, countRow.CountedLineCount);
            Assert.Equal(-3, countRow.DifferenceUnits);
            Assert.Empty(query.ListCounts(destinationId));

            var countDetail = query.GetCount(countId)!;
            Assert.Equal(2, countDetail.Lines.Count);
            var firstLine = countDetail.Lines[0];
            Assert.Equal(firstSku, firstLine.ProductSku);
            Assert.Equal("Aceite de integración", firstLine.ProductName);
            Assert.Equal(10, firstLine.SystemQuantity);
            Assert.Equal(7, firstLine.CountedQuantity);
            Assert.Equal(-3, firstLine.Difference);

            var transferRow = Assert.Single(query.ListTransfers(originId));
            Assert.Equal(originName, transferRow.OriginStoreName);
            Assert.Equal(destinationName, transferRow.DestinationStoreName);
            Assert.Equal(2, transferRow.LineCount);
            Assert.Equal(7, transferRow.TotalUnits);

            // The destination also sees the transfer in its own list.
            Assert.Single(query.ListTransfers(destinationId));

            var transferDetail = query.GetTransfer(transferId)!;
            Assert.Equal("Aceite de integración", transferDetail.Lines[0].ProductName);
            Assert.Equal(2, transferDetail.Lines[0].Quantity);

            Assert.Null(query.GetCount(int.MaxValue));
            Assert.Null(query.GetTransfer(int.MaxValue));
        }
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; }
    }
}
