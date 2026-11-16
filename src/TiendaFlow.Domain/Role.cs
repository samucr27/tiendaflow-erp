namespace TiendaFlow.Domain;

/// <summary>
/// The names of the seven roles defined by the system. Role ids are fixed by
/// the development seed and by the role-permission matrix in the Application.
/// </summary>
public static class RoleNames
{
    public const string Administrator = "Administrator";
    public const string CorporateManager = "Corporate Manager";
    public const string StoreManager = "Store Manager";
    public const string Cashier = "Cashier";
    public const string StockClerk = "Stock Clerk";
    public const string LogisticsCoordinator = "Logistics Coordinator";
    public const string HumanResourcesOfficer = "HR Officer";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Administrator,
        CorporateManager,
        StoreManager,
        Cashier,
        StockClerk,
        LogisticsCoordinator,
        HumanResourcesOfficer
    };
}

public class Role
{
    public Role(int id, string name)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), "Role id must be positive.");
        RequireText(name, "Role name");

        Id = id;
        Name = name;
    }

    public int Id { get; }

    public string Name { get; private set; }

    public void Rename(string name)
    {
        RequireText(name, "Role name");
        Name = name;
    }

    private static void RequireText(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{field} is required.", nameof(value));
    }
}
