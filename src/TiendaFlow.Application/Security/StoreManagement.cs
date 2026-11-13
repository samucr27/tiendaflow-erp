using TiendaFlow.Domain;

namespace TiendaFlow.Application.Security;

public sealed record CreateStoreRequest(string Name, string Address, bool Active = true);

public sealed record UpdateStoreRequest(string Name, string Address);

/// <summary>
/// Operational facts that block the deactivation of a store. The read model is
/// intentionally small so the application service never depends on inventory or
/// purchasing repositories it does not own.
/// </summary>
public interface IStoreUsageQuery
{
    bool HasStock(int storeId);

    bool HasOpenPurchaseOrders(int storeId);

    bool HasActiveUsers(int storeId);
}

/// <summary>
/// Read side of the store administration screen: one row per store with the
/// counters the deactivation guard uses, plus the users and employees that can
/// be reassigned.
/// </summary>
public interface IStoreDirectoryQuery
{
    IReadOnlyList<StoreListItem> List();

    StoreDetail? Get(int id);
}

public sealed record StoreListItem(
    int Id,
    string Name,
    string Address,
    bool Active,
    int ActiveUsers,
    int EmployeeCount,
    int UnitsInStock,
    int OpenOrders);

public sealed record StoreUserAssignment(
    int UserId,
    string Name,
    string Email,
    string RoleLabel,
    int? StoreId,
    string StoreName,
    bool Active);

public sealed record StoreEmployeeAssignment(
    int EmployeeId,
    string Name,
    string IdNumber,
    string Position,
    int StoreId,
    string StoreName,
    bool Active);

public sealed record StoreDetail(
    StoreListItem Store,
    IReadOnlyList<StoreUserAssignment> Users,
    IReadOnlyList<StoreEmployeeAssignment> Employees);

/// <summary>
/// Store administration (Fase 1). Creating, renaming, activating and
/// deactivating a store requires <c>security.stores.manage</c>; assigning a
/// user or an employee to another store moves the employee record the user is
/// linked to, so both roles see the same store. Every operation is audited and
/// the deactivation is refused while the store still has stock, open purchase
/// orders or active users.
/// </summary>
public sealed class StoreService
{
    private readonly IStoreRepository _stores;
    private readonly IEmployeeRepository _employees;
    private readonly IAppUserRepository _users;
    private readonly IStoreUsageQuery _usage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;

    public StoreService(
        IStoreRepository stores,
        IEmployeeRepository employees,
        IAppUserRepository users,
        IStoreUsageQuery usage,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser)
    {
        _stores = stores;
        _employees = employees;
        _users = users;
        _usage = usage;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
    }

    public int CreateStore(CreateStoreRequest request)
    {
        _accessControl.Demand(Permissions.SecurityStoresManage);

        var name = NormalizeName(request.Name);

        if (_stores.ExistsName(name))
            throw new DuplicateStoreNameException(name);

        var store = new Store(
            _stores.NextId(),
            name,
            NormalizeAddress(request.Address));

        if (!request.Active)
            store.Deactivate();

        _unitOfWork.ExecuteInTransaction(() =>
        {
            _stores.Add(store);
            Audit(
                "StoreCreated",
                store.Id.ToString(),
                $"Tienda {store.Id} creada: {store.Name}; activa {store.Active}");
        });

        return store.Id;
    }

    public void UpdateStore(int storeId, UpdateStoreRequest request)
    {
        _accessControl.Demand(Permissions.SecurityStoresManage);

        var store = _stores.GetById(storeId) ?? throw new StoreNotFoundException(storeId);
        var name = NormalizeName(request.Name);

        if (_stores.ExistsName(name, storeId))
            throw new DuplicateStoreNameException(name);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            store.Rename(name);
            store.ChangeAddress(NormalizeAddress(request.Address));
            _stores.Update(store);
            Audit("StoreUpdated", store.Id.ToString(), $"Tienda {store.Id} actualizada: {store.Name}");
        });
    }

    public void ActivateStore(int storeId)
    {
        _accessControl.Demand(Permissions.SecurityStoresManage);

        var store = _stores.GetById(storeId) ?? throw new StoreNotFoundException(storeId);

        if (store.Active)
            return;

        _unitOfWork.ExecuteInTransaction(() =>
        {
            store.Activate();
            _stores.Update(store);
            Audit("StoreActivated", store.Id.ToString(), $"Tienda {store.Id} activada");
        });
    }

    public void DeactivateStore(int storeId)
    {
        _accessControl.Demand(Permissions.SecurityStoresManage);

        var store = _stores.GetById(storeId) ?? throw new StoreNotFoundException(storeId);

        if (!store.Active)
            return;

        // Checked in this order so the UI can explain the first blocking reason.
        if (_usage.HasStock(storeId))
            throw new StoreHasStockException(storeId);

        if (_usage.HasOpenPurchaseOrders(storeId))
            throw new StoreHasOpenOrdersException(storeId);

        if (_usage.HasActiveUsers(storeId))
            throw new StoreHasActiveUsersException(storeId);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            store.Deactivate();
            _stores.Update(store);
            Audit("StoreDeactivated", store.Id.ToString(), $"Tienda {store.Id} desactivada");
        });
    }

    public void AssignEmployeeToStore(int employeeId, int storeId)
    {
        _accessControl.Demand(Permissions.SecurityStoresManage);

        _ = ActiveStore(storeId);
        var employee = _employees.GetById(employeeId) ?? throw new EmployeeNotFoundException(employeeId);

        if (employee.StoreId == storeId)
            return;

        _unitOfWork.ExecuteInTransaction(() =>
        {
            MoveEmployee(employee, storeId);
            Audit(
                "StoreEmployeeAssigned",
                storeId.ToString(),
                $"Empleado {employeeId} asignado a la tienda {storeId}");
        });
    }

    public void AssignUserToStore(int userId, int storeId)
    {
        _accessControl.Demand(Permissions.SecurityStoresManage);

        _ = ActiveStore(storeId);
        var user = _users.GetById(userId) ?? throw new UserNotFoundException(userId);

        if (user.EmployeeId is not int employeeId)
            throw new InvalidStoreException("El usuario no tiene un empleado asociado.");

        var employee = _employees.GetById(employeeId) ?? throw new EmployeeNotFoundException(employeeId);

        if (employee.StoreId == storeId)
            return;

        _unitOfWork.ExecuteInTransaction(() =>
        {
            MoveEmployee(employee, storeId);
            Audit(
                "StoreUserAssigned",
                storeId.ToString(),
                $"Usuario {userId} asignado a la tienda {storeId}");
        });
    }

    private Store ActiveStore(int storeId)
    {
        var store = _stores.GetById(storeId) ?? throw new StoreNotFoundException(storeId);

        if (!store.Active)
            throw new InvalidStoreException("No se puede asignar personal a una tienda inactiva.");

        return store;
    }

    private void MoveEmployee(Employee employee, int storeId)
    {
        employee.Update(
            storeId,
            employee.IdNumber,
            employee.Name,
            employee.Position,
            employee.ContractType,
            employee.Salary);

        _employees.Update(employee);
    }

    private static string NormalizeName(string name) => (name ?? string.Empty).Trim();

    private static string NormalizeAddress(string address) => (address ?? string.Empty).Trim();

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
/// Read-only store directory, guarded by <c>security.stores.view</c>. The
/// corporate manager can consult the stores without receiving the manage
/// permission.
/// </summary>
public sealed class StoreDirectoryService
{
    private readonly IStoreDirectoryQuery _query;
    private readonly IAccessControl _accessControl;

    public StoreDirectoryService(IStoreDirectoryQuery query, IAccessControl accessControl)
    {
        _query = query;
        _accessControl = accessControl;
    }

    public IReadOnlyList<StoreListItem> List()
    {
        _accessControl.Demand(Permissions.SecurityStoresView);
        return _query.List();
    }

    public StoreDetail Get(int id)
    {
        _accessControl.Demand(Permissions.SecurityStoresView);
        return _query.Get(id) ?? throw new StoreNotFoundException(id);
    }
}
