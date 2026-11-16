using TiendaFlow.Domain;

namespace TiendaFlow.Application.Security;

public record AuthenticatedUser(
    int Id,
    string Email,
    string FullName,
    int RoleId,
    string RoleName,
    int? StoreId,
    string StoreName,
    IReadOnlySet<string> Permissions);

/// <summary>
/// Verifies credentials for the login endpoint. It knows nothing about cookies:
/// the web layer turns the result into claims and a cookie.
/// </summary>
public sealed class AuthenticationService
{
    private readonly IAppUserRepository _users;
    private readonly IEmployeeRepository _employees;
    private readonly IStoreRepository _stores;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLog _auditLog;

    public AuthenticationService(
        IAppUserRepository users,
        IEmployeeRepository employees,
        IStoreRepository stores,
        IPasswordHasher passwordHasher,
        IAuditLog auditLog)
    {
        _users = users;
        _employees = employees;
        _stores = stores;
        _passwordHasher = passwordHasher;
        _auditLog = auditLog;
    }

    public AuthenticatedUser? SignIn(string email, string password, string? ip)
    {
        var user = string.IsNullOrWhiteSpace(email)
            ? null
            : _users.GetByEmail(email.Trim());

        if (user is null || !user.Active || !_passwordHasher.Verify(user.PasswordHash, password))
        {
            AuditFailure(email, ip);
            return null;
        }

        var roleName = SystemRoles.NameFor(user.RoleId);
        var employee = user.EmployeeId is int employeeId ? _employees.GetById(employeeId) : null;
        var store = employee is null ? null : _stores.GetById(employee.StoreId);

        var authenticated = new AuthenticatedUser(
            user.Id,
            user.Email,
            employee?.Name ?? user.Email,
            user.RoleId,
            roleName,
            employee?.StoreId,
            store?.Name ?? "Sin tienda",
            RolePermissions.ForRole(roleName));

        _auditLog.Record(new AuditEntry(
            "LoginSucceeded",
            user.Id.ToString(),
            "Inicio de sesión correcto",
            user.Id,
            DateTime.UtcNow,
            module: "security",
            ip: ip));

        return authenticated;
    }

    private void AuditFailure(string? email, string? ip)
    {
        var entityId = string.IsNullOrWhiteSpace(email)
            ? "desconocido"
            : email.Trim();

        if (entityId.Length > 50)
            entityId = entityId[..50];

        _auditLog.Record(new AuditEntry(
            "LoginFailed",
            entityId,
            "Intento de inicio de sesión fallido",
            null,
            DateTime.UtcNow,
            module: "security",
            ip: ip));
    }
}
