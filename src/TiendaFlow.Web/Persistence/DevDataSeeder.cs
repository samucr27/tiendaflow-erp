using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.Web.Persistence;

/// <summary>
/// Seeds the catalog, the store, the seven roles and one demo user per role.
/// Registered only in the Development environment (see Program.cs).
/// Passwords are read from user-secrets or configuration and are never stored
/// in this file, in logs or in the repository.
/// </summary>
public sealed class DevDataSeeder : IHostedService
{
    public const string AdminPasswordKey = "Seed:AdminPassword";
    public const string DemoPasswordKey = "Seed:DemoPassword";

    private const int DefaultStoreId = 1;

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

        await context.Database.MigrateAsync(cancellationToken);

        await SeedStoreAsync(context, cancellationToken);
        await SeedRolesAsync(context, cancellationToken);
        await SeedUsersAsync(context, cancellationToken);
        await SeedCatalogAsync(context, cancellationToken);
        await SeedSuppliersAsync(context, cancellationToken);
        await SeedInvoicesAsync(context, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task SeedStoreAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        if (await context.Stores.AnyAsync(cancellationToken))
            return;

        context.Stores.Add(new Store(
            DefaultStoreId,
            "Tienda Centro",
            "Calle 10 # 20-30, Bogotá"));

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedRolesAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        if (await context.Roles.AnyAsync(cancellationToken))
            return;

        context.Roles.AddRange(RoleNames.All.Select(name =>
            new Role(SystemRoles.IdFor(name), name)));

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedUsersAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        if (await context.AppUsers.AnyAsync(cancellationToken))
            return;

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
            new(
                "Administrador del sistema",
                "admin@tiendaflow.local",
                "1000000001",
                "Administrador",
                RoleNames.Administrator,
                adminPassword),
            new(
                "Laura Gómez",
                "gerente@tiendaflow.local",
                "1000000002",
                "Gerente corporativo",
                RoleNames.CorporateManager,
                demoPassword),
            new(
                "Carlos Ramírez",
                "jefe.tienda@tiendaflow.local",
                "1000000003",
                "Jefe de tienda",
                RoleNames.StoreManager,
                demoPassword),
            new(
                "Ana Torres",
                "cajero@tiendaflow.local",
                "1000000004",
                "Cajero",
                RoleNames.Cashier,
                demoPassword),
            new(
                "Diego Herrera",
                "bodega@tiendaflow.local",
                "1000000005",
                "Auxiliar de bodega",
                RoleNames.StockClerk,
                demoPassword),
            new(
                "Sofía Rojas",
                "logistica@tiendaflow.local",
                "1000000006",
                "Coordinador de logística",
                RoleNames.LogisticsCoordinator,
                demoPassword),
            new(
                "Marcela Díaz",
                "rrhh@tiendaflow.local",
                "1000000007",
                "Gestor de recursos humanos",
                RoleNames.HumanResourcesOfficer,
                demoPassword)
        };

        var employees = accounts.Select(account => new Employee(
            id: 0,
            storeId: DefaultStoreId,
            idNumber: account.IdNumber,
            name: account.FullName,
            position: account.Position,
            contractType: "Indefinido",
            salary: 0m)).ToList();

        context.Employees.AddRange(employees);
        await context.SaveChangesAsync(cancellationToken);

        var users = accounts.Select((account, index) => new AppUser(
            employees[index].Id,
            SystemRoles.IdFor(account.RoleName),
            account.Email,
            _passwordHasher.Hash(account.Password))).ToList();

        context.AppUsers.AddRange(users);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded {UserCount} development users: admin plus one user per role.",
            users.Count);
    }

    private static async Task SeedCatalogAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        if (await context.Products.AnyAsync(cancellationToken))
            return;

        context.Products.AddRange(
            new Product("SKU-1001", "7701234567001", "Agua 600 ml", 2500m),
            new Product("SKU-1002", "7701234567002", "Gaseosa 400 ml", 3500m),
            new Product("SKU-1003", "7701234567003", "Arroz 500 g", 3300m),
            new Product("SKU-1004", "7701234567004", "Pan tajado", 5500m));

        context.Stocks.AddRange(
            new StockItem("SKU-1001", DefaultStoreId, 40, "Pasillo 1", minStock: 10),
            new StockItem("SKU-1002", DefaultStoreId, 25, "Pasillo 1", minStock: 8),
            new StockItem("SKU-1003", DefaultStoreId, 18, "Pasillo 2", minStock: 6),
            new StockItem("SKU-1004", DefaultStoreId, 12, "Pasillo 3", minStock: 5));

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedSuppliersAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        if (await context.Suppliers.AnyAsync(cancellationToken))
            return;

        context.Suppliers.AddRange(
            new Supplier(0, "900123456-1", "Distribuidora Andina S.A.S.", "Calle 13 # 45-20, Bogotá"),
            new Supplier(0, "900654321-2", "Lácteos del Altiplano Ltda.", "Carrera 7 # 12-34, Tunja"),
            new Supplier(0, "901987654-3", "Aseo Total Colombia S.A.S.", "Avenida 68 # 22-10, Bogotá"));

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedInvoicesAsync(
        TiendaFlowDbContext context,
        CancellationToken cancellationToken)
    {
        if (await context.SalesInvoices.AnyAsync(cancellationToken))
            return;

        var counter = new SalesInvoice(
            id: 0,
            customerName: "Cliente mostrador",
            customerDocument: "2222222222",
            issueDate: DateTime.UtcNow);
        counter.AddLine("SKU-1001", "Agua 600 ml", 2, 2500m);
        counter.AddLine("SKU-1004", "Pan tajado", 1, 5500m);

        var neighbor = new SalesInvoice(
            id: 0,
            customerName: "María Fernanda Rojas",
            customerDocument: "1010101010",
            issueDate: DateTime.UtcNow.AddDays(-1));
        neighbor.AddLine("SKU-1002", "Gaseosa 400 ml", 3, 3500m);
        neighbor.AddLine("SKU-1003", "Arroz 500 g", 2, 3300m);

        context.SalesInvoices.AddRange(counter, neighbor);
        await context.SaveChangesAsync(cancellationToken);
    }

    private sealed record SeedAccount(
        string FullName,
        string Email,
        string IdNumber,
        string Position,
        string RoleName,
        string Password);
}
