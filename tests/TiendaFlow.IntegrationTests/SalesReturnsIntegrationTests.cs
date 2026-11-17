using TiendaFlow.Application;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// Real SQL Server coverage for the returns module: persistence of the
/// document and its lines, the credit-note authorization with the stock
/// re-entry, the reject path that never touches the inventory and the read
/// models the screens consume.
/// </summary>
public class SalesReturnsIntegrationTests : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 13, 15, 0, 0, DateTimeKind.Utc);

    private readonly SqlServerDatabaseFixture _database;

    public SalesReturnsIntegrationTests(SqlServerDatabaseFixture database) =>
        _database = database;

    private static string NewSku() => $"SKU-{Guid.NewGuid():N}";

    private static string NewBarcode() => $"770{Guid.NewGuid():N}";

    private static (int Id, string Name) NewStore(TiendaFlowDbContext context)
    {
        var repository = new EfStoreRepository(context);
        var id = repository.NextId();
        var name = $"Tienda devolución {id}";

        repository.Add(new Store(id, name, "Calle 1 # 2-3"));

        return (id, name);
    }

    private static string NewProduct(TiendaFlowDbContext context, string name, decimal price)
    {
        var sku = NewSku();
        new EfProductRepository(context).Add(new Product(sku, NewBarcode(), name, price));
        return sku;
    }

    private static SalesReturnService Service(TiendaFlowDbContext context) =>
        new(
            new EfSalesReturnRepository(context),
            new EfSalesReturnQuery(context),
            new EfInvoiceRepository(context),
            new EfStockRepository(context),
            new EfStoreRepository(context),
            new EfUnitOfWork(context),
            new AllowAllAccessControl(),
            new EfAuditLog(context),
            new TestCurrentUser(),
            new TestClock(Now));

    private static SalesInvoice CreateInvoice(
        TiendaFlowDbContext context,
        params (string Sku, int Quantity, decimal Price)[] lines)
    {
        var invoice = new SalesInvoice(0, "Cliente devolución", "1020304050", Now.AddDays(-2));

        foreach (var line in lines)
            invoice.AddLine(line.Sku, $"Producto {line.Sku}", line.Quantity, line.Price);

        new EfInvoiceRepository(context).Add(invoice);
        return invoice;
    }

    [SqlServerFact]
    public void Authorize_ReturnsTheUnitsToStockAndIssuesOneCreditNote()
    {
        int storeId;
        int invoiceId;
        string firstSku;
        string secondSku;
        int returnId;

        using (var context = _database.CreateContext())
        {
            (storeId, _) = NewStore(context);
            firstSku = NewProduct(context, "Agua devolución", 2500m);
            secondSku = NewProduct(context, "Gaseosa devolución", 3500m);

            var stock = new EfStockRepository(context);
            stock.Add(new StockItem(firstSku, storeId, 20, "Pasillo 1"));
            stock.Add(new StockItem(secondSku, storeId, 15, "Pasillo 1"));

            var invoice = CreateInvoice(
                context,
                (firstSku, 5, 2500m),
                (secondSku, 4, 3500m));
            invoiceId = invoice.Id;

            returnId = Service(context).Create(new CreateSalesReturnRequest(
                invoiceId,
                storeId,
                "Producto en mal estado",
                new[]
                {
                    new SalesReturnLineRequest(firstSku, 2),
                    new SalesReturnLineRequest(secondSku, 1)
                }));
        }

        using (var context = _database.CreateContext())
        {
            var detail = new EfSalesReturnQuery(context).GetById(returnId)!;

            Assert.Equal(SalesReturnStatus.Pending, detail.Return.Status);
            Assert.Null(detail.Return.CreditNoteNumber);
            Assert.Equal(2, detail.Lines.Count);
            Assert.Equal(8500m, detail.Total);
            Assert.Equal("Cliente devolución", detail.Return.CustomerName);
        }

        using (var context = _database.CreateContext())
        {
            Service(context).Authorize(returnId);

            var first = new EfStockRepository(context).Get(storeId, firstSku)!;
            var second = new EfStockRepository(context).Get(storeId, secondSku)!;

            Assert.Equal(22, first.QuantityAvailable);
            Assert.Equal(MovementType.Return, first.LastMovement!.Type);
            Assert.Equal(2, first.LastMovement.Quantity);
            Assert.Equal(16, second.QuantityAvailable);

            var document = new EfSalesReturnRepository(context).GetById(returnId)!;
            Assert.Equal(SalesReturnStatus.Authorized, document.Status);
            Assert.Equal($"NC-{returnId:D6}", document.CreditNoteNumber);
            Assert.NotNull(document.AuthorizedAt);

            Assert.Contains(
                context.AuditEntries,
                entry => entry.Action == "SalesReturnAuthorized"
                    && entry.EntityId == returnId.ToString()
                    && entry.Module == "sales");
        }

        using (var context = _database.CreateContext())
        {
            Assert.Throws<InvalidSalesReturnException>(
                () => Service(context).Authorize(returnId));

            Assert.Equal(22, new EfStockRepository(context).Get(storeId, firstSku)!.QuantityAvailable);
        }
    }

    [SqlServerFact]
    public void Create_RejectsMoreUnitsThanTheInvoiceStillAllows()
    {
        int storeId;
        int invoiceId;
        string sku;
        int returnId;

        using (var context = _database.CreateContext())
        {
            (storeId, _) = NewStore(context);
            sku = NewProduct(context, "Producto límite", 4000m);
            new EfStockRepository(context).Add(new StockItem(sku, storeId, 10, "Bodega"));

            var invoice = CreateInvoice(context, (sku, 3, 4000m));
            invoiceId = invoice.Id;

            returnId = Service(context).Create(new CreateSalesReturnRequest(
                invoiceId,
                storeId,
                "Devolución parcial",
                new[] { new SalesReturnLineRequest(sku, 2) }));
        }

        using (var context = _database.CreateContext())
            Service(context).Authorize(returnId);

        using (var context = _database.CreateContext())
        {
            Assert.Throws<InvalidSalesReturnException>(() =>
                Service(context).Create(new CreateSalesReturnRequest(
                    invoiceId,
                    storeId,
                    "Intento por encima de lo vendido",
                    new[] { new SalesReturnLineRequest(sku, 2) })));

            var allowed = Service(context).Create(new CreateSalesReturnRequest(
                invoiceId,
                storeId,
                "Devolución restante",
                new[] { new SalesReturnLineRequest(sku, 1) }));

            Assert.True(allowed > returnId);
        }
    }

    [SqlServerFact]
    public void Reject_ClosesTheReturnWithoutMovingTheStock()
    {
        int storeId;
        int invoiceId;
        string sku;
        int returnId;

        using (var context = _database.CreateContext())
        {
            (storeId, _) = NewStore(context);
            sku = NewProduct(context, "Producto rechazado", 5000m);
            new EfStockRepository(context).Add(new StockItem(sku, storeId, 7, "Bodega"));

            var invoice = CreateInvoice(context, (sku, 2, 5000m));
            invoiceId = invoice.Id;

            returnId = Service(context).Create(new CreateSalesReturnRequest(
                invoiceId,
                storeId,
                "Reclamo sin evidencia",
                new[] { new SalesReturnLineRequest(sku, 1) }));
        }

        using (var context = _database.CreateContext())
            Service(context).Reject(returnId);

        using (var context = _database.CreateContext())
        {
            var document = new EfSalesReturnRepository(context).GetById(returnId)!;
            Assert.Equal(SalesReturnStatus.Rejected, document.Status);
            Assert.Null(document.CreditNoteNumber);
            Assert.NotNull(document.RejectedAt);
            Assert.Equal(7, new EfStockRepository(context).Get(storeId, sku)!.QuantityAvailable);

            Assert.Contains(
                context.AuditEntries,
                entry => entry.Action == "SalesReturnRejected"
                    && entry.EntityId == returnId.ToString());
        }
    }

    [SqlServerFact]
    public void Query_ListsByStoreAndReportsTheReturnableUnits()
    {
        int storeId;
        int otherStoreId;
        string storeName;
        string sku;
        int invoiceId;
        int returnId;

        using (var context = _database.CreateContext())
        {
            (storeId, storeName) = NewStore(context);
            (otherStoreId, _) = NewStore(context);
            sku = NewProduct(context, "Producto consultado", 6000m);
            new EfStockRepository(context).Add(new StockItem(sku, storeId, 12, "Bodega"));

            var invoice = CreateInvoice(context, (sku, 5, 6000m));
            invoiceId = invoice.Id;

            returnId = Service(context).Create(new CreateSalesReturnRequest(
                invoiceId,
                storeId,
                "Producto defectuoso",
                new[] { new SalesReturnLineRequest(sku, 2) }));
        }

        using (var context = _database.CreateContext())
        {
            var query = new EfSalesReturnQuery(context);
            var row = Assert.Single(query.List(storeId));

            Assert.Equal(returnId, row.Id);
            Assert.Equal(storeName, row.StoreName);
            Assert.Equal("Pendiente", row.StatusLabel);
            Assert.Equal(12000m, row.Total);

            Assert.Empty(query.List(otherStoreId));

            Assert.Contains(
                query.ListReturnableInvoices(),
                invoice => invoice.Id == invoiceId && invoice.CustomerName == "Cliente devolución");

            var pendingLine = query.ListInvoiceLines(invoiceId).Single();
            Assert.Equal(5, pendingLine.InvoicedQuantity);
            Assert.Equal(0, pendingLine.ReturnedQuantity);
            Assert.Equal(5, pendingLine.ReturnableQuantity);
        }

        using (var context = _database.CreateContext())
            Service(context).Authorize(returnId);

        using (var context = _database.CreateContext())
        {
            var line = new EfSalesReturnQuery(context).ListInvoiceLines(invoiceId).Single();

            Assert.Equal(2, line.ReturnedQuantity);
            Assert.Equal(3, line.ReturnableQuantity);
        }
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; }
    }
}
