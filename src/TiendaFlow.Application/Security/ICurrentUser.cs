namespace TiendaFlow.Application.Security;

/// <summary>
/// The authenticated user available to application services. Implementations
/// read claims from the web request; tests provide their own implementation.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    int? UserId { get; }

    string Name { get; }

    string Email { get; }

    string Role { get; }

    string RoleLabel { get; }

    int? StoreId { get; }

    string StoreName { get; }

    string? Ip { get; }

    IReadOnlySet<string> Permissions { get; }
}

public interface IAccessControl
{
    bool Has(string permission);

    void Demand(string permission);
}
