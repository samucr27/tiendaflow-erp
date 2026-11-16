using Microsoft.EntityFrameworkCore;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    private DbContextOptions<TiendaFlowDbContext>? _options;
    private string? _connectionString;

    public string? ConnectionString => _connectionString;

    public async Task InitializeAsync()
    {
        var configuredConnectionString = TestDatabase.ConfiguredConnectionString;
        if (string.IsNullOrWhiteSpace(configuredConnectionString)
            || !TestDatabase.CanConnect(configuredConnectionString))
        {
            // The SqlServerFact tests skip in this situation; nothing to set up.
            return;
        }

        var connectionString = TestDatabase.CreateUniqueDatabaseConnectionString(configuredConnectionString);
        _connectionString = connectionString;

        _options = new DbContextOptionsBuilder<TiendaFlowDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new TiendaFlowDbContext(_options);
        await context.Database.MigrateAsync();
        await SeedSecurityReferencesAsync(context);
    }

    /// <summary>
    /// Audit rows reference app_user, and some legacy tests use known user ids.
    /// These rows keep those tests realistic without weakening the foreign key.
    /// </summary>
    private static async Task SeedSecurityReferencesAsync(TiendaFlowDbContext context)
    {
        const string sql = """
            INSERT INTO [store] ([id], [name], [address], [active])
            VALUES (1, N'Tienda Centro', N'Calle 10 # 20-30', 1);

            INSERT INTO [role] ([id], [name]) VALUES
                (1, N'Administrator'),
                (2, N'Corporate Manager'),
                (3, N'Store Manager'),
                (4, N'Cashier'),
                (5, N'Stock Clerk'),
                (6, N'Logistics Coordinator'),
                (7, N'HR Officer');

            SET IDENTITY_INSERT [app_user] ON;
            INSERT INTO [app_user] ([id], [employee_id], [role_id], [email], [password_hash], [two_factor], [active])
            VALUES
                (1, NULL, 1, N'integration1@tiendaflow.local', N'test-only-hash', 0, 1),
                (2, NULL, 2, N'integration2@tiendaflow.local', N'test-only-hash', 0, 1),
                (3, NULL, 3, N'integration3@tiendaflow.local', N'test-only-hash', 0, 1),
                (4, NULL, 4, N'integration4@tiendaflow.local', N'test-only-hash', 0, 1),
                (5, NULL, 5, N'integration5@tiendaflow.local', N'test-only-hash', 0, 1),
                (6, NULL, 6, N'integration6@tiendaflow.local', N'test-only-hash', 0, 1),
                (7, NULL, 7, N'integration7@tiendaflow.local', N'test-only-hash', 0, 1),
                (8, NULL, 1, N'integration8@tiendaflow.local', N'test-only-hash', 0, 1),
                (9, NULL, 1, N'integration9@tiendaflow.local', N'test-only-hash', 0, 1);
            SET IDENTITY_INSERT [app_user] OFF;
            """;

        await context.Database.ExecuteSqlRawAsync(sql);
    }

    public async Task DisposeAsync()
    {
        if (_options is null)
            return;

        await using var context = new TiendaFlowDbContext(_options);

        try
        {
            await context.Database.EnsureDeletedAsync();
        }
        catch (Exception)
        {
            // Best-effort cleanup: a missing database must not fail the test run.
        }
    }

    public TiendaFlowDbContext CreateContext() =>
        new(_options ?? throw new InvalidOperationException(
            $"Set {TestDatabase.EnvironmentVariable} to run SQL Server integration tests."));
}
