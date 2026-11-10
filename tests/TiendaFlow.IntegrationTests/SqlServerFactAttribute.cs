namespace TiendaFlow.IntegrationTests;

/// <summary>
/// A fact that only runs when a SQL Server connection string is configured and
/// reachable. Otherwise it is reported as skipped, so CI without SQL Server
/// remains green.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        var connectionString = TestDatabase.ConfiguredConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Skip = $"Set {TestDatabase.EnvironmentVariable} to run SQL Server integration tests.";
            return;
        }

        if (!TestDatabase.CanConnect(connectionString))
            Skip = "SQL Server is not reachable.";
    }
}
