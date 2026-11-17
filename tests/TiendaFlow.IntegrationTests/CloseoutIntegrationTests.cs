using TiendaFlow.Application;
using TiendaFlow.Application.Crm;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// The closing modules (returns, HR records, CRM records and the editable role
/// matrix) against SQL Server with the Phase12CloseoutModules migration.
/// </summary>
public class CloseoutIntegrationTests : IClassFixture<SqlServerDatabaseFixture>
{
    private readonly SqlServerDatabaseFixture _database;

    public CloseoutIntegrationTests(SqlServerDatabaseFixture database) => _database = database;

    private sealed class StoreUser : ICurrentUser
    {
        public StoreUser(int storeId) => StoreId = storeId;
        public bool IsAuthenticated => true;
        public int? UserId => null;
        public string Name => "Prueba";
        public string Email => "prueba@tiendaflow.local";
        public string Role => RoleNames.StoreManager;
        public string RoleLabel => "Jefe de tienda";
        public int? StoreId { get; }
        public string StoreName => "Tienda de prueba";
        public string? Ip => "127.0.0.1";
        public IReadOnlySet<string> Permissions { get; } =
            TiendaFlow.Application.Security.Permissions.All.ToHashSet(StringComparer.Ordinal);
    }

    private (int StoreId, string Sku) GivenStock(int quantity)
    {
        using var context = _database.CreateContext();
        var stores = new EfStoreRepository(context);
        var storeId = stores.NextId();
        stores.Add(new Store(storeId, $"Tienda cierre {storeId}", "Calle 4 # 5-6"));
        var sku = $"SKU-{Guid.NewGuid():N}";
        new EfProductRepository(context).Add(new Product(sku, $"770{Guid.NewGuid():N}", "Arroz 500 g", 3300m));
        new EfStockRepository(context).Add(new StockItem(sku, storeId, quantity, "Pasillo 2", 2));
        return (storeId, sku);
    }

    [SqlServerFact]
    public void PartialReturn_IsSavedWithItsLinesAndPutsUnitsBack()
    {
        var (storeId, sku) = GivenStock(10);
        int invoiceId;

        using (var context = _database.CreateContext())
        {
            invoiceId = new SalesInvoiceService(new EfInvoiceRepository(context), new EfInvoiceQuery(context),
                    new EfStockRepository(context), new EfUnitOfWork(context), new AllowAllAccessControl(),
                    new EfAuditLog(context), new StoreUser(storeId))
                .Create(new CreateInvoiceRequest("Cliente", $"DOC-{Guid.NewGuid():N}"[..20], DateTime.UtcNow,
                    new[] { new InvoiceLineRequest(sku, "Arroz 500 g", 4, 3300m) }));
        }

        using (var context = _database.CreateContext())
        {
            new SalesReturnService(new EfInvoiceRepository(context), new EfEntityStore<SalesReturn>(context),
                    new EfStockRepository(context), new EfUnitOfWork(context), new AllowAllAccessControl(),
                    new EfAuditLog(context), new StoreUser(storeId), new SystemClock())
                .Register(invoiceId, "Empaque roto", new[] { new SalesReturnLineRequest(sku, 3) });
        }

        using var check = _database.CreateContext();
        Assert.Equal(9, new EfStockRepository(check).Get(storeId, sku)!.QuantityAvailable);
        var saved = new EfEntityStore<SalesReturn>(check).All().Single(item => item.InvoiceId == invoiceId);
        Assert.Equal(3, Assert.Single(saved.Lines).Quantity);
        Assert.Equal(9900m, saved.Total);
    }

    [SqlServerFact]
    public void HrAndCrmRecords_RoundTripThroughSqlServer()
    {
        int employeeId;
        int customerId;
        var document = $"CC-{Guid.NewGuid():N}"[..20];

        using (var context = _database.CreateContext())
        {
            var stores = new EfStoreRepository(context);
            var storeId = stores.NextId();
            stores.Add(new Store(storeId, $"Tienda RR. HH. {storeId}", "Calle 7 # 8-9"));
            var employee = new Employee(0, storeId, $"ID-{Guid.NewGuid():N}"[..20], "Laura Gómez", "Cajera", "Indefinido", 1_800_000m);
            new EfEmployeeRepository(context).Add(employee);
            employeeId = employee.Id;
            var customer = new Customer(0, document, "Natalia Pérez");
            new EfCustomerRepository(context).Add(customer);
            customerId = customer.Id;
        }

        var day = new DateOnly(2026, 11, 16);
        using (var context = _database.CreateContext())
        {
            var contract = new EmploymentContract(employeeId, ContractType.FixedTerm, day.AddMonths(-6), day.AddDays(20), 1_800_000m, "Prueba");
            new EfEntityStore<EmploymentContract>(context).Add(contract);
            contract.Extend(day.AddMonths(6));
            new EfEntityStore<EmploymentContract>(context).Update(contract);

            new EfEntityStore<AttendanceRecord>(context).Add(
                new AttendanceRecord(employeeId, day, AttendanceType.Worked, new TimeOnly(7, 0), new TimeOnly(15, 0)));
            new EfEntityStore<WorkShift>(context).Add(
                new WorkShift(new EfEmployeeRepository(context).GetById(employeeId)!.StoreId, employeeId, day, new TimeOnly(6, 0), new TimeOnly(14, 0)));
            new EfEntityStore<LoyaltyPointsEntry>(context).Add(new LoyaltyPointsEntry(customerId, 25, "Factura de prueba", DateTime.UtcNow));
            var pqrs = new PqrsCase(PqrsType.Claim, "Cobro doble", "Detalle", customerId, null, DateTime.UtcNow);
            new EfEntityStore<PqrsCase>(context).Add(pqrs);
            pqrs.Assign("Jefe de tienda");
            new EfEntityStore<PqrsCase>(context).Update(pqrs);
            new EfEntityStore<MarketingCampaign>(context).Add(new MarketingCampaign("Prueba", "B", day, day.AddDays(10), "Mensaje"));
        }

        using var check = _database.CreateContext();
        var savedContract = new EfEntityStore<EmploymentContract>(check).All().Single(item => item.EmployeeId == employeeId);
        Assert.Equal(day.AddMonths(6), savedContract.EndDate);
        Assert.Equal(1, savedContract.Extensions);
        Assert.Equal(8m, new EfEntityStore<AttendanceRecord>(check).All().Single(item => item.EmployeeId == employeeId).HoursWorked);
        Assert.Single(new EfEntityStore<WorkShift>(check).All(), item => item.EmployeeId == employeeId);
        Assert.Equal(25, new EfEntityStore<LoyaltyPointsEntry>(check).All().Where(item => item.CustomerId == customerId).Sum(item => item.Points));
        Assert.Equal(PqrsStatus.InProgress, new EfEntityStore<PqrsCase>(check).All().Single(item => item.CustomerId == customerId).Status);
    }

    [SqlServerFact]
    public void RoleMatrix_IsStoredAndUsedAtSignIn()
    {
        var roleId = SystemRoles.IdFor(RoleNames.StockClerk);

        using (var context = _database.CreateContext())
        {
            if (!context.Roles.Any(role => role.Id == roleId))
            {
                context.Roles.Add(new Role(roleId, RoleNames.StockClerk));
                context.SaveChanges();
            }
            var store = new EfRolePermissionStore(context);
            var permissions = RolePermissions.ForRole(RoleNames.StockClerk).ToHashSet(StringComparer.Ordinal);
            permissions.Add(Permissions.InventoryReportsView);
            store.Replace(roleId, permissions);
        }

        using (var context = _database.CreateContext())
        {
            var store = new EfRolePermissionStore(context);
            Assert.Contains(Permissions.InventoryReportsView, EffectivePermissions.For(RoleNames.StockClerk, store));
            store.Clear(roleId);
            Assert.Null(store.StoredFor(roleId));
            Assert.Equal(RolePermissions.ForRole(RoleNames.StockClerk), EffectivePermissions.For(RoleNames.StockClerk, store));
        }
    }
}
