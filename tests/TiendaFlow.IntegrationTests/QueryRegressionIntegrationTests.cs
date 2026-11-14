using TiendaFlow.Application;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// Regression guard for the list and detail queries behind every screen. Each
/// test executes the real LINQ against SQL Server: the original defect was an
/// OrderBy or Where applied after projecting to a record constructor, which EF
/// Core cannot translate. Running against the provider (instead of an
/// in-memory context) is the only way to keep that class of bug from coming
/// back.
/// </summary>
public class QueryRegressionIntegrationTests : IClassFixture<SqlServerDatabaseFixture>
{
    private readonly SqlServerDatabaseFixture _database;

    public QueryRegressionIntegrationTests(SqlServerDatabaseFixture database) =>
        _database = database;

    private static string Token() => Guid.NewGuid().ToString("N")[..8];

    [SqlServerFact]
    public void SupplierQuery_ListAndDetail_ReturnTheKpis()
    {
        var token = Token();
        var nit = $"N{token}";
        var businessName = $"Proveedor {token}";
        var receivedSku = $"SKU-R-{token}";
        var cancelledSku = $"SKU-C-{token}";
        var draftSku = $"SKU-D-{token}";
        int supplierId;

        using (var context = _database.CreateContext())
        {
            context.Products.AddRange(
                new Product(receivedSku, $"BCR{token}", $"Producto recibido {token}", 1500m),
                new Product(cancelledSku, $"BCC{token}", $"Producto cancelado {token}", 2500m),
                new Product(draftSku, $"BCD{token}", $"Producto borrador {token}", 4000m));

            var supplier = new Supplier(0, nit, businessName, "Calle 1 # 2-3", rating: 81m);
            context.Suppliers.Add(supplier);
            context.SaveChanges();

            supplier.ChangeRating(86m);

            var received = new PurchaseOrder(
                0,
                supplier.Id,
                1,
                DateTime.UtcNow.AddDays(-2),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
            received.AddLine(receivedSku, 10, 1000m);
            received.Approve();
            received.Send();
            received.ReceiveLine(
                receivedSku,
                10,
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

            var cancelled = new PurchaseOrder(0, supplier.Id, 1, DateTime.UtcNow.AddDays(-3));
            cancelled.AddLine(cancelledSku, 5, 2000m);
            cancelled.Cancel();

            var draft = new PurchaseOrder(0, supplier.Id, 1, DateTime.UtcNow.AddDays(-1));
            draft.AddLine(draftSku, 2, 3300m);

            context.PurchaseOrders.AddRange(received, cancelled, draft);
            context.SaveChanges();

            supplierId = supplier.Id;
        }

        using (var context = _database.CreateContext())
        {
            var query = new EfSupplierQuery(context);

            var detail = query.GetById(supplierId);

            Assert.NotNull(detail);
            Assert.Equal(nit, detail!.Nit);
            Assert.Equal(86m, detail.Rating);
            Assert.Equal(3, detail.OrderCount);
            Assert.Equal(1, detail.ReceivedOrderCount);
            Assert.Equal(1, detail.MeasuredOrderCount);
            Assert.Equal(1, detail.OnTimeOrderCount);
            Assert.Equal(10, detail.OrderedUnits);
            Assert.Equal(10, detail.ReceivedUnits);
            Assert.Equal(16600m, detail.PurchasedTotal);

            var list = query.List();

            Assert.Contains(list, row => row.Id == supplierId && row.BusinessName == businessName);
        }
    }

    [SqlServerFact]
    public void CustomerQuery_ListAndDetail_ReturnTheCustomer()
    {
        var token = Token();
        var document = $"CC{token}";
        int customerId;

        using (var context = _database.CreateContext())
        {
            var customer = new Customer(
                0,
                document,
                $"Cliente {token}",
                $"cliente.{token}@example.com",
                "3000000000",
                "Carrera 1 # 2-3");
            context.Customers.Add(customer);
            context.SaveChanges();
            customerId = customer.Id;
        }

        using (var context = _database.CreateContext())
        {
            var query = new EfCustomerQuery(context);

            var detail = query.GetById(customerId);

            Assert.NotNull(detail);
            Assert.Equal(document, detail!.DocumentNumber);
            Assert.Equal($"cliente.{token}@example.com", detail.Email);

            var list = query.List();

            Assert.Contains(list, row => row.Id == customerId);
        }
    }

    [SqlServerFact]
    public void PurchaseOrderQuery_ListBySupplierAndDetail_ReturnTheLines()
    {
        var token = Token();
        var supplierNit = $"N{token}";
        var lowerName = $"AAA producto {token}";
        var higherName = $"ZZZ producto {token}";
        var lowerSku = $"SKU-A-{token}";
        var higherSku = $"SKU-Z-{token}";
        int supplierId;
        int orderId;

        using (var context = _database.CreateContext())
        {
            var supplier = new Supplier(0, supplierNit, $"Proveedor {token}", "Calle 4 # 5-6");
            context.Suppliers.Add(supplier);

            context.Products.AddRange(
                new Product(lowerSku, $"BC-A-{token}", lowerName, 5000m),
                new Product(higherSku, $"BC-Z-{token}", higherName, 7000m));

            context.SaveChanges();

            var order = new PurchaseOrder(
                0,
                supplier.Id,
                1,
                DateTime.UtcNow.AddDays(-1),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)));
            order.AddLine(higherSku, 1, 3000m);
            order.AddLine(lowerSku, 2, 2500m);

            context.PurchaseOrders.Add(order);
            context.SaveChanges();

            supplierId = supplier.Id;
            orderId = order.Id;
        }

        using (var context = _database.CreateContext())
        {
            var query = new EfPurchaseOrderQuery(context);

            var detail = query.GetById(orderId);

            Assert.NotNull(detail);
            Assert.Equal(supplierId, detail!.Header.SupplierId);
            Assert.Equal(2, detail.Header.LineCount);
            Assert.Equal(8000m, detail.Header.Total);
            Assert.Equal(
                new[] { lowerName, higherName },
                detail.Lines.Select(line => line.ProductName));

            var list = query.List();
            Assert.Contains(list, row => row.Id == orderId);

            var bySupplier = query.ListBySupplier(supplierId);
            Assert.Single(bySupplier);
            Assert.Equal(orderId, bySupplier[0].Id);
        }
    }

    [SqlServerFact]
    public void InvoiceQuery_ListAndDetail_ReturnTheInvoiceAndItsLines()
    {
        var token = Token();
        int invoiceId;

        using (var context = _database.CreateContext())
        {
            var invoice = new SalesInvoice(
                0,
                $"Cliente factura {token}",
                $"NIT-{token}",
                DateTime.UtcNow.AddHours(-1));
            invoice.AddLine("SKU-1001", "Agua 600 ml", 2, 2500m);
            invoice.AddLine("SKU-1003", "Arroz 500 g", 1, 3300m);

            context.SalesInvoices.Add(invoice);
            context.SaveChanges();

            invoiceId = invoice.Id;
        }

        using (var context = _database.CreateContext())
        {
            var query = new EfInvoiceQuery(context);

            var detail = query.GetById(invoiceId);

            Assert.NotNull(detail);
            Assert.Equal(2, detail!.Lines.Count);
            Assert.Equal(8300m, detail.Total);
            Assert.Equal(
                new[] { "SKU-1001", "SKU-1003" },
                detail.Lines.Select(line => line.ProductSku));

            var list = query.List();
            var row = Assert.Single(list, candidate => candidate.Id == invoiceId);
            Assert.Equal(2, row.LineCount);
            Assert.Equal(8300m, row.Total);
        }
    }

    [SqlServerFact]
    public void ProductAndStockQueries_ListAndDetail_ReturnTheSeededRows()
    {
        var token = Token();
        var sku = $"SKU-REG-{token}";
        var barcode = $"77099{token}";
        var name = $"Producto regresión {token}";

        using (var context = _database.CreateContext())
        {
            context.Products.Add(new Product(sku, barcode, name, 4200m));
            var stock = new StockItem(sku, 1, 0, $"Pasillo {token}", minStock: 3);
            stock.RegisterReceipt(7, DateTime.UtcNow.AddDays(-2));
            stock.RegisterSale(2, DateTime.UtcNow.AddDays(-1));
            context.Stocks.Add(stock);
            context.SaveChanges();
        }

        using (var context = _database.CreateContext())
        {
            var catalog = new EfProductCatalogQuery(context);

            var found = Assert.Single(catalog.Search(token));
            Assert.Equal(sku, found.Sku);
            Assert.Equal(barcode, found.Barcode);
            Assert.Equal(name, found.Name);
            Assert.Equal(4200m, found.SalePrice);

            var detail = catalog.GetBySku(sku);
            Assert.NotNull(detail);
            Assert.Equal(name, detail!.Name);
            Assert.True(catalog.Count() > 0);

            var stockQuery = new EfStockQuery(context);

            var storeRows = stockQuery.ListByStore(1);
            var storeRow = Assert.Single(storeRows, row => row.Sku == sku);
            Assert.Equal(5, storeRow.QuantityAvailable);
            Assert.Equal(3, storeRow.MinStock);
            Assert.Equal(name, storeRow.ProductName);
            Assert.NotNull(storeRow.LastMovement);
            Assert.Equal(MovementType.Sale, storeRow.LastMovement!.Type);

            var skuRows = stockQuery.ListBySku(sku);
            Assert.Single(skuRows);
        }
    }

    [SqlServerFact]
    public void ConsumptionQuery_SumsIssuedInvoicesInTheDateRange()
    {
        var token = Token();
        var sku = $"SKU-CONS-{token}";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var context = _database.CreateContext())
        {
            var issued = new SalesInvoice(
                0,
                $"Consumo {token}",
                $"DOC-{token}",
                DateTime.UtcNow);
            issued.AddLine(sku, "Producto de consumo", 4, 1000m);

            var cancelled = new SalesInvoice(
                0,
                $"Consumo anulado {token}",
                $"DOC-C-{token}",
                DateTime.UtcNow);
            cancelled.AddLine(sku, "Producto de consumo", 9, 1000m);
            cancelled.Cancel();

            context.SalesInvoices.AddRange(issued, cancelled);
            context.SaveChanges();
        }

        using (var context = _database.CreateContext())
        {
            var query = new EfConsumptionQuery(context);

            var rows = query.Get(today.AddDays(-1), today.AddDays(1));

            var row = Assert.Single(rows, candidate => candidate.ProductSku == sku);
            Assert.Equal(4, row.Quantity);
        }
    }

    [SqlServerFact]
    public void UserDirectoryAndEmployeeQueries_ListAndDetail_ReturnTheUser()
    {
        var token = Token();
        var idNumber = $"E-{token}";
        var email = $"usuario.{token}@tiendaflow.local";
        int userId;

        using (var context = _database.CreateContext())
        {
            var employee = new Employee(
                0,
                1,
                idNumber,
                $"Empleado {token}",
                "Cajero",
                "Indefinido",
                1800000m);
            context.Employees.Add(employee);
            context.SaveChanges();

            var user = new AppUser(employee.Id, 4, email, "test-only-hash");
            context.AppUsers.Add(user);
            context.SaveChanges();

            userId = user.Id;
        }

        using (var context = _database.CreateContext())
        {
            var users = new EfUserDirectoryQuery(context);

            var detail = users.Get(userId);

            Assert.NotNull(detail);
            Assert.Equal(email, detail!.Email);
            Assert.Equal($"Empleado {token}", detail.FullName);
            Assert.Equal("Cashier", detail.RoleName);

            var list = users.List();
            Assert.Contains(list, row => row.Id == userId && row.IdNumber == idNumber);

            var employees = new EfEmployeeRepository(context);

            var employeeDetail = employees.GetByIdNumber(idNumber);
            Assert.NotNull(employeeDetail);
            Assert.Equal($"Empleado {token}", employeeDetail!.Name);
            Assert.Contains(employees.List(), row => row.IdNumber == idNumber);
        }
    }

    [SqlServerFact]
    public void AuditTrailQuery_Recent_ResolvesTheUserName()
    {
        var token = Token();
        long entryId;

        using (var context = _database.CreateContext())
        {
            var employee = new Employee(
                0,
                1,
                $"A-{token}",
                $"Auditor {token}",
                "Analista",
                "Indefinido",
                2000000m);
            context.Employees.Add(employee);
            context.SaveChanges();

            var user = new AppUser(employee.Id, 1, $"auditor.{token}@tiendaflow.local", "test-only-hash");
            context.AppUsers.Add(user);
            context.SaveChanges();

            var entry = new AuditEntry(
                $"RegressionAction{token}",
                user.Id.ToString(),
                $"Evento de regresión {token}",
                user.Id,
                DateTime.UtcNow,
                module: "security",
                ip: "127.0.0.1");
            context.AuditEntries.Add(entry);
            context.SaveChanges();

            entryId = entry.Id;
        }

        using (var context = _database.CreateContext())
        {
            var query = new EfAuditTrailQuery(context);

            var rows = query.Recent(500);

            var row = Assert.Single(rows, candidate => candidate.Id == entryId);
            Assert.Equal($"RegressionAction{token}", row.Action);
            Assert.Equal($"Auditor {token}", row.UserName);
        }
    }

    [SqlServerFact]
    public void ParameterQueries_ListAndLatest_ReturnTheVersionedValue()
    {
        var token = Token();
        var key = $"regression.{token}";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var context = _database.CreateContext())
        {
            context.SystemParameters.Add(new SystemParameter(
                key,
                ParameterValueType.Integer,
                ParameterScope.Global,
                null,
                "15",
                today,
                1,
                null,
                DateTime.UtcNow));
            context.SaveChanges();
        }

        using (var context = _database.CreateContext())
        {
            var repository = new EfSystemParameterRepository(context);

            Assert.Contains(repository.List(), row => row.Key == key);
            Assert.Single(repository.ListForKey(key));

            var latest = repository.GetLatest(key, ParameterScope.Global, null, today);

            Assert.NotNull(latest);
            Assert.Equal("15", latest!.Value);
            Assert.Equal(1, latest.Version);

            var authors = new EfParameterAuthorQuery(context);
            Assert.NotNull(authors.Names());
        }
    }

    [SqlServerFact]
    public void ProductSupplierQuery_ListAndDetail_ReturnTheRelation()
    {
        var token = Token();
        var sku = $"SKU-PS-{token}";
        int supplierId;

        using (var context = _database.CreateContext())
        {
            context.Products.Add(new Product(sku, $"BCPS{token}", $"Producto proveedor {token}", 6000m));
            var supplier = new Supplier(0, $"NPS{token}", $"Proveedor relación {token}", "Calle 9 # 8-7");
            context.Suppliers.Add(supplier);
            context.SaveChanges();

            context.ProductSuppliers.Add(new ProductSupplier(sku, supplier.Id, 4100m, isDefault: true));
            context.SaveChanges();

            supplierId = supplier.Id;
        }

        using (var context = _database.CreateContext())
        {
            var repository = new EfProductSupplierRepository(context);

            var relation = repository.Get(sku, supplierId);

            Assert.NotNull(relation);
            Assert.True(relation!.IsDefault);
            Assert.Equal(4100m, relation.LastCost);

            Assert.Contains(
                repository.List(),
                row => row.ProductSku == sku && row.SupplierId == supplierId);
        }
    }
}
