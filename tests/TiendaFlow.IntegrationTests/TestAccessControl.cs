using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// The SQL Server tests exercise persistence and transactions. Authorization is
/// covered by the unit tests, so these tests allow every operation.
/// </summary>
internal sealed class AllowAllAccessControl : IAccessControl
{
    public bool Has(string permission) => true;

    public void Demand(string permission)
    {
    }
}

/// <summary>
/// Minimal authenticated user for containers that need ICurrentUser but never
/// exercise authorization.
/// </summary>
internal sealed class TestCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => true;

    public int? UserId => 1;

    public string Name => "Integration Test";

    public string Email => "integration@tiendaflow.local";

    public string Role => RoleNames.Administrator;

    public string RoleLabel => "Administrador";

    public int? StoreId => 1;

    public string StoreName => "Tienda Centro";

    public string? Ip => "127.0.0.1";

    public IReadOnlySet<string> Permissions { get; } =
        TiendaFlow.Application.Security.Permissions.All.ToHashSet(StringComparer.Ordinal);
}
