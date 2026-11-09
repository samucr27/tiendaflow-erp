using TiendaFlow.Domain;

namespace TiendaFlow.Application.Security;

/// <summary>
/// Enforces the role matrix at the application-service boundary. A denied
/// attempt is written to the audit log with the authenticated user id before
/// the exception reaches the UI.
/// </summary>
public sealed class AccessControl : IAccessControl
{
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLog _auditLog;

    public AccessControl(ICurrentUser currentUser, IAuditLog auditLog)
    {
        _currentUser = currentUser;
        _auditLog = auditLog;
    }

    public bool Has(string permission) =>
        _currentUser.IsAuthenticated && _currentUser.Permissions.Contains(permission);

    public void Demand(string permission)
    {
        if (Has(permission))
            return;

        if (_currentUser.UserId is int userId)
        {
            _auditLog.Record(new AuditEntry(
                "AccessDenied",
                permission,
                $"Acceso denegado a la función {permission}",
                userId,
                DateTime.UtcNow,
                module: "security",
                ip: _currentUser.Ip));
        }

        throw new AccessDeniedException(permission);
    }
}
