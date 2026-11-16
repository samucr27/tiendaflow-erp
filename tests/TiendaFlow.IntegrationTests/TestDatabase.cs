using Microsoft.Data.SqlClient;

namespace TiendaFlow.IntegrationTests;

internal static class TestDatabase
{
    public const string EnvironmentVariable = "TIENDAFLOW_TEST_CONNECTION";

    public static string? ConfiguredConnectionString =>
        Environment.GetEnvironmentVariable(EnvironmentVariable)
        ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

    public static bool CanConnect(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 3 };

            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static string CreateUniqueDatabaseConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = $"TiendaFlow_OpenCode_Test_{Guid.NewGuid():N}"
        };

        return builder.ConnectionString;
    }
}
