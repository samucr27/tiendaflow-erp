using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// Requests for quotation against SQL Server with the Phase13Quotations
/// migration: CRUD, approval and the purchase order generated from them.
/// </summary>
public class QuotationIntegrationTests : IClassFixture<SqlServerDatabaseFixture>
{
    private readonly SqlServerDatabaseFixture _database;

    public QuotationIntegrationTests(SqlServerDatabaseFixture database) => _database = database;

    private sealed class Buyer : ICurrentUser
    {
        public Buyer(int storeId) => StoreId = storeId;
        public bool IsAuthenticated => true;
        public int? UserId => null;
        public string Name => "Carlos Ramírez";
        public string Email => "jefe.tienda@tiendaflow.local";
        public string Role => RoleNames.StoreManager;
        public string RoleLabel => "Jefe de tienda";
        public int? StoreId { get; }
        public string StoreName => "Tienda de prueba";
        public string? Ip => "127.0.0.1";
        public IReadOnlySet<string> Permissions { get; } =
            TiendaFlow.Application.Security.Permissions.All.ToHashSet(StringComparer.Ordinal);
    }

    private static QuotationService Service(TiendaFlowDbContext context, int storeId)
    {
        var user = new Buyer(storeId);
        var unitOfWork = new EfUnitOfWork(context);
        var orders = new PurchaseOrderService(new EfPurchaseOrderRepository(context), new EfSupplierRepository(context),
            new EfProductSupplierRepository(context), new EfStockQuery(context),
            new StockService(new EfStockRepository(context), new AllowAllAccessControl()), unitOfWork,
            new AllowAllAccessControl(), new EfAuditLog(context), user);
        return new QuotationService(new EfEntityStore<QuotationRequest>(context), new EfSupplierRepository(context),
            new EfProductCatalogQuery(context), orders, unitOfWork, new AllowAllAccessControl(),
            new EfAuditLog(context), user, new SystemClock());
    }

    private (int StoreId, int SupplierId, string Sku) Given()
    {
        using var context = _database.CreateContext();
        var stores = new EfStoreRepository(context);
        var storeId = stores.NextId();
        stores.Add(new Store(storeId, $"Tienda cotización {storeId}", "Calle 8 # 9-10"));
        var sku = $"SKU-{Guid.NewGuid():N}"[..20];
        new EfProductRepository(context).Add(new Product(sku, $"770{Guid.NewGuid():N}"[..13], "Leche entera 1 L", 4800m));
        var suppliers = new EfSupplierRepository(context);
        var nit = $"9{Guid.NewGuid():N}"[..12];
        suppliers.Add(new Supplier(0, nit, "Lácteos de prueba", "Carrera 7"));
        return (storeId, suppliers.GetByNit(nit)!.Id, sku);
    }

    [SqlServerFact]
    public void ApprovedQuotation_BecomesADraftPurchaseOrderAtTheQuotedPrice()
    {
        var (storeId, supplierId, sku) = Given();
        int quotationId;
        int orderId;

        using (var context = _database.CreateContext())
        {
            var service = Service(context, storeId);
            quotationId = service.Create(new QuotationInput(supplierId, sku, 48, 3900m, null, "Refrigerados"));
            service.Update(quotationId, new QuotationInput(supplierId, sku, 50, 3850m, null, "Refrigerados"));
            service.Approve(quotationId);
        }

        using (var context = _database.CreateContext())
            orderId = Service(context, storeId).GenerateOrder(quotationId, storeId);

        using var check = _database.CreateContext();
        var saved = new EfEntityStore<QuotationRequest>(check).Get(quotationId)!;
        Assert.Equal(QuotationStatus.Ordered, saved.Status);
        Assert.Equal(orderId, saved.PurchaseOrderId);
        Assert.Equal("Carlos Ramírez", saved.DecidedByName);
        var order = new EfPurchaseOrderRepository(check).GetById(orderId)!;
        Assert.Equal(supplierId, order.SupplierId);
        Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
        Assert.Equal(50 * 3850m, order.Total);
    }

    [SqlServerFact]
    public void PendingQuotation_CanBeDeleted()
    {
        var (storeId, supplierId, sku) = Given();
        int quotationId;

        using (var context = _database.CreateContext())
        {
            var service = Service(context, storeId);
            quotationId = service.Create(new QuotationInput(supplierId, sku, 10, 3900m, null, null));
            service.Delete(quotationId);
        }

        using var check = _database.CreateContext();
        Assert.Null(new EfEntityStore<QuotationRequest>(check).Get(quotationId));
    }
}
