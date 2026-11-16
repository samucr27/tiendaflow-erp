using TiendaFlow.Domain;

namespace TiendaFlow.Application.Security;

/// <summary>
/// Fixed identifiers for the seven roles. The values match the seed data and
/// the role table, so the database never has to guess which role is which.
/// </summary>
public static class SystemRoles
{
    public static readonly IReadOnlyDictionary<string, int> Ids =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [RoleNames.Administrator] = 1,
            [RoleNames.CorporateManager] = 2,
            [RoleNames.StoreManager] = 3,
            [RoleNames.Cashier] = 4,
            [RoleNames.StockClerk] = 5,
            [RoleNames.LogisticsCoordinator] = 6,
            [RoleNames.HumanResourcesOfficer] = 7
        };

    public static int IdFor(string roleName) =>
        Ids.TryGetValue(roleName, out var id)
            ? id
            : throw new ArgumentOutOfRangeException(nameof(roleName), roleName, "Unknown role.");

    public static string NameFor(int roleId) =>
        Ids.FirstOrDefault(pair => pair.Value == roleId).Key
        ?? throw new ArgumentOutOfRangeException(nameof(roleId), roleId, "Unknown role id.");

    public static string Label(string roleName) => roleName switch
    {
        RoleNames.Administrator => "Administrador",
        RoleNames.CorporateManager => "Gerente corporativo",
        RoleNames.StoreManager => "Jefe de tienda",
        RoleNames.Cashier => "Cajero",
        RoleNames.StockClerk => "Auxiliar de bodega",
        RoleNames.LogisticsCoordinator => "Coordinador de logística",
        RoleNames.HumanResourcesOfficer => "Gestor de recursos humanos",
        _ => roleName
    };

    public static string LabelFor(int roleId) => Label(NameFor(roleId));
}
