using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

public class PurchasingPhase3IntegrationTests : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 12, 15, 0, 0, DateTimeKind.Utc);

    private readonly SqlServerDatabaseFixture _database;

    public PurchasingPhase3IntegrationTests(SqlServerDatabaseFixture database) =>
        _database = database;

    private static string NewSku() => $"SKU-{Guid.NewGuid():N}";

    private static string NewBarcode() => $"770{Guid.NewGuid():N}";

    private static string NewNit() => $"9{Guid.NewGuid():N}"[..12];

    private static int NewStoreId() => Random.Shared.Next(10000, 90000);

    private static async Task<int> AddStoreAsync(TiendaFlowDbContext context)
    {
        var id = NewStoreId();
        var name = $"Tienda integración {id}";
        var address = "Calle 1 # 2-3";

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [store] ([id], [name], [address], [active]) VALUES ({id}, {name}, {address}, 1)");

        return id;
    }

    [SqlServerFact]
    public void ProductSupplier_EnforcesOneDefaultPerProductAndPersistsTheCost()
    {
        var sku = NewSku();
        using (var context = _database.CreateContext())
        {
            new EfProductRepository(context).Add(
                new Product(sku, NewBarcode(), "Café de integración", 15000m));
        }

        int firstSupplierId;
        int secondSupplierId;

        using (var context = _database.CreateContext())
        {
            var suppliers = new EfSupplierRepository(context);
            suppliers.Add(new Supplier(0, NewNit(), "Proveedor uno", "Calle 1"));
            suppliers.Add(new Supplier(0, NewNit(), "Proveedor dos", "Calle 2"));
            firstSupplierId = suppliers.List().Single(supplier => supplier.BusinessName == "Proveedor uno").Id;
            secondSupplierId = suppliers.List().Single(supplier => supplier.BusinessName == "Proveedor dos").Id;
        }

        using (var context = _database.CreateContext())
        {
            var relations = new EfProductSupplierRepository(context);
            relations.Add(new ProductSupplier(sku, firstSupplierId, 10000m, isDefault: true));

            var stored = relations.Get(sku, firstSupplierId);
            Assert.NotNull(stored);
            Assert.Equal(10000m, stored.LastCost);
            Assert.True(stored.IsDefault);

            var second = new ProductSupplier(sku, secondSupplierId, 9000m, isDefault: true);

            // One default per product is a database guarantee, not only a
            // service rule.
            Assert.Throws<DbUpdateException>(() => relations.Add(second));
        }

        using (var context = _database.CreateContext())
        {
            var relations = new EfProductSupplierRepository(context);
            var relation = relations.Get(sku, firstSupplierId);
            Assert.NotNull(relation);

            relation!.ChangeCost(11000m);
            relations.Update(relation);

            var reloaded = new EfProductSupplierRepository(_database.CreateContext())
                .Get(sku, firstSupplierId);

            Assert.Equal(11000m, reloaded!.LastCost);
        }
    }

    [SqlServerFact]
    public void SupplierQuery_ComputesPunctualityAndFulfillmentFromRealOrders()
    {
        var sku = NewSku();

        using var context = _database.CreateContext();
        var storeId = AddStoreAsync(context).GetAwaiter().GetResult();
        new EfProductRepository(context).Add(new Product(sku, NewBarcode(), "Arroz integración", 3000m));

        var suppliers = new EfSupplierRepository(context);
        suppliers.Add(new Supplier(0, NewNit(), "Distribuidora Central", "Calle 5"));
        var supplier = suppliers.List().Single(item => item.BusinessName == "Distribuidora Central");

        var orders = new EfPurchaseOrderRepository(context);

        var onTime = new PurchaseOrder(0, supplier.Id, storeId, Now, new DateOnly(2026, 11, 10));
        onTime.AddLine(sku, 10, 2000m);
        onTime.Approve();
        onTime.Send();
        onTime.ReceiveLine(sku, 10, new DateOnly(2026, 11, 8));
        orders.Add(onTime);

        var late = new PurchaseOrder(0, supplier.Id, storeId, Now, new DateOnly(2026, 11, 5));
        late.AddLine(sku, 5, 2000m);
        late.Approve();
        late.Send();
        late.ReceiveLine(sku, 5, new DateOnly(2026, 11, 6));
        orders.Add(late);

        var row = new EfSupplierQuery(_database.CreateContext()).GetById(supplier.Id);

        Assert.NotNull(row);
        Assert.Equal(2, row!.OrderCount);
        Assert.Equal(2, row.ReceivedOrderCount);
        Assert.Equal(2, row.MeasuredOrderCount);
        Assert.Equal(1, row.OnTimeOrderCount);
        Assert.Equal(15, row.OrderedUnits);
        Assert.Equal(15, row.ReceivedUnits);
        Assert.Equal(50m, row.PunctualityRate);
        Assert.Equal(100m, row.FulfillmentRate);
        Assert.Equal("50 %", row.PunctualityLabel);
    }

    [SqlServerFact]
    public void ConsumptionQuery_SumsIssuedLinesInsideTheLocalDateWindow()
    {
        var sku = NewSku();

        using var context = _database.CreateContext();
        var invoices = new EfInvoiceRepository(context);

        var inside = new SalesInvoice(
            0,
            "Cliente integración",
            "900123456",
            new DateTime(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc));
        inside.AddLine(sku, "Producto de integración", 7, 4500m);
        invoices.Add(inside);

        var outside = new SalesInvoice(
            0,
            "Cliente integración",
            "900123456",
            new DateTime(2026, 11, 1, 15, 0, 0, DateTimeKind.Utc));
        outside.AddLine(sku, "Producto de integración", 100, 4500m);
        invoices.Add(outside);

        var cancelled = new SalesInvoice(
            0,
            "Cliente integración",
            "900123456",
            new DateTime(2026, 11, 10, 18, 0, 0, DateTimeKind.Utc));
        cancelled.AddLine(sku, "Producto de integración", 50, 4500m);
        cancelled.Cancel();
        invoices.Add(cancelled);

        var rows = new EfConsumptionQuery(_database.CreateContext())
            .Get(new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 11));

        var row = Assert.Single(rows, item => item.ProductSku == sku);
        Assert.Equal(7, row.Quantity);
    }

    [SqlServerFact]
    public void Replenishment_GeneratesOneDraftPerSupplierWithoutSendingIt()
    {
        var sku = NewSku();
        var clock = new FixedClock(Now);

        using var context = _database.CreateContext();
        var storeId = AddStoreAsync(context).GetAwaiter().GetResult();
        new EfProductRepository(context).Add(new Product(sku, NewBarcode(), "Leche integración", 4200m));
        new EfStockRepository(context).Add(new StockItem(sku, storeId, 0, "Bodega", minStock: 10));

        var suppliers = new EfSupplierRepository(context);
        suppliers.Add(new Supplier(0, NewNit(), "Lácteos del Norte", "Calle 9"));
        var supplier = suppliers.List().Single(item => item.BusinessName == "Lácteos del Norte");

        new EfProductSupplierRepository(context)
            .Add(new ProductSupplier(sku, supplier.Id, 3500m, isDefault: true));

        var parameters = new ParameterValues(
            new EfSystemParameterRepository(context),
            clock);
        var service = new ReplenishmentService(
            new EfStockQuery(context),
            new EfConsumptionQuery(context),
            new EfProductSupplierRepository(context),
            new EfSupplierRepository(context),
            new EfPurchaseOrderRepository(context),
            parameters,
            new EfUnitOfWork(context),
            new AllowAllAccessControl(),
            new EfAuditLog(context),
            new TestCurrentUser(),
            clock);

        var preview = service.Preview(storeId);

        var row = Assert.Single(preview.Rows);
        Assert.Equal(sku, row.Sku);
        Assert.Equal(10, row.SuggestedQuantity);
        Assert.Equal(supplier.Id, row.SupplierId);
        Assert.Equal(35000m, row.EstimatedCost);

        var result = service.GenerateDrafts(storeId);

        var draft = Assert.Single(result.Orders);
        Assert.Equal(supplier.Id, draft.SupplierId);
        Assert.Equal(1, draft.LineCount);
        Assert.Equal(35000m, draft.Total);

        using var verifyContext = _database.CreateContext();
        var order = new EfPurchaseOrderRepository(verifyContext).GetById(draft.OrderId);

        Assert.NotNull(order);
        Assert.Equal(PurchaseOrderStatus.Draft, order!.Status);
        Assert.Null(order.ReceivedDate);
        Assert.Single(order.Lines);
        Assert.Equal(10, order.Lines[0].Quantity);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow) => UtcNow = UtcTimestamp.Normalize(utcNow);

        public DateTime UtcNow { get; }
    }
}
