using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.Web.Persistence;

/// <summary>
/// Seeds the catalog, the stores, the seven roles, one demo user per role and
/// enough operational data for every screen in the demo to show information.
/// It is registered only when Seed:Enabled is true (see Program.cs), so a
/// production deployment never creates demo accounts by accident. Passwords
/// are read from configuration (Seed:AdminPassword and Seed:DemoPassword) and
/// are never stored in this file, in logs or in the repository. The schema is
/// migrated separately through Database:MigrateOnStartup or the EF bundle.
///
/// Every section decides per record, not per table: each row is looked up by a
/// stable natural key (NIT, document, SKU, email, id number, supplier + store +
/// SKU set for orders, and so on). Starting the application twice never
/// duplicates data, and an old database missing a single demo row gets that row
/// back without touching the ones that already exist.
/// </summary>
public sealed class DevDataSeeder : IHostedService
{
    public const string AdminPasswordKey = "Seed:AdminPassword";
    public const string DemoPasswordKey = "Seed:DemoPassword";

    private const int DefaultStoreId = 1;
    private const int NorthStoreId = 2;

    private static readonly SeedStore[] DemoStores =
    {
        new(DefaultStoreId, "Tienda Centro", "Calle 10 # 20-30, Bogotá"),
        new(NorthStoreId, "Tienda Norte", "Carrera 15 # 120-45, Bogotá")
    };

    private static readonly SeedEmployee[] DemoEmployees =
    {
        // The seven accounts below match one user per role.
        new("1000000001", "Administrador del sistema", "Administrador", DefaultStoreId, 4500000m),
        new("1000000002", "Laura Gómez", "Gerente corporativo", DefaultStoreId, 6200000m),
        new("1000000003", "Carlos Ramírez", "Jefe de tienda", DefaultStoreId, 3900000m),
        new("1000000004", "Ana Torres", "Cajero", DefaultStoreId, 1850000m),
        new("1000000005", "Diego Herrera", "Auxiliar de bodega", NorthStoreId, 1950000m),
        new("1000000006", "Sofía Rojas", "Coordinador de logística", DefaultStoreId, 3300000m),
        new("1000000007", "Marcela Díaz", "Gestor de recursos humanos", DefaultStoreId, 3600000m),

        // Additional employees make the per-store headcount realistic.
        new("1000000008", "Valentina Ruiz", "Jefe de tienda", NorthStoreId, 3700000m),
        new("1000000009", "Julián Ospina", "Cajero", NorthStoreId, 1800000m),
        new("1000000010", "Paula Méndez", "Cajera", DefaultStoreId, 1820000m),
        new("1000000011", "Andrés Castro", "Auxiliar de bodega", DefaultStoreId, 1920000m)
    };

    private static readonly SeedProduct[] DemoProducts =
    {
        new("SKU-1001", "7701234567001", "Agua 600 ml", 2500m, 40, 10, 35, 10, "Pasillo 1", true),
        new("SKU-1002", "7701234567002", "Gaseosa 400 ml", 3500m, 25, 8, 18, 8, "Pasillo 1", true),
        new("SKU-1003", "7701234567003", "Arroz 500 g", 3300m, 18, 6, 12, 6, "Pasillo 2", false),
        new("SKU-1004", "7701234567004", "Pan tajado", 5500m, 12, 5, 7, 5, "Pasillo 3", true),
        new("SKU-1005", "7701234567005", "Leche entera 1 L", 4800m, 4, 12, 9, 10, "Refrigerados", false),
        new("SKU-1006", "7701234567006", "Café molido 500 g", 12500m, 6, 10, 4, 8, "Pasillo 4", false),
        new("SKU-1007", "7701234567007", "Aceite girasol 1 L", 9800m, 9, 9, 11, 6, "Pasillo 2", false),
        new("SKU-1008", "7701234567008", "Azúcar 1 kg", 4600m, 30, 10, 22, 10, "Pasillo 2", false),
        new("SKU-1009", "7701234567009", "Sal 500 g", 2200m, 3, 8, 14, 8, "Pasillo 2", false),
        new("SKU-1010", "7701234567010", "Papel higiénico x4", 8900m, 22, 8, 16, 8, "Pasillo 5", true),
        new("SKU-1011", "7701234567011", "Jabón de baño", 3400m, 15, 6, 10, 6, "Pasillo 5", false),
        new("SKU-1012", "7701234567012", "Detergente 900 g", 11200m, 5, 10, 3, 8, "Pasillo 5", false),
        new("SKU-1013", "7701234567013", "Galletas surtidas", 3800m, 35, 12, 20, 12, "Pasillo 3", true),
        new("SKU-1014", "7701234567014", "Jugo de naranja 1 L", 6200m, 11, 6, 6, 6, "Refrigerados", false),
        new("SKU-1015", "7701234567015", "Atún en lata", 5400m, 2, 6, 8, 6, "Pasillo 4", false)
    };

    private static readonly SeedSupplier[] DemoSuppliers =
    {
        new("900123456-1", "Distribuidora Andina S.A.S.", "Calle 13 # 45-20, Bogotá", 92m, true),
        new("900654321-2", "Lácteos del Altiplano Ltda.", "Carrera 7 # 12-34, Tunja", 76m, true),
        new("901987654-3", "Aseo Total Colombia S.A.S.", "Avenida 68 # 22-10, Bogotá", 64m, true),
        new("900456789-4", "Bebidas del Caribe S.A.S.", "Calle 72 # 54-11, Barranquilla", 88m, true),
        new("901234567-5", "Snacks La Sabana Ltda.", "Carrera 50 # 26-80, Bogotá", 55m, true),
        new("900987654-6", "Suministros del Pacífico S.A.S.", "Avenida 6N # 23-45, Cali", 71m, false)
    };

    private static readonly SeedCustomer[] DemoCustomers =
    {
        new("1010101010", "María Fernanda Rojas", "maria.rojas@example.com", "3001234567", "Calle 45 # 12-30, Bogotá", true),
        new("2222222222", "Cliente mostrador", null, null, null, true),
        new("79876543", "Jorge Iván Peña", "jorge.pena@example.com", "3107654321", "Carrera 15 # 80-20, Bogotá", true),
        new("1020304050", "Camila Andrea Suárez", "camila.suarez@example.com", "3204567890", "Calle 100 # 15-25, Bogotá", true),
        new("79123456", "Luis Alberto Gómez", "luis.gomez@example.com", "3156789012", "Avenida 68 # 40-12, Bogotá", true),
        new("1030405060", "Natalia Pérez", "natalia.perez@example.com", "3012345678", "Carrera 7 # 60-40, Bogotá", true),
        new("80456789", "Ricardo Molina", "ricardo.molina@example.com", "3123456789", "Calle 26 # 90-15, Bogotá", true),
        new("1040506070", "Sofía Herrera", "sofia.herrera@example.com", "3187654321", "Carrera 11 # 70-30, Bogotá", false)
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DevDataSeeder> _logger;

    public DevDataSeeder(
        IServiceScopeFactory scopeFactory,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<DevDataSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TiendaFlowDbContext>();

        await SeedStoresAsync(context, cancellationToken);
        await SeedRolesAsync(context, cancellationToken);
        await SeedEmployeesAsync(context, cancellationToken);
        await SeedContractsAsync(context, cancellationToken);
        await SeedUsersAsync(context, cancellationToken);
        await SeedCatalogAsync(context, cancellationToken);
        await SeedSuppliersAsync(context, cancellationToken);
        await SeedProductSuppliersAsync(context, cancellationToken);
        await SeedCustomersAsync(context, cancellationToken);
        await SeedPurchaseOrdersAsync(context, cancellationToken);
        await SeedInvoicesAsync(context, cancellationToken);
        await SeedSalesReturnsAsync(context, cancellationToken);
        await SeedInventoryCountsAsync(context, cancellationToken);
        await SeedInventoryTransfersAsync(context, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedStoresAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        foreach (var seed in DemoStores)
        {
            if (await context.Stores.AnyAsync(store => store.Id == seed.Id, cancellationToken))
                continue;

            context.Stores.Add(new Store(seed.Id, seed.Name, seed.Address));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRolesAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        foreach (var name in RoleNames.All)
        {
            var id = SystemRoles.IdFor(name);

            if (await context.Roles.AnyAsync(role => role.Id == id, cancellationToken))
                continue;

            context.Roles.Add(new Role(id, name));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedEmployeesAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        foreach (var seed in DemoEmployees)
        {
            if (await context.Employees.AnyAsync(
                    employee => employee.IdNumber == seed.IdNumber,
                    cancellationToken))
            {
                continue;
            }

            context.Employees.Add(new Employee(
                id: 0,
                storeId: seed.StoreId,
                idNumber: seed.IdNumber,
                name: seed.FullName,
                position: seed.Position,
                contractType: "Indefinido",
                salary: seed.Salary));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// One contract per employee, with two demo histories: one expiring
    /// fixed-term contract for the alert panel and one renewed employee whose
    /// previous contract stays as extended. The natural key is employee plus
    /// start date, so a second run never duplicates a contract.
    /// </summary>
    private static async Task SeedContractsAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        var employees = (await context.Employees.ToListAsync(cancellationToken))
            .ToDictionary(employee => employee.IdNumber, StringComparer.Ordinal);

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        // The employees with a special history are skipped in the default pass.
        var withHistory = new[] { "1000000004", "1000000005", "1000000009" };
        var defaultStart = new DateOnly(2023, 1, 15);

        foreach (var seed in DemoEmployees)
        {
            if (withHistory.Contains(seed.IdNumber, StringComparer.Ordinal))
                continue;
            if (!employees.TryGetValue(seed.IdNumber, out var employee))
                continue;

            if (await ContractExistsAsync(context, employee.Id, defaultStart, cancellationToken))
                continue;

            context.EmployeeContracts.Add(new EmploymentContract(
                id: 0,
                employee.Id,
                ContractType.Indefinite,
                defaultStart,
                endDate: null,
                employee.Salary,
                now));
        }

        await context.SaveChangesAsync(cancellationToken);

        if (employees.TryGetValue("1000000004", out var cashier))
        {
            // Ends inside the alert window (hr.contracts.expiry_alert_days).
            var start = new DateOnly(2026, 1, 1);

            if (!await ContractExistsAsync(context, cashier.Id, start, cancellationToken))
            {
                context.EmployeeContracts.Add(new EmploymentContract(
                    id: 0,
                    cashier.Id,
                    ContractType.FixedTerm,
                    start,
                    today.AddDays(20),
                    cashier.Salary,
                    now));
            }
        }

        if (employees.TryGetValue("1000000009", out var former))
        {
            var start = new DateOnly(2022, 3, 1);

            if (!await ContractExistsAsync(context, former.Id, start, cancellationToken))
            {
                var terminated = new EmploymentContract(
                    id: 0,
                    former.Id,
                    ContractType.FixedTerm,
                    start,
                    new DateOnly(2023, 12, 31),
                    former.Salary,
                    now);

                terminated.Terminate(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc));
                context.EmployeeContracts.Add(terminated);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        if (employees.TryGetValue("1000000005", out var renewedEmployee))
        {
            var previousStart = new DateOnly(2024, 1, 1);
            var renewedStart = new DateOnly(2025, 1, 1);

            var previous = await context.EmployeeContracts.FirstOrDefaultAsync(
                contract => contract.EmployeeId == renewedEmployee.Id
                    && contract.StartDate == previousStart,
                cancellationToken);

            if (previous is null)
            {
                previous = new EmploymentContract(
                    id: 0,
                    renewedEmployee.Id,
                    ContractType.FixedTerm,
                    previousStart,
                    new DateOnly(2024, 12, 31),
                    renewedEmployee.Salary,
                    now);

                context.EmployeeContracts.Add(previous);
                await context.SaveChangesAsync(cancellationToken);
            }

            if (!await ContractExistsAsync(
                    context,
                    renewedEmployee.Id,
                    renewedStart,
                    cancellationToken))
            {
                if (previous.Status == EmploymentContractStatus.Active)
                {
                    var renewed = previous.Renew(
                        renewedStart,
                        new DateOnly(2026, 12, 31),
                        renewedEmployee.Salary,
                        now);

                    previous.MarkExtended();
                    context.EmployeeContracts.Add(renewed);
                }
                else
                {
                    context.EmployeeContracts.Add(new EmploymentContract(
                        id: 0,
                        renewedEmployee.Id,
                        ContractType.FixedTerm,
                        renewedStart,
                        new DateOnly(2026, 12, 31),
                        renewedEmployee.Salary,
                        now));
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Task<bool> ContractExistsAsync(
        TiendaFlowDbContext context,
        int employeeId,
        DateOnly startDate,
        CancellationToken cancellationToken) =>
        context.EmployeeContracts.AnyAsync(
            contract => contract.EmployeeId == employeeId
                && contract.StartDate == startDate,
            cancellationToken);

    private async Task SeedUsersAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        var adminPassword = _configuration[AdminPasswordKey];
        var demoPassword = _configuration[DemoPasswordKey];

        if (string.IsNullOrWhiteSpace(adminPassword) || string.IsNullOrWhiteSpace(demoPassword))
        {
            _logger.LogWarning(
                "Security seed skipped: set {AdminKey} and {DemoKey} with dotnet user-secrets.",
                AdminPasswordKey,
                DemoPasswordKey);
            return;
        }

        var accounts = new List<SeedAccount>
        {
            new("1000000001", "admin@tiendaflow.local", RoleNames.Administrator, adminPassword),
            new("1000000002", "gerente@tiendaflow.local", RoleNames.CorporateManager, demoPassword),
            new("1000000003", "jefe.tienda@tiendaflow.local", RoleNames.StoreManager, demoPassword),
            new("1000000004", "cajero@tiendaflow.local", RoleNames.Cashier, demoPassword),
            new("1000000005", "bodega@tiendaflow.local", RoleNames.StockClerk, demoPassword),
            new("1000000006", "logistica@tiendaflow.local", RoleNames.LogisticsCoordinator, demoPassword),
            new("1000000007", "rrhh@tiendaflow.local", RoleNames.HumanResourcesOfficer, demoPassword)
        };

        var employees = await context.Employees
            .ToDictionaryAsync(employee => employee.IdNumber, cancellationToken);

        var created = 0;

        foreach (var account in accounts)
        {
            if (await context.AppUsers.AnyAsync(
                    user => user.Email == account.Email,
                    cancellationToken))
            {
                continue;
            }

            if (!employees.TryGetValue(account.IdNumber, out var employee))
                continue;

            context.AppUsers.Add(new AppUser(
                employee.Id,
                SystemRoles.IdFor(account.RoleName),
                account.Email,
                _passwordHasher.Hash(account.Password)));

            created++;
        }

        await context.SaveChangesAsync(cancellationToken);

        if (created > 0)
        {
            _logger.LogInformation(
                "Seeded {UserCount} development users: admin plus one user per role.",
                created);
        }
    }

    private static async Task SeedCatalogAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        foreach (var seed in DemoProducts)
        {
            if (!await context.Products.AnyAsync(
                    product => product.Sku == seed.Sku,
                    cancellationToken))
            {
                context.Products.Add(new Product(
                    seed.Sku,
                    seed.Barcode,
                    seed.Name,
                    seed.SalePrice));
            }

            await EnsureStockAsync(
                context,
                seed.Sku,
                DefaultStoreId,
                seed.StoreOneQuantity,
                seed.StoreOneMinimum,
                seed.Location,
                registerSale: seed.RegisterSale,
                cancellationToken);

            await EnsureStockAsync(
                context,
                seed.Sku,
                NorthStoreId,
                seed.StoreTwoQuantity,
                seed.StoreTwoMinimum,
                seed.Location,
                registerSale: false,
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureStockAsync(
        TiendaFlowDbContext context,
        string sku,
        int storeId,
        int quantity,
        int minStock,
        string location,
        bool registerSale,
        CancellationToken cancellationToken)
    {
        if (await context.Stocks.AnyAsync(
                item => item.Sku == sku && item.StoreId == storeId,
                cancellationToken))
        {
            return;
        }

        var stock = new StockItem(sku, storeId, 0, location, minStock);
        var received = registerSale ? quantity + 1 : quantity;
        stock.RegisterReceipt(received, DateTime.UtcNow.AddDays(-10));

        if (registerSale)
            stock.RegisterSale(1, DateTime.UtcNow.AddDays(-1));

        context.Stocks.Add(stock);
    }

    private static async Task SeedSuppliersAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        foreach (var seed in DemoSuppliers)
        {
            if (await context.Suppliers.AnyAsync(
                    supplier => supplier.Nit == seed.Nit,
                    cancellationToken))
            {
                continue;
            }

            var supplier = new Supplier(
                0,
                seed.Nit,
                seed.BusinessName,
                seed.Address,
                seed.Rating);

            if (!seed.Active)
                supplier.Deactivate();

            context.Suppliers.Add(supplier);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCustomersAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        foreach (var seed in DemoCustomers)
        {
            if (await context.Customers.AnyAsync(
                    customer => customer.DocumentNumber == seed.DocumentNumber,
                    cancellationToken))
            {
                continue;
            }

            context.Customers.Add(new Customer(
                0,
                seed.DocumentNumber,
                seed.FullName,
                seed.Email,
                seed.Phone,
                seed.Address,
                seed.Active));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    // Default supplier of each demo product, so the replenishment screen can
    // turn the critical stock alerts (HU-05) into draft purchase orders
    // without any manual setup. The last cost is about 65 % of the sale price.
    private static readonly (string Sku, string SupplierNit)[] DemoProductSuppliers =
    {
        ("SKU-1001", "900456789-4"), ("SKU-1002", "900456789-4"),
        ("SKU-1003", "900123456-1"), ("SKU-1004", "900123456-1"),
        ("SKU-1005", "900654321-2"), ("SKU-1006", "900123456-1"),
        ("SKU-1007", "900123456-1"), ("SKU-1008", "900123456-1"),
        ("SKU-1009", "900123456-1"), ("SKU-1010", "901987654-3"),
        ("SKU-1011", "901987654-3"), ("SKU-1012", "901987654-3"),
        ("SKU-1013", "901234567-5"), ("SKU-1014", "900654321-2"),
        ("SKU-1015", "900123456-1")
    };

    private static async Task SeedProductSuppliersAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        // The natural key of the relation is (SKU, supplier id). A table-wide
        // guard would skip the whole section when a single row exists, leaving
        // an old database permanently incomplete.
        var existing = await context.ProductSuppliers
            .AsNoTracking()
            .Select(item => new { item.ProductSku, item.SupplierId })
            .ToListAsync(cancellationToken);

        var keys = existing
            .Select(item => (item.ProductSku, item.SupplierId))
            .ToHashSet();

        var suppliers = await context.Suppliers
            .ToDictionaryAsync(supplier => supplier.Nit, cancellationToken);

        foreach (var (sku, nit) in DemoProductSuppliers)
        {
            var product = DemoProducts.FirstOrDefault(seed => seed.Sku == sku);
            if (product is null || !suppliers.TryGetValue(nit, out var supplier))
                continue;

            if (!keys.Add((sku, supplier.Id)))
                continue;

            var lastCost = Math.Round(product.SalePrice * 0.65m / 100m) * 100m;
            context.ProductSuppliers.Add(new ProductSupplier(sku, supplier.Id, lastCost, isDefault: true));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedPurchaseOrdersAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        // A purchase order has no business number in this demo, so its natural
        // key is the combination the seed controls: supplier, store and the set
        // of product SKUs. The record is inserted only when that combination is
        // absent; an order with the same key is considered already seeded.
        var existing = await context.PurchaseOrders
            .AsNoTracking()
            .Include(order => order.Lines)
            .Select(order => new
            {
                order.SupplierId,
                order.StoreId,
                Skus = order.Lines.Select(line => line.ProductSku).ToList()
            })
            .ToListAsync(cancellationToken);

        var keys = existing
            .Select(order => PurchaseOrderKey(order.SupplierId, order.StoreId, order.Skus))
            .ToHashSet(StringComparer.Ordinal);

        var orders = new List<PurchaseOrder>();

        void AddIfMissing(PurchaseOrder order)
        {
            var key = PurchaseOrderKey(
                order.SupplierId,
                order.StoreId,
                order.Lines.Select(line => line.ProductSku));

            if (keys.Add(key))
                orders.Add(order);
        }

        var suppliers = await context.Suppliers
            .ToDictionaryAsync(supplier => supplier.Nit, cancellationToken);

        var products = await context.Products
            .ToDictionaryAsync(product => product.Sku, cancellationToken);

        if (!suppliers.TryGetValue("900123456-1", out var andina)
            || !suppliers.TryGetValue("900654321-2", out var lacteos)
            || !suppliers.TryGetValue("901987654-3", out var aseo)
            || !suppliers.TryGetValue("900456789-4", out var bebidas)
            || !suppliers.TryGetValue("901234567-5", out var snacks))
        {
            return;
        }

        var draft = new PurchaseOrder(
            0,
            andina.Id,
            DefaultStoreId,
            DateTime.UtcNow.AddDays(-1),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));
        AddLine(draft, products, "SKU-1001", 24, 1600m);
        AddLine(draft, products, "SKU-1002", 18, 2300m);

        var pendingApproval = new PurchaseOrder(
            0,
            lacteos.Id,
            DefaultStoreId,
            DateTime.UtcNow.AddDays(-2),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)));
        AddLine(pendingApproval, products, "SKU-1005", 30, 3100m);
        pendingApproval.SubmitForApproval();

        var approved = new PurchaseOrder(
            0,
            aseo.Id,
            NorthStoreId,
            DateTime.UtcNow.AddDays(-3),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4)));
        AddLine(approved, products, "SKU-1010", 20, 6200m);
        AddLine(approved, products, "SKU-1012", 12, 8100m);
        approved.Approve();

        var sent = new PurchaseOrder(
            0,
            bebidas.Id,
            DefaultStoreId,
            DateTime.UtcNow.AddDays(-4),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));
        AddLine(sent, products, "SKU-1014", 16, 4300m);
        sent.Approve();
        sent.Send();

        var confirmed = new PurchaseOrder(
            0,
            snacks.Id,
            NorthStoreId,
            DateTime.UtcNow.AddDays(-5),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)));
        AddLine(confirmed, products, "SKU-1013", 25, 2500m);
        confirmed.Approve();
        confirmed.Send();
        confirmed.Confirm();

        var partiallyReceived = new PurchaseOrder(
            0,
            andina.Id,
            DefaultStoreId,
            DateTime.UtcNow.AddDays(-6),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));
        AddLine(partiallyReceived, products, "SKU-1008", 20, 3200m);
        AddLine(partiallyReceived, products, "SKU-1009", 15, 1400m);
        partiallyReceived.Approve();
        partiallyReceived.Send();
        partiallyReceived.ReceiveLine(
            "SKU-1008",
            8,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

        var received = new PurchaseOrder(
            0,
            lacteos.Id,
            DefaultStoreId,
            DateTime.UtcNow.AddDays(-7),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)));
        AddLine(received, products, "SKU-1006", 10, 9000m);
        received.Approve();
        received.Send();
        received.ReceiveLine(
            "SKU-1006",
            10,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)));

        var cancelled = new PurchaseOrder(
            0,
            aseo.Id,
            NorthStoreId,
            DateTime.UtcNow.AddDays(-8),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)));
        AddLine(cancelled, products, "SKU-1011", 10, 2100m);
        cancelled.Cancel();

        AddIfMissing(draft);
        AddIfMissing(pendingApproval);
        AddIfMissing(approved);
        AddIfMissing(sent);
        AddIfMissing(confirmed);
        AddIfMissing(partiallyReceived);
        AddIfMissing(received);
        AddIfMissing(cancelled);

        context.PurchaseOrders.AddRange(orders);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string PurchaseOrderKey(
        int supplierId,
        int storeId,
        IEnumerable<string> skus) =>
        $"{supplierId}|{storeId}|{string.Join(
            ',',
            skus.OrderBy(sku => sku, StringComparer.Ordinal))}";

    private static void AddLine(
        PurchaseOrder order,
        IReadOnlyDictionary<string, Product> products,
        string sku,
        int quantity,
        decimal unitCost)
    {
        if (products.ContainsKey(sku))
            order.AddLine(sku, quantity, unitCost);
    }

    private static async Task SeedInvoicesAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        var products = await context.Products
            .ToDictionaryAsync(product => product.Sku, cancellationToken);

        var issued = new[]
        {
            new SeedInvoice(
                "Cliente mostrador",
                "2222222222",
                0,
                new[] { new SeedInvoiceLine("SKU-1001", 2), new SeedInvoiceLine("SKU-1004", 1) }),
            new SeedInvoice(
                "María Fernanda Rojas",
                "1010101010",
                1,
                new[] { new SeedInvoiceLine("SKU-1002", 3), new SeedInvoiceLine("SKU-1003", 2) }),
            new SeedInvoice(
                "Jorge Iván Peña",
                "79876543",
                2,
                new[] { new SeedInvoiceLine("SKU-1005", 2), new SeedInvoiceLine("SKU-1008", 1) }),
            new SeedInvoice(
                "Camila Andrea Suárez",
                "1020304050",
                3,
                new[]
                {
                    new SeedInvoiceLine("SKU-1006", 1),
                    new SeedInvoiceLine("SKU-1010", 2),
                    new SeedInvoiceLine("SKU-1011", 3)
                }),
            new SeedInvoice(
                "Luis Alberto Gómez",
                "79123456",
                4,
                new[] { new SeedInvoiceLine("SKU-1007", 1), new SeedInvoiceLine("SKU-1013", 2) }),
            new SeedInvoice(
                "Natalia Pérez",
                "1030405060",
                5,
                new[] { new SeedInvoiceLine("SKU-1014", 2), new SeedInvoiceLine("SKU-1015", 3) })
        };

        // The customer document is the natural key of each demo invoice. The
        // previous index-based guard miscounted when an invoice was cancelled
        // or deleted, so the seeder now inserts only the documents that are
        // genuinely missing.
        var issuedDocuments = (await context.SalesInvoices
                .AsNoTracking()
                .Where(invoice => invoice.Status == InvoiceStatus.Issued)
                .Select(invoice => invoice.CustomerDocument)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var seed in issued)
        {
            if (!issuedDocuments.Add(seed.CustomerDocument))
                continue;

            context.SalesInvoices.Add(CreateInvoice(seed, products));
        }

        var cancelledSeed = new SeedInvoice(
            "Ricardo Molina",
            "80456789",
            2,
            new[] { new SeedInvoiceLine("SKU-1001", 1), new SeedInvoiceLine("SKU-1011", 2) });

        var cancelledExists = await context.SalesInvoices.AnyAsync(
            invoice => invoice.Status == InvoiceStatus.Cancelled
                && invoice.CustomerDocument == cancelledSeed.CustomerDocument,
            cancellationToken);

        if (!cancelledExists)
        {
            var invoice = CreateInvoice(cancelledSeed, products);
            invoice.Cancel();
            context.SalesInvoices.Add(invoice);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static SalesInvoice CreateInvoice(
        SeedInvoice seed,
        IReadOnlyDictionary<string, Product> products)
    {
        var invoice = new SalesInvoice(
            0,
            seed.CustomerName,
            seed.CustomerDocument,
            DateTime.UtcNow.AddDays(-seed.DaysAgo));

        foreach (var line in seed.Lines)
        {
            if (!products.TryGetValue(line.Sku, out var product))
                continue;

            invoice.AddLine(
                product.Sku,
                product.Name,
                line.Quantity,
                product.SalePrice);
        }

        return invoice;
    }

    private static async Task SeedSalesReturnsAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        var invoices = await context.SalesInvoices
            .AsNoTracking()
            .Include(invoice => invoice.Lines)
            .Where(invoice => invoice.Status == InvoiceStatus.Issued)
            .OrderBy(invoice => invoice.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
            return;

        // The demo owns at most one return per invoice, so the invoice id is
        // the natural key. Checking per invoice keeps the authorized return and
        // its stock re-entry from running twice.
        var invoicesWithReturn = (await context.SalesReturns
                .AsNoTracking()
                .Select(salesReturn => salesReturn.InvoiceId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var first = invoices[0];
        var firstLine = first.Lines.OrderBy(line => line.Id).FirstOrDefault();

        if (firstLine is not null && invoicesWithReturn.Add(first.Id))
        {
            var pending = new SalesReturn(
                0,
                first.Id,
                DefaultStoreId,
                first.CustomerName,
                first.CustomerDocument,
                "Producto vencido",
                DateTime.UtcNow.AddDays(-3));

            pending.AddLine(
                firstLine.ProductSku,
                firstLine.Description,
                Math.Min(1, firstLine.Quantity),
                firstLine.UnitPrice);

            context.SalesReturns.Add(pending);
        }

        SalesReturn? authorized = null;

        if (invoices.Count > 1)
        {
            var second = invoices[1];
            var secondLine = second.Lines.OrderBy(line => line.Id).FirstOrDefault();

            if (secondLine is not null && invoicesWithReturn.Add(second.Id))
            {
                authorized = new SalesReturn(
                    0,
                    second.Id,
                    DefaultStoreId,
                    second.CustomerName,
                    second.CustomerDocument,
                    "Empaque equivocado",
                    DateTime.UtcNow.AddDays(-2));

                authorized.AddLine(
                    secondLine.ProductSku,
                    secondLine.Description,
                    Math.Min(1, secondLine.Quantity),
                    secondLine.UnitPrice);

                context.SalesReturns.Add(authorized);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        if (authorized is null)
            return;

        var authorizedAt = DateTime.UtcNow.AddDays(-1);
        var line = authorized.Lines[0];

        authorized.Authorize(authorizedAt, $"NC-{authorized.Id:D6}");

        var stock = await context.Stocks.FirstOrDefaultAsync(
            item => item.StoreId == DefaultStoreId && item.Sku == line.ProductSku,
            cancellationToken);

        stock?.RegisterReturn(line.Quantity, authorizedAt);

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedInventoryCountsAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        // A count is identified by its store and location: the demo only ever
        // creates one per combination, independent of how many stock rows the
        // store has by the time the seeder runs.
        var existing = await context.InventoryCounts
            .AsNoTracking()
            .Select(count => new { count.StoreId, count.Location })
            .ToListAsync(cancellationToken);

        var keys = existing
            .Select(count => InventoryCountKey(count.StoreId, count.Location))
            .ToHashSet(StringComparer.Ordinal);

        var storeOneStock = await context.Stocks
            .AsNoTracking()
            .Where(item => item.StoreId == DefaultStoreId)
            .OrderBy(item => item.Sku)
            .Take(5)
            .ToListAsync(cancellationToken);

        if (storeOneStock.Count == 0)
            return;

        var open = new InventoryCount(
            0,
            DefaultStoreId,
            "Bodega principal",
            DateTime.UtcNow.AddDays(-2));

        foreach (var item in storeOneStock)
            open.AddLine(item.Sku, item.QuantityAvailable);

        // Leave one line with a difference so the demo shows the workflow:
        // open, partially counted, waiting for the operator to close it.
        var first = storeOneStock[0];
        open.SetCounted(first.Sku, Math.Max(0, first.QuantityAvailable - 2));

        if (keys.Add(InventoryCountKey(open.StoreId, open.Location)))
            context.InventoryCounts.Add(open);

        var northStock = await context.Stocks
            .AsNoTracking()
            .Where(item => item.StoreId == NorthStoreId)
            .OrderBy(item => item.Sku)
            .Take(3)
            .ToListAsync(cancellationToken);

        if (northStock.Count > 0)
        {
            var closed = new InventoryCount(
                0,
                NorthStoreId,
                null,
                DateTime.UtcNow.AddDays(-4));

            foreach (var item in northStock)
            {
                closed.AddLine(item.Sku, item.QuantityAvailable);
                closed.SetCounted(item.Sku, item.QuantityAvailable);
            }

            closed.Close(DateTime.UtcNow.AddDays(-3));

            if (keys.Add(InventoryCountKey(closed.StoreId, closed.Location)))
                context.InventoryCounts.Add(closed);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string InventoryCountKey(int storeId, string? location) =>
        $"{storeId}|{location?.Trim() ?? string.Empty}";

    private static async Task SeedInventoryTransfersAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        // A transfer has no business number, so the natural key is the route
        // plus the set of products it moves. The draft and the cancelled
        // transfer have different product sets, so both survive a second run.
        var existing = await context.InventoryTransfers
            .AsNoTracking()
            .Include(transfer => transfer.Lines)
            .Select(transfer => new
            {
                transfer.OriginStoreId,
                transfer.DestinationStoreId,
                Skus = transfer.Lines.Select(line => line.ProductSku).ToList()
            })
            .ToListAsync(cancellationToken);

        var keys = existing
            .Select(transfer => InventoryTransferKey(
                transfer.OriginStoreId,
                transfer.DestinationStoreId,
                transfer.Skus))
            .ToHashSet(StringComparer.Ordinal);

        var stock = await context.Stocks
            .AsNoTracking()
            .Where(item => item.StoreId == DefaultStoreId && item.QuantityAvailable > 0)
            .OrderBy(item => item.Sku)
            .Take(3)
            .ToListAsync(cancellationToken);

        if (stock.Count == 0)
            return;

        // A draft and a cancelled draft show every list state this seed needs
        // without touching the stock that the other demo screens display.
        var draft = new InventoryTransfer(
            0,
            DefaultStoreId,
            NorthStoreId,
            DateTime.UtcNow.AddDays(-1));

        foreach (var item in stock)
            draft.AddLine(item.Sku, Math.Min(item.QuantityAvailable, 2));

        var cancelled = new InventoryTransfer(
            0,
            DefaultStoreId,
            NorthStoreId,
            DateTime.UtcNow.AddDays(-3));
        cancelled.AddLine(stock[0].Sku, 1);
        cancelled.Cancel(DateTime.UtcNow.AddDays(-2));

        if (keys.Add(InventoryTransferKey(
                draft.OriginStoreId,
                draft.DestinationStoreId,
                draft.Lines.Select(line => line.ProductSku))))
        {
            context.InventoryTransfers.Add(draft);
        }

        if (keys.Add(InventoryTransferKey(
                cancelled.OriginStoreId,
                cancelled.DestinationStoreId,
                cancelled.Lines.Select(line => line.ProductSku))))
        {
            context.InventoryTransfers.Add(cancelled);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string InventoryTransferKey(
        int originStoreId,
        int destinationStoreId,
        IEnumerable<string> skus) =>
        $"{originStoreId}|{destinationStoreId}|{string.Join(
            ',',
            skus.OrderBy(sku => sku, StringComparer.Ordinal))}";

    private sealed record SeedStore(int Id, string Name, string Address);

    private sealed record SeedEmployee(
        string IdNumber,
        string FullName,
        string Position,
        int StoreId,
        decimal Salary);

    private sealed record SeedProduct(
        string Sku,
        string Barcode,
        string Name,
        decimal SalePrice,
        int StoreOneQuantity,
        int StoreOneMinimum,
        int StoreTwoQuantity,
        int StoreTwoMinimum,
        string Location,
        bool RegisterSale);

    private sealed record SeedSupplier(
        string Nit,
        string BusinessName,
        string Address,
        decimal Rating,
        bool Active);

    private sealed record SeedCustomer(
        string DocumentNumber,
        string FullName,
        string? Email,
        string? Phone,
        string? Address,
        bool Active);

    private sealed record SeedInvoice(
        string CustomerName,
        string CustomerDocument,
        int DaysAgo,
        SeedInvoiceLine[] Lines);

    private sealed record SeedInvoiceLine(string Sku, int Quantity);

    private sealed record SeedAccount(
        string IdNumber,
        string Email,
        string RoleName,
        string Password);
}
