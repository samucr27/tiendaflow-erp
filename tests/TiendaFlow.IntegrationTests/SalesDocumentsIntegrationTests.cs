using TiendaFlow.Application;
using TiendaFlow.Application.Reporting;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// The point-of-sale sale and the invoice now move inventory and are saved as
/// documents in the same transaction. These tests run them against SQL Server
/// and read the result back with the queries the dashboard and the sales
/// report use.
/// </summary>
public class SalesDocumentsIntegrationTests : IClassFixture<SqlServerDatabaseFixture>
{
    private readonly SqlServerDatabaseFixture _database;

    public SalesDocumentsIntegrationTests(SqlServerDatabaseFixture database) => _database = database;

    private sealed class StoreCashier : ICurrentUser
    {
        public StoreCashier(int storeId) => StoreId = storeId;

        public bool IsAuthenticated => true;
        public int? UserId => null;
        public string Name => "Cajero de prueba";
        public string Email => "cajero.prueba@tiendaflow.local";
        public string Role => RoleNames.Cashier;
        public string RoleLabel => "Cajero";
        public int? StoreId { get; }
        public string StoreName => "Tienda de prueba";
        public string? Ip => "127.0.0.1";
        public IReadOnlySet<string> Permissions { get; } =
            TiendaFlow.Application.Security.Permissions.All.ToHashSet(StringComparer.Ordinal);
    }

    private (int StoreId, string Sku) GivenStoreWithStock(int quantity, int minStock)
    {
        using var context = _database.CreateContext();
        var stores = new EfStoreRepository(context);
        var storeId = stores.NextId();
        stores.Add(new Store(storeId, $"Tienda ventas {storeId}", "Calle 2 # 3-4"));

        var sku = $"SKU-{Guid.NewGuid():N}";
        new EfProductRepository(context).Add(new Product(sku, $"770{Guid.NewGuid():N}", "Jugo 1 L", 6200m));
        new EfStockRepository(context).Add(new StockItem(sku, storeId, quantity, "Nevera 2", minStock));

        return (storeId, sku);
    }

    private static int StockOf(TiendaFlowDbContext context, int storeId, string sku) =>
        new EfStockRepository(context).Get(storeId, sku)!.QuantityAvailable;

    [SqlServerFact]
    public void PosSale_IsSavedWithTheStockDiscountAndShowsInTheSalesReport()
    {
        var (storeId, sku) = GivenStoreWithStock(quantity: 11, minStock: 6);

        PosSaleReceipt receipt;
        using (var context = _database.CreateContext())
        {
            var service = new PosSaleService(
                new EfStockRepository(context),
                new EfProductRepository(context),
                new EfPosSaleRepository(context),
                new EfUnitOfWork(context),
                new AllowAllAccessControl(),
                new EfAuditLog(context),
                new StoreCashier(storeId),
                new SystemClock());

            receipt = service.Register(storeId, sku, 6);
        }

        Assert.True(receipt.SaleId > 0);
        Assert.True(receipt.LeftStockCritical);

        using var check = _database.CreateContext();
        Assert.Equal(5, StockOf(check, storeId, sku));

        var today = DateOnly.FromDateTime(LocalTime.ToLocal(DateTime.UtcNow));
        var report = new SalesReportService(new EfInvoiceQuery(check), new AllowAllAccessControl(), new EfPosSaleQuery(check))
            .Get(today, today);

        var row = Assert.Single(report.PointOfSaleSales, sale => sale.Id == receipt.SaleId);
        Assert.Equal(37200m, row.Total);
        Assert.Equal(6, row.Units);
        Assert.Equal("Sin cajero", row.CashierName);

        var consumption = new EfConsumptionQuery(check).Get(today, today);
        Assert.Equal(6, Assert.Single(consumption, item => item.ProductSku == sku).Quantity);
    }

    [SqlServerFact]
    public void Invoice_TakesStockWhenIssuedAndReturnsItWhenCancelled()
    {
        var (storeId, sku) = GivenStoreWithStock(quantity: 10, minStock: 2);

        int invoiceId;
        using (var context = _database.CreateContext())
        {
            invoiceId = InvoiceService(context, storeId).Create(new CreateInvoiceRequest(
                "Cliente empresa",
                "900111222",
                DateTime.UtcNow,
                new[] { new InvoiceLineRequest(sku, "Jugo 1 L", 4, 6000m) }));
        }

        using (var context = _database.CreateContext())
        {
            Assert.Equal(6, StockOf(context, storeId, sku));
            InvoiceService(context, storeId).Cancel(invoiceId);
        }

        using var check = _database.CreateContext();
        Assert.Equal(10, StockOf(check, storeId, sku));
        Assert.Equal(InvoiceStatus.Cancelled, new EfInvoiceQuery(check).GetById(invoiceId)!.Status);
    }

    [SqlServerFact]
    public void Invoice_WithMoreUnitsThanTheStock_RollsBackEverything()
    {
        var (storeId, sku) = GivenStoreWithStock(quantity: 2, minStock: 0);
        var document = $"DOC-{Guid.NewGuid():N}"[..20];

        using (var context = _database.CreateContext())
        {
            Assert.ThrowsAny<Exception>(() => InvoiceService(context, storeId).Create(new CreateInvoiceRequest(
                "Cliente",
                document,
                DateTime.UtcNow,
                new[]
                {
                    new InvoiceLineRequest(sku, "Jugo 1 L", 1, 6000m),
                    new InvoiceLineRequest(sku + "-X", "Producto sin stock", 1, 1000m)
                })));
        }

        using var check = _database.CreateContext();
        Assert.Equal(2, StockOf(check, storeId, sku));
        Assert.DoesNotContain(new EfInvoiceQuery(check).List(), invoice => invoice.CustomerDocument == document);
    }

    private static SalesInvoiceService InvoiceService(TiendaFlowDbContext context, int storeId) =>
        new(
            new EfInvoiceRepository(context),
            new EfInvoiceQuery(context),
            new EfStockRepository(context),
            new EfUnitOfWork(context),
            new AllowAllAccessControl(),
            new EfAuditLog(context),
            new StoreCashier(storeId));
}
