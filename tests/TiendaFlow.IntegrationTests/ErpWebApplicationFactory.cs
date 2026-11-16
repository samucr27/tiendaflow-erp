using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TiendaFlow.Application.Security;
using TiendaFlow.Infrastructure.Persistence;
using TiendaFlow.Web.Persistence;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// Hosts the real web pipeline against a disposable SQL Server database. The
/// database is migrated and the demo seeder runs with random passwords, so the
/// smoke tests sign in through the real login form without storing any test
/// credential in the repository.
/// </summary>
public sealed class ErpWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly (string Email, bool IsAdmin)[] Accounts =
    {
        ("admin@tiendaflow.local", true),
        ("gerente@tiendaflow.local", false),
        ("jefe.tienda@tiendaflow.local", false),
        ("cajero@tiendaflow.local", false),
        ("bodega@tiendaflow.local", false),
        ("logistica@tiendaflow.local", false),
        ("rrhh@tiendaflow.local", false)
    };

    private readonly string _connectionString;
    private readonly SemaphoreSlim _preparation = new(1, 1);
    private bool _prepared;
    private bool _disposed;

    public ErpWebApplicationFactory()
    {
        var configured = TestDatabase.ConfiguredConnectionString
            ?? throw new InvalidOperationException(
                $"Set {TestDatabase.EnvironmentVariable} to run the web smoke tests.");

        _connectionString = TestDatabase.CreateUniqueDatabaseConnectionString(configured);
        AdminPassword = NewPassword();
        DemoPassword = NewPassword();
    }

    public string AdminPassword { get; }

    public string DemoPassword { get; }

    public CapturingLoggerProvider Log { get; } = new();

    /// <summary>
    /// Migrates and seeds the disposable database, then replaces the password
    /// hashes with the random values this factory generated. The hosted seeder
    /// runs before the in-memory configuration wins, so it may read the local
    /// user secrets; resetting the hashes keeps the smoke test independent of
    /// whatever a developer has configured locally.
    /// </summary>
    public async Task PrepareAsync()
    {
        await _preparation.WaitAsync();

        try
        {
            if (_prepared)
                return;

            using var scope = Services.CreateScope();
            var provider = scope.ServiceProvider;
            var context = provider.GetRequiredService<TiendaFlowDbContext>();

            await context.Database.MigrateAsync();

            if (!await context.AppUsers.AnyAsync())
            {
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        [DevDataSeeder.AdminPasswordKey] = AdminPassword,
                        [DevDataSeeder.DemoPasswordKey] = DemoPassword
                    })
                    .Build();

                var seeder = new DevDataSeeder(
                    Services.GetRequiredService<IServiceScopeFactory>(),
                    provider.GetRequiredService<IPasswordHasher>(),
                    configuration,
                    provider.GetRequiredService<ILogger<DevDataSeeder>>());

                await seeder.StartAsync(CancellationToken.None);
            }

            var hasher = provider.GetRequiredService<IPasswordHasher>();

            foreach (var account in Accounts)
            {
                var user = await context.AppUsers.SingleAsync(candidate => candidate.Email == account.Email);
                var password = account.IsAdmin ? AdminPassword : DemoPassword;

                if (!hasher.Verify(user.PasswordHash, password))
                    user.ChangePassword(hasher.Hash(password));
            }

            await context.SaveChangesAsync();
            _prepared = true;
        }
        finally
        {
            _preparation.Release();
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.AddProvider(Log));

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Database:MigrateOnStartup"] = "true",
                ["Seed:Enabled"] = "true",
                ["Seed:AdminPassword"] = AdminPassword,
                ["Seed:DemoPassword"] = DemoPassword
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing || _disposed)
            return;

        _disposed = true;
        _preparation.Dispose();

        try
        {
            var builder = new SqlConnectionStringBuilder(_connectionString)
            {
                InitialCatalog = "master"
            };

            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = $"""
                IF DB_ID(N'{DatabaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{DatabaseName}];
                END
                """;
            command.ExecuteNonQuery();
        }
        catch (Exception)
        {
            // Best-effort cleanup: a missing database must not fail the suite.
        }
    }

    private string DatabaseName =>
        new SqlConnectionStringBuilder(_connectionString).InitialCatalog;

    private static string NewPassword() =>
        $"Tf-{Guid.NewGuid():N}-1!";
}
