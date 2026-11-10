using TiendaFlow.Application.Payroll;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public sealed record EmployeeRow(
    int Id,
    string Name,
    string IdNumber,
    string Position,
    string ContractType,
    decimal? Salary,
    bool Active,
    int StoreId,
    string StoreName);

public sealed record CreateEmployeeRequest(
    string IdNumber,
    string Name,
    string Position,
    int StoreId,
    string ContractType,
    decimal Salary);

public sealed record UpdateEmployeeRequest(
    string IdNumber,
    string Name,
    string Position,
    int StoreId,
    string ContractType,
    decimal Salary,
    bool Active);

/// <summary>
/// Employee registry behind the HR screens. Reading works with the view
/// permission, but the salary is only exposed to callers allowed to manage
/// employees (Ley 1581 de 2012). Every operation enforces its permission
/// here, not only in the page or the navigation menu.
/// </summary>
public sealed class EmployeeService
{
    private readonly IEmployeeRepository _employees;
    private readonly IStoreRepository _stores;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;

    public EmployeeService(
        IEmployeeRepository employees,
        IStoreRepository stores,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser)
    {
        _employees = employees;
        _stores = stores;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
    }

    public IReadOnlyList<EmployeeRow> List()
    {
        _accessControl.Demand(Permissions.HrEmployeesView);

        var storeNames = _stores.List().ToDictionary(store => store.Id, store => store.Name);
        var showSalary = _accessControl.Has(Permissions.HrEmployeesManage);

        return _employees.List()
            .OrderBy(employee => employee.Name)
            .Select(employee => ToRow(employee, storeNames, showSalary))
            .ToList();
    }

    public EmployeeRow Get(int id)
    {
        _accessControl.Demand(Permissions.HrEmployeesView);

        var employee = _employees.GetById(id) ?? throw new EmployeeNotFoundException(id);
        var store = _stores.GetById(employee.StoreId);
        var showSalary = _accessControl.Has(Permissions.HrEmployeesManage);

        return ToRow(employee, store?.Name ?? "—", showSalary);
    }

    public IReadOnlyList<Store> ListStores()
    {
        _accessControl.Demand(Permissions.HrEmployeesView);

        return _stores.List()
            .Where(store => store.Active)
            .OrderBy(store => store.Name)
            .ToList();
    }

    public int Create(CreateEmployeeRequest request)
    {
        _accessControl.Demand(Permissions.HrEmployeesManage);

        var data = Validate(
            request.IdNumber,
            request.Name,
            request.Position,
            request.StoreId,
            request.ContractType,
            request.Salary);

        if (_employees.GetByIdNumber(data.IdNumber) is not null)
            throw new DuplicateIdNumberException(data.IdNumber);

        var employee = new Employee(
            id: 0,
            storeId: data.StoreId,
            idNumber: data.IdNumber,
            name: data.Name,
            position: data.Position,
            contractType: data.ContractLabel,
            salary: data.Salary);

        _employees.Add(employee);
        Record("EmployeeCreated", employee.Id, $"Empleado {employee.Id} creado en tienda {employee.StoreId}");

        return employee.Id;
    }

    public void Update(int id, UpdateEmployeeRequest request)
    {
        _accessControl.Demand(Permissions.HrEmployeesManage);

        var employee = _employees.GetById(id) ?? throw new EmployeeNotFoundException(id);

        var data = Validate(
            request.IdNumber,
            request.Name,
            request.Position,
            request.StoreId,
            request.ContractType,
            request.Salary);

        var sameDocument = _employees.GetByIdNumber(data.IdNumber);
        if (sameDocument is not null && sameDocument.Id != employee.Id)
            throw new DuplicateIdNumberException(data.IdNumber);

        var wasActive = employee.Active;

        employee.Update(
            data.StoreId,
            data.IdNumber,
            data.Name,
            data.Position,
            data.ContractLabel,
            data.Salary);

        if (request.Active)
            employee.Activate();
        else
            employee.Deactivate();

        _employees.Update(employee);

        Record(
            "EmployeeUpdated",
            employee.Id,
            $"Empleado {employee.Id} actualizado; tienda {employee.StoreId}; activo {employee.Active}");

        if (wasActive != employee.Active)
        {
            Record(
                employee.Active ? "EmployeeActivated" : "EmployeeDeactivated",
                employee.Id,
                employee.Active
                    ? $"Empleado {employee.Id} activado"
                    : $"Empleado {employee.Id} desactivado");
        }
    }

    private EmployeeData Validate(
        string idNumber,
        string name,
        string position,
        int storeId,
        string contractType,
        decimal salary)
    {
        var trimmedIdNumber = idNumber?.Trim() ?? string.Empty;
        var trimmedName = name?.Trim() ?? string.Empty;
        var trimmedPosition = position?.Trim() ?? string.Empty;

        if (trimmedIdNumber.Length == 0)
            throw new InvalidEmployeeException("El número de documento es obligatorio.");
        if (trimmedName.Length == 0)
            throw new InvalidEmployeeException("El nombre del empleado es obligatorio.");
        if (trimmedPosition.Length == 0)
            throw new InvalidEmployeeException("El cargo es obligatorio.");
        if (!ContractTypeLabels.TryParse(contractType, out var parsedContract))
            throw new InvalidEmployeeException("El tipo de contrato no es válido.");
        if (salary < 0m)
            throw new InvalidEmployeeException("El salario no puede ser negativo.");

        var store = _stores.GetById(storeId)
            ?? throw new InvalidEmployeeException("La tienda seleccionada no existe.");
        if (!store.Active)
            throw new InvalidEmployeeException("La tienda seleccionada está inactiva.");

        return new EmployeeData(
            trimmedIdNumber,
            trimmedName,
            trimmedPosition,
            store.Id,
            ContractTypeLabels.ToLabel(parsedContract),
            salary);
    }

    private static EmployeeRow ToRow(
        Employee employee,
        IReadOnlyDictionary<int, string> storeNames,
        bool showSalary) =>
        new(
            employee.Id,
            employee.Name,
            employee.IdNumber,
            employee.Position,
            employee.ContractType,
            showSalary ? employee.Salary : null,
            employee.Active,
            employee.StoreId,
            storeNames.TryGetValue(employee.StoreId, out var storeName) ? storeName : "—");

    private static EmployeeRow ToRow(Employee employee, string storeName, bool showSalary) =>
        new(
            employee.Id,
            employee.Name,
            employee.IdNumber,
            employee.Position,
            employee.ContractType,
            showSalary ? employee.Salary : null,
            employee.Active,
            employee.StoreId,
            storeName);

    private void Record(string action, int entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId.ToString(),
            detail,
            _currentUser.UserId,
            DateTime.UtcNow,
            module: "hr",
            ip: _currentUser.Ip));

    private sealed record EmployeeData(
        string IdNumber,
        string Name,
        string Position,
        int StoreId,
        string ContractLabel,
        decimal Salary);
}
