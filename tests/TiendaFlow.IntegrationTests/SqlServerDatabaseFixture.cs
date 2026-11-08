using Microsoft.EntityFrameworkCore;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    private DbContextOptions<TiendaFlowDbContext>? _options;

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

        _options = new DbContextOptionsBuilder<TiendaFlowDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new TiendaFlowDbContext(_options);
        await context.Database.MigrateAsync();
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
