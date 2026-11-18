using TiendaFlow.Application;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.UnitTests;

/// <summary>
/// Records that multi-table operations were wrapped in a transaction. The unit
/// tests use in-memory repositories, so the action simply runs; the counter lets
/// a test prove the application layer asked for atomicity before writing.
/// </summary>
internal sealed class RecordingUnitOfWork : IUnitOfWork
{
    public int Executions { get; private set; }

    public void ExecuteInTransaction(Action action)
    {
        Executions++;
        action();
    }
}

/// <summary>
/// Parameter source for the shift caps. Decimals are stored directly so the
/// tests exercise the configured limits without a database.
/// </summary>
internal sealed class StubDecimalParameterValues : IParameterValues
{
    private readonly Dictionary<string, decimal> _decimals = new(StringComparer.Ordinal);

    public StubDecimalParameterValues With(string key, decimal value)
    {
        _decimals[key] = value;
        return this;
    }

    public int GetInt(string key, int? storeId = null) => (int)GetDecimal(key, storeId);

    public decimal GetDecimal(string key, int? storeId = null) =>
        _decimals.TryGetValue(key, out var value) ? value : 0m;

    public string GetText(string key, int? storeId = null) => GetDecimal(key, storeId).ToString();

    public bool GetBoolean(string key, int? storeId = null) => GetDecimal(key, storeId) != 0m;
}

internal sealed class InMemoryAttendanceRepository : IAttendanceRepository
{
    private readonly List<AttendanceRecord> _records = new();
    private int _nextId = 1;

    public int ListCalls { get; private set; }

    public IReadOnlyList<AttendanceRecord> All() => _records;

    public AttendanceRecord? GetById(int id) => _records.FirstOrDefault(record => record.Id == id);

    public AttendanceRecord? GetByEmployeeAndDate(int employeeId, DateOnly workDate) =>
        _records.FirstOrDefault(record =>
            record.EmployeeId == employeeId && record.WorkDate == workDate);

    public IReadOnlyList<AttendanceRecord> List(DateOnly from, DateOnly to, int? storeId)
    {
        ListCalls++;
        return _records
            .Where(record => record.WorkDate >= from && record.WorkDate <= to)
            .Where(record => storeId is null || record.StoreId == storeId)
            .ToList();
    }

    public void Add(AttendanceRecord record)
    {
        TestIds.Assign(record, _nextId++);
        _records.Add(record);
    }

    public void Update(AttendanceRecord record)
    {
        if (!_records.Contains(record))
            _records.Add(record);
    }
}

/// <summary>
/// Employee repository that preserves the ids supplied by the test. The
/// general in-memory double assigns identities like EF Core does; workforce
/// tests prefer stable ids so audit details and store-scope assertions stay
/// clear and independent of insertion order.
/// </summary>
internal sealed class FixedIdEmployeeRepository : IEmployeeRepository
{
    private readonly List<Employee> _employees = new();

    public void Add(Employee employee) => _employees.Add(employee);

    public Employee? GetById(int id) => _employees.FirstOrDefault(employee => employee.Id == id);

    public Employee? GetByIdNumber(string idNumber) =>
        _employees.FirstOrDefault(employee => employee.IdNumber == idNumber.Trim());

    public IReadOnlyList<Employee> List() => _employees;

    public void Update(Employee employee)
    {
        if (!_employees.Contains(employee))
            _employees.Add(employee);
    }
}

internal sealed class InMemoryShiftRepository : IShiftRepository
{
    private readonly List<ShiftTemplate> _templates = new();
    private readonly List<ShiftAssignment> _assignments = new();
    private int _nextTemplateId = 1;
    private int _nextAssignmentId = 1;

    public int TemplateListCalls { get; private set; }

    public int AssignmentListCalls { get; private set; }

    public IReadOnlyList<ShiftTemplate> AllTemplates() => _templates;

    public IReadOnlyList<ShiftAssignment> AllAssignments() => _assignments;

    public IReadOnlyList<ShiftTemplate> ListTemplates()
    {
        TemplateListCalls++;
        return _templates;
    }

    public ShiftTemplate? GetTemplate(int id) => _templates.FirstOrDefault(template => template.Id == id);

    public bool ExistsTemplateName(string name, int? exceptTemplateId = null) =>
        _templates.Any(template =>
            string.Equals(template.Name, name.Trim(), StringComparison.Ordinal)
            && (exceptTemplateId is null || template.Id != exceptTemplateId));

    public void AddTemplate(ShiftTemplate template)
    {
        TestIds.Assign(template, _nextTemplateId++);
        _templates.Add(template);
    }

    public ShiftAssignment? GetAssignment(int id) =>
        _assignments.FirstOrDefault(assignment => assignment.Id == id);

    public IReadOnlyList<ShiftAssignment> ListAssignments(
        DateOnly from,
        DateOnly to,
        int? storeId)
    {
        AssignmentListCalls++;
        return _assignments
            .Where(assignment => assignment.WorkDate >= from && assignment.WorkDate <= to)
            .Where(assignment => storeId is null || assignment.StoreId == storeId)
            .ToList();
    }

    public IReadOnlyList<ShiftAssignment> ListAssignmentsByEmployeeAndDate(
        int employeeId,
        DateOnly workDate) =>
        _assignments
            .Where(assignment => assignment.EmployeeId == employeeId && assignment.WorkDate == workDate)
            .ToList();

    public void AddAssignment(ShiftAssignment assignment)
    {
        TestIds.Assign(assignment, _nextAssignmentId++);
        _assignments.Add(assignment);
    }

    public void RemoveAssignment(ShiftAssignment assignment) => _assignments.Remove(assignment);
}
