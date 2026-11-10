namespace TiendaFlow.Application.Security;

/// <summary>
/// Chooses the first page a signed-in user may open. The order is fixed so the
/// landing decision is deterministic and testable without the web host.
/// </summary>
public static class LandingPage
{
    public const string Fallback = "/inventario";

    private static readonly IReadOnlyList<(string Permission, string Route)> Candidates =
        new[]
        {
            (Permissions.InventoryStockView, "/inventario"),
            (Permissions.HrEmployeesView, "/rrhh/empleados"),
            (Permissions.SecurityUsersView, "/seguridad/usuarios")
        };

    public static string For(IReadOnlySet<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        foreach (var (permission, route) in Candidates)
        {
            if (permissions.Contains(permission))
                return route;
        }

        return Fallback;
    }
}
