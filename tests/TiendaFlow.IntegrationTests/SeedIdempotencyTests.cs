using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;
using TiendaFlow.Web.Persistence;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// The demo seeder must be idempotent per record, not per table. These tests
/// run the real seeder against the disposable SQL Server database twice and
/// also simulate an old database that is missing a few demo rows.
/// </summary>
public class SeedIdempotencyTests : IClassFixture<ErpWebApplicationFactory>, IAsyncLifetime
{
    private readonly ErpWebApplicationFactory _factory;

    public SeedIdempotencyTests(ErpWebApplicationFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.PrepareAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RunningTheSeederTwiceDoesNotDuplicateAnyRecord()
    {
        var before = await SnapshotAsync();

        await NewSeeder().StartAsync(CancellationToken.None);

        var after = await SnapshotAsync();

        Assert.Equal(before, after);
    }

    [Fact]
    public async Task TheSeederRepairsAnOldDatabaseWithoutDuplicatingTheRest()
    {
        var before = await SnapshotAsync();

        // An old table-wide guard could leave a database with some demo rows
        // and not others. Remove one row from four different sections.
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TiendaFlowDbContext>();

            var relation = await context.ProductSuppliers
                .OrderBy(item => item.ProductSku)
                .FirstAsync();
            context.ProductSuppliers.Remove(relation);

            var order = await context.PurchaseOrders
                .Include(item => item.Lines)
                .OrderBy(item => item.Id)
                .FirstAsync();
            context.PurchaseOrders.Remove(order);

            var cancelled = await context.SalesInvoices
                .Include(item => item.Lines)
                .FirstAsync(item => item.Status == InvoiceStatus.Cancelled);
            context.SalesInvoices.Remove(cancelled);

            var transfer = await context.InventoryTransfers
                .Include(item => item.Lines)
                .OrderBy(item => item.Id)
                .FirstAsync();
            context.InventoryTransfers.Remove(transfer);

            var terminatedContract = await context.EmployeeContracts
                .FirstAsync(item => item.Status == EmploymentContractStatus.Terminated);
            context.EmployeeContracts.Remove(terminatedContract);

            await context.SaveChangesAsync();
        }

        var damaged = await SnapshotAsync();
        Assert.NotEqual(before, damaged);

        await NewSeeder().StartAsync(CancellationToken.None);

        Assert.Equal(before, await SnapshotAsync());
    }

    private DevDataSeeder NewSeeder() =>
        new(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            _factory.Services.GetRequiredService<IPasswordHasher>(),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [DevDataSeeder.AdminPasswordKey] = _factory.AdminPassword,
                    [DevDataSeeder.DemoPasswordKey] = _factory.DemoPassword
                })
                .Build(),
            _factory.Services.GetRequiredService<ILogger<DevDataSeeder>>());

    private async Task<List<(string Table, int Count)>> SnapshotAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TiendaFlowDbContext>();

        return new List<(string, int)>
        {
            ("stores", await context.Stores.CountAsync()),
            ("roles", await context.Roles.CountAsync()),
            ("employees", await context.Employees.CountAsync()),
            ("users", await context.AppUsers.CountAsync()),
            ("products", await context.Products.CountAsync()),
            ("stock rows", await context.Stocks.CountAsync()),
            ("suppliers", await context.Suppliers.CountAsync()),
            ("product suppliers", await context.ProductSuppliers.CountAsync()),
            ("customers", await context.Customers.CountAsync()),
            ("purchase orders", await context.PurchaseOrders.CountAsync()),
            ("purchase order lines", await context.PurchaseOrderLines.CountAsync()),
            ("invoices", await context.SalesInvoices.CountAsync()),
            ("invoice lines", await context.InvoiceLines.CountAsync()),
            ("returns", await context.SalesReturns.CountAsync()),
            ("return lines", await context.SalesReturnLines.CountAsync()),
            ("counts", await context.InventoryCounts.CountAsync()),
            ("count lines", await context.InventoryCountLines.CountAsync()),
            ("transfers", await context.InventoryTransfers.CountAsync()),
            ("transfer lines", await context.InventoryTransferLines.CountAsync()),
            ("contracts", await context.EmployeeContracts.CountAsync()),
            ("contract attachments", await context.ContractAttachments.CountAsync())
        };
    }
}
