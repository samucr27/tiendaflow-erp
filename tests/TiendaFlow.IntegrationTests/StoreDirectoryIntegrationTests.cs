using Microsoft.EntityFrameworkCore;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

public class StoreDirectoryIntegrationTests : IClassFixture<SqlServerDatabaseFixture>
{
    private readonly SqlServerDatabaseFixture _database;

    public StoreDirectoryIntegrationTests(SqlServerDatabaseFixture database) => _database = database;

    private static int NewStoreId() => Random.Shared.Next(2000, 9000);

    [SqlServerFact]
    public void StoreDirectoryAndUsageQuery_ReportTheRealCounters()
    {
        var storeId = NewStoreId();
        var storeName = $"Tienda integración {Guid.NewGuid():N}";
        var idNumber = $"E-{Guid.NewGuid():N}"[..20];
        var email = $"tienda.{Guid.NewGuid():N}@tiendaflow.local";
        var sku = $"SKU-{Guid.NewGuid():N}";
        var nit = $"N{Guid.NewGuid():N}"[..20];
        int userId;

        using (var context = _database.CreateContext())
        {
            context.Stores.Add(new Store(storeId, storeName, "Calle 1 # 2-3"));
            context.Stocks.Add(new StockItem(sku, storeId, 7, "Pasillo 1", minStock: 2));

            var employee = new Employee(
                0,
                storeId,
                idNumber,
                "Empleado de integración",
                "Cajero",
                "Indefinido",
                0m);
            context.Employees.Add(employee);

            var supplier = new Supplier(0, nit, "Proveedor de integración", "Carrera 4 # 5-6");
            context.Suppliers.Add(supplier);
            context.SaveChanges();

            context.PurchaseOrders.Add(new PurchaseOrder(0, supplier.Id, storeId, DateTime.UtcNow));
            context.SaveChanges();

            var user = new AppUser(employee.Id, 4, email, "test-only-hash");
            context.AppUsers.Add(user);
            context.SaveChanges();

            userId = user.Id;
        }

        using (var context = _database.CreateContext())
        {
            var usage = new EfStoreUsageQuery(context);

            Assert.True(usage.HasStock(storeId));
            Assert.True(usage.HasOpenPurchaseOrders(storeId));
            Assert.True(usage.HasActiveUsers(storeId));

            var directory = new EfStoreDirectoryQuery(context);
            var row = directory.List().Single(candidate => candidate.Id == storeId);

            Assert.Equal(storeName, row.Name);
            Assert.True(row.Active);
            Assert.Equal(1, row.ActiveUsers);
            Assert.Equal(1, row.EmployeeCount);
            Assert.Equal(7, row.UnitsInStock);
            Assert.Equal(1, row.OpenOrders);

            var detail = directory.Get(storeId);
            Assert.NotNull(detail);
            Assert.Contains(detail!.Users, user => user.UserId == userId && user.StoreId == storeId);
            Assert.Contains(detail.Employees, employee => employee.IdNumber == idNumber);
        }

        using (var context = _database.CreateContext())
        {
            var stores = new EfStoreRepository(context);

            Assert.True(stores.ExistsName(storeName));
            Assert.False(stores.ExistsName(storeName, storeId));
            Assert.True(stores.NextId() > storeId);
        }
    }
}
