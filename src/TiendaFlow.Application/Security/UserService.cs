using TiendaFlow.Domain;

namespace TiendaFlow.Application.Security;

public record CreateUserRequest(
    string Email,
    string FullName,
    string IdNumber,
    string Position,
    int StoreId,
    int RoleId,
    string Password,
    string ContractType = "Indefinido",
    decimal Salary = 0m);

public record UpdateUserRequest(
    string Email,
    string FullName,
    string IdNumber,
    string Position,
    int StoreId,
    int RoleId,
    bool Active,
    string? NewPassword = null);

/// <summary>
/// Administrative user management. Every operation demands the manage
/// permission, so a page, a button or a stray service call cannot bypass it.
/// </summary>
public sealed class UserService
{
    private readonly IAppUserRepository _users;
    private readonly IEmployeeRepository _employees;
    private readonly IStoreRepository _stores;
    private readonly IRoleRepository _roles;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;

    public UserService(
        IAppUserRepository users,
        IEmployeeRepository employees,
        IStoreRepository stores,
        IRoleRepository roles,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser)
    {
        _users = users;
        _employees = employees;
        _stores = stores;
        _roles = roles;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
    }

    public int CreateUser(CreateUserRequest request)
    {
        _accessControl.Demand(Permissions.SecurityUsersManage);

        var role = _roles.GetById(request.RoleId)
            ?? throw new InvalidUserException("El rol seleccionado no existe.");
        _ = _stores.GetById(request.StoreId)
            ?? throw new InvalidUserException("La tienda seleccionada no existe.");

        if (_users.ExistsEmail(request.Email))
            throw new DuplicateEmailException(request.Email);

        if (_employees.GetByIdNumber(request.IdNumber) is not null)
            throw new DuplicateIdNumberException(request.IdNumber);

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw new InvalidUserException("La contraseña debe tener al menos 8 caracteres.");

        var userId = 0;

        _unitOfWork.ExecuteInTransaction(() =>
        {
            var employee = new Employee(
                id: 0,
                storeId: request.StoreId,
                idNumber: request.IdNumber.Trim(),
                name: request.FullName.Trim(),
                position: request.Position.Trim(),
                contractType: request.ContractType,
                salary: request.Salary);

            _employees.Add(employee);

            var user = new AppUser(
                employee.Id,
                role.Id,
                request.Email,
                _passwordHasher.Hash(request.Password));

            _users.Add(user);
            userId = user.Id;

            Audit("UserCreated", user.Id.ToString(), $"Usuario {user.Id} creado con rol {role.Name}");
        });

        return userId;
    }

    public void UpdateUser(int userId, UpdateUserRequest request)
    {
        _accessControl.Demand(Permissions.SecurityUsersManage);

        var user = _users.GetById(userId) ?? throw new UserNotFoundException(userId);
        var role = _roles.GetById(request.RoleId)
            ?? throw new InvalidUserException("El rol seleccionado no existe.");
        _ = _stores.GetById(request.StoreId)
            ?? throw new InvalidUserException("La tienda seleccionada no existe.");

        if (!string.Equals(user.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase)
            && _users.ExistsEmail(request.Email))
        {
            throw new DuplicateEmailException(request.Email);
        }

        var employee = user.EmployeeId is int employeeId
            ? _employees.GetById(employeeId)
            : null;

        if (employee is null)
            throw new InvalidUserException("El usuario no tiene un empleado asociado.");

        var sameIdNumber = _employees.GetByIdNumber(request.IdNumber);
        if (sameIdNumber is not null && sameIdNumber.Id != employee.Id)
            throw new DuplicateIdNumberException(request.IdNumber);

        if (userId == _currentUser.UserId && !request.Active)
            throw new InvalidUserException("No puedes desactivar tu propia cuenta.");

        _unitOfWork.ExecuteInTransaction(() =>
        {
            employee.Update(
                request.StoreId,
                request.IdNumber.Trim(),
                request.FullName.Trim(),
                request.Position.Trim(),
                employee.ContractType,
                employee.Salary);

            _employees.Update(employee);

            user.ChangeEmail(request.Email);
            user.AssignRole(role.Id);

            if (request.Active)
                user.Activate();
            else
                user.Deactivate();

            if (!string.IsNullOrWhiteSpace(request.NewPassword))
            {
                if (request.NewPassword.Length < 8)
                    throw new InvalidUserException("La contraseña debe tener al menos 8 caracteres.");

                user.ChangePassword(_passwordHasher.Hash(request.NewPassword));
            }

            _users.Update(user);

            Audit("UserUpdated", user.Id.ToString(),
                $"Usuario {user.Id} actualizado; rol {role.Name}; activo {user.Active}");
        });
    }

    public void DeactivateUser(int userId)
    {
        _accessControl.Demand(Permissions.SecurityUsersManage);

        if (userId == _currentUser.UserId)
            throw new InvalidUserException("No puedes desactivar tu propia cuenta.");

        var user = _users.GetById(userId) ?? throw new UserNotFoundException(userId);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            user.Deactivate();
            _users.Update(user);
            Audit("UserDeactivated", user.Id.ToString(), $"Usuario {user.Id} desactivado");
        });
    }

    public void ResetPassword(int userId, string newPassword)
    {
        _accessControl.Demand(Permissions.SecurityUsersManage);

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            throw new InvalidUserException("La contraseña debe tener al menos 8 caracteres.");

        var user = _users.GetById(userId) ?? throw new UserNotFoundException(userId);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            user.ChangePassword(_passwordHasher.Hash(newPassword));
            _users.Update(user);
            Audit("PasswordReset", user.Id.ToString(), $"Contraseña restablecida para el usuario {user.Id}");
        });
    }

    private void Audit(string action, string entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId,
            detail,
            _currentUser.UserId,
            DateTime.UtcNow,
            module: "security",
            ip: _currentUser.Ip));
}

/// <summary>
/// Read side of user management, guarded by the view permission.
/// </summary>
public sealed class UserDirectoryService
{
    private readonly IUserDirectoryQuery _query;
    private readonly IRoleRepository _roles;
    private readonly IStoreRepository _stores;
    private readonly IAccessControl _accessControl;

    public UserDirectoryService(
        IUserDirectoryQuery query,
        IRoleRepository roles,
        IStoreRepository stores,
        IAccessControl accessControl)
    {
        _query = query;
        _roles = roles;
        _stores = stores;
        _accessControl = accessControl;
    }

    public IReadOnlyList<UserListItem> List()
    {
        _accessControl.Demand(Permissions.SecurityUsersView);
        return _query.List();
    }

    public UserDetail Get(int id)
    {
        _accessControl.Demand(Permissions.SecurityUsersView);
        return _query.Get(id) ?? throw new UserNotFoundException(id);
    }

    public UserFormOptions GetFormOptions()
    {
        _accessControl.Demand(Permissions.SecurityUsersView);
        return new UserFormOptions(
            _roles.List(),
            _stores.List().Where(store => store.Active).ToList());
    }
}

public record UserFormOptions(
    IReadOnlyList<Domain.Role> Roles,
    IReadOnlyList<Domain.Store> Stores);

public sealed class AuditTrailService
{
    private readonly IAuditTrailQuery _query;
    private readonly IAccessControl _accessControl;

    public AuditTrailService(IAuditTrailQuery query, IAccessControl accessControl)
    {
        _query = query;
        _accessControl = accessControl;
    }

    public IReadOnlyList<AuditTrailRow> Recent(int take = 200)
    {
        _accessControl.Demand(Permissions.SecurityAuditView);
        return _query.Recent(Math.Clamp(take, 1, 500));
    }
}

public sealed class RoleCatalogService
{
    private readonly IAccessControl _accessControl;

    public RoleCatalogService(IAccessControl accessControl) => _accessControl = accessControl;

    public RoleMatrix GetMatrix()
    {
        _accessControl.Demand(Permissions.SecurityRolesView);

        var roles = RoleNames.All;
        var rows = Permissions.All
            .Select(permission => new RoleMatrixRow(
                permission,
                roles.ToDictionary(
                    role => role,
                    role => RolePermissions.IsAllowed(role, permission),
                    StringComparer.Ordinal)))
            .ToList();

        return new RoleMatrix(roles, rows);
    }
}
