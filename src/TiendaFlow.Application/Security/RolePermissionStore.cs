using TiendaFlow.Domain;

namespace TiendaFlow.Application.Security;

/// <summary>
/// Stored permissions of the roles an administrator edited. A role without
/// stored rows keeps the default matrix defined in <see cref="RolePermissions"/>.
/// </summary>
public interface IRolePermissionStore
{
    IReadOnlySet<string>? StoredFor(int roleId);

    void Replace(int roleId, IReadOnlySet<string> permissions);

    void Clear(int roleId);
}

public static class EffectivePermissions
{
    public static IReadOnlySet<string> For(string roleName, IRolePermissionStore? store) =>
        store?.StoredFor(SystemRoles.IdFor(roleName)) ?? RolePermissions.ForRole(roleName);
}

public record RolePermissionChange(string RoleName, IReadOnlySet<string> Permissions);

/// <summary>
/// Edits the role matrix from /seguridad/roles. The Administrator role always
/// keeps the permissions that let someone manage users and roles, so the
/// system can never be locked out. Changes apply at the next sign-in.
/// </summary>
public sealed class RoleEditorService
{
    public static readonly IReadOnlySet<string> LockoutGuard = new HashSet<string>(StringComparer.Ordinal)
    {
        Permissions.SecurityUsersView,
        Permissions.SecurityUsersManage,
        Permissions.SecurityRolesView,
        Permissions.SecurityRolesManage
    };

    private readonly IRolePermissionStore _store;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;

    public RoleEditorService(
        IRolePermissionStore store,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser)
    {
        _store = store;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
    }

    public bool IsCustomized(string roleName)
    {
        _accessControl.Demand(Permissions.SecurityRolesView);
        return _store.StoredFor(SystemRoles.IdFor(roleName)) is not null;
    }

    public void Update(RolePermissionChange change)
    {
        _accessControl.Demand(Permissions.SecurityRolesManage);

        var roleId = SystemRoles.IdFor(change.RoleName);
        if (change.Permissions.Count == 0)
            throw new InvalidUserException("Un rol debe conservar al menos un permiso.");

        var unknown = change.Permissions.Where(permission => !Permissions.All.Contains(permission)).ToList();
        if (unknown.Count > 0)
            throw new InvalidUserException($"Permisos desconocidos: {string.Join(", ", unknown)}.");

        if (change.RoleName == RoleNames.Administrator && !LockoutGuard.IsSubsetOf(change.Permissions))
        {
            throw new InvalidUserException(
                "El rol Administrador debe conservar la gestión de usuarios y de roles para no bloquear el sistema.");
        }

        var before = EffectivePermissions.For(change.RoleName, _store);
        var added = change.Permissions.Except(before).OrderBy(code => code).ToList();
        var removed = before.Except(change.Permissions).OrderBy(code => code).ToList();

        if (added.Count == 0 && removed.Count == 0)
            return;

        _unitOfWork.ExecuteInTransaction(() =>
        {
            _store.Replace(roleId, change.Permissions);
            Audit("RolePermissionsUpdated", roleId,
                $"Rol {change.RoleName}: +[{string.Join(", ", added)}] -[{string.Join(", ", removed)}]");
        });
    }

    public void Reset(string roleName)
    {
        _accessControl.Demand(Permissions.SecurityRolesManage);

        var roleId = SystemRoles.IdFor(roleName);
        _unitOfWork.ExecuteInTransaction(() =>
        {
            _store.Clear(roleId);
            Audit("RolePermissionsReset", roleId, $"Rol {roleName} vuelve a la matriz por defecto");
        });
    }

    private void Audit(string action, int roleId, string detail)
    {
        var text = detail.Length > 480 ? detail[..480] : detail;
        _auditLog.Record(new AuditEntry(
            action,
            roleId.ToString(),
            text,
            _currentUser.UserId,
            DateTime.UtcNow,
            module: "security",
            ip: _currentUser.Ip));
    }
}
