namespace TiendaFlow.Infrastructure;

/// <summary>
/// Picks the SQL Server connection string. The DB_CONNECTION_STRING
/// environment variable wins, so the same build runs unchanged on Azure App
/// Service, AWS Elastic Beanstalk or a Docker container; otherwise the usual
/// ConnectionStrings:DefaultConnection setting (user-secrets, App Service
/// connection string) is used.
/// </summary>
public static class DatabaseConnection
{
    public const string EnvironmentVariable = "DB_CONNECTION_STRING";

    public static string? Resolve(string? environmentValue, string? defaultConnection)
    {
        if (!string.IsNullOrWhiteSpace(environmentValue))
            return environmentValue.Trim();

        return string.IsNullOrWhiteSpace(defaultConnection) ? null : defaultConnection;
    }
}
