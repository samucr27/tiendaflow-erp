using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public record ContractRow(
    int Id,
    int EmployeeId,
    string EmployeeName,
    ContractType Type,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal Salary,
    EmploymentContractStatus Status,
    int Extensions,
    string? Notes);

public record AttendanceRow(
    int Id,
    int EmployeeId,
    string EmployeeName,
    DateOnly Date,
    AttendanceType Type,
    TimeOnly? CheckIn,
    TimeOnly? CheckOut,
    decimal HoursWorked,
    string? Notes);

public record ShiftRow(
    int Id,
    int StoreId,
    int EmployeeId,
    string EmployeeName,
    DateOnly Date,
    TimeOnly Start,
    TimeOnly End,
    decimal Hours);

public record NewContractRequest(
    int EmployeeId,
    ContractType Type,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal Salary,
    string? Notes);

public record AttendanceRequest(
    int EmployeeId,
    DateOnly Date,
    AttendanceType Type,
    TimeOnly? CheckIn,
    TimeOnly? CheckOut,
    string? Notes);

public record ShiftRequest(int EmployeeId, DateOnly Date, TimeOnly Start, TimeOnly End);

/// <summary>
/// Contracts, attendance and shifts of the employees. Each operation demands
/// its own HR permission and is audited with the record id only: names,
/// documents and salaries never reach the audit detail.
/// </summary>
public sealed class HumanResourcesRecordsService
{
    private readonly IEmployeeRepository _employees;
    private readonly IEntityStore<EmploymentContract> _contracts;
    private readonly IEntityStore<AttendanceRecord> _attendance;
    private readonly IEntityStore<WorkShift> _shifts;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;

    public HumanResourcesRecordsService(
        IEmployeeRepository employees,
        IEntityStore<EmploymentContract> contracts,
        IEntityStore<AttendanceRecord> attendance,
        IEntityStore<WorkShift> shifts,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser)
    {
        _employees = employees;
        _contracts = contracts;
        _attendance = attendance;
        _shifts = shifts;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
    }

    // ------------------------------------------------------------ contracts

    public IReadOnlyList<ContractRow> Contracts()
    {
        _accessControl.Demand(Permissions.HrContractsManage);
        var names = EmployeeNames();

        return _contracts.All()
            .OrderBy(contract => names.GetValueOrDefault(contract.EmployeeId))
            .ThenByDescending(contract => contract.StartDate)
            .Select(contract => new ContractRow(
                contract.Id, contract.EmployeeId, names.GetValueOrDefault(contract.EmployeeId, "—"),
                contract.Type, contract.StartDate, contract.EndDate, contract.Salary,
                contract.Status, contract.Extensions, contract.Notes))
            .ToList();
    }

    /// <summary>
    /// Registers a contract. An employee has at most one active contract; the
    /// employee record takes the contract type and salary so payroll uses them.
    /// </summary>
    public int AddContract(NewContractRequest request)
    {
        _accessControl.Demand(Permissions.HrContractsManage);

        var employee = _employees.GetById(request.EmployeeId)
            ?? throw new EmployeeNotFoundException(request.EmployeeId);

        if (_contracts.All().Any(contract =>
                contract.EmployeeId == employee.Id && contract.Status == EmploymentContractStatus.Active))
        {
            throw new InvalidHrRecordException("The employee already has an active contract; end it first.");
        }

        var contract = new EmploymentContract(
            employee.Id, request.Type, request.StartDate, request.EndDate, request.Salary, request.Notes);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            _contracts.Add(contract);

            employee.Update(
                employee.StoreId,
                employee.IdNumber,
                employee.Name,
                employee.Position,
                ContractTypeLabelsFor(request.Type),
                request.Salary);
            _employees.Update(employee);

            Audit("ContractCreated", contract.Id, $"Contrato {contract.Id} registrado para el empleado {employee.Id}");
        });

        return contract.Id;
    }

    public void ExtendContract(int contractId, DateOnly newEndDate)
    {
        _accessControl.Demand(Permissions.HrContractsManage);
        var contract = _contracts.Get(contractId) ?? throw new HrRecordNotFoundException("Contract", contractId);

        contract.Extend(newEndDate);
        _contracts.Update(contract);
        Audit("ContractExtended", contract.Id, $"Contrato {contract.Id} prorrogado (prórroga {contract.Extensions})");
    }

    public void EndContract(int contractId, DateOnly endDate)
    {
        _accessControl.Demand(Permissions.HrContractsManage);
        var contract = _contracts.Get(contractId) ?? throw new HrRecordNotFoundException("Contract", contractId);

        contract.End(endDate);
        _contracts.Update(contract);
        Audit("ContractEnded", contract.Id, $"Contrato {contract.Id} terminado");
    }

    /// <summary>Active contracts that end within the given number of days.</summary>
    public IReadOnlyList<ContractRow> ExpiringContracts(DateOnly today, int days) =>
        Contracts()
            .Where(row => row.Status == EmploymentContractStatus.Active
                          && row.EndDate is DateOnly end
                          && end >= today
                          && end <= today.AddDays(days))
            .OrderBy(row => row.EndDate)
            .ToList();

    // ------------------------------------------------------------ attendance

    public IReadOnlyList<AttendanceRow> Attendance(DateOnly from, DateOnly to)
    {
        _accessControl.Demand(Permissions.HrAttendanceManage);
        var names = EmployeeNames();

        return _attendance.All()
            .Where(record => record.Date >= from && record.Date <= to)
            .OrderByDescending(record => record.Date)
            .ThenBy(record => names.GetValueOrDefault(record.EmployeeId))
            .Select(record => new AttendanceRow(
                record.Id, record.EmployeeId, names.GetValueOrDefault(record.EmployeeId, "—"),
                record.Date, record.Type, record.CheckIn, record.CheckOut, record.HoursWorked, record.Notes))
            .ToList();
    }

    /// <summary>One record per employee and day: registering again replaces it.</summary>
    public int RecordAttendance(AttendanceRequest request)
    {
        _accessControl.Demand(Permissions.HrAttendanceManage);
        _ = _employees.GetById(request.EmployeeId) ?? throw new EmployeeNotFoundException(request.EmployeeId);

        if (_attendance.All().Any(record => record.EmployeeId == request.EmployeeId && record.Date == request.Date))
            throw new InvalidHrRecordException("The employee already has a record for that day.");

        var record = new AttendanceRecord(
            request.EmployeeId, request.Date, request.Type, request.CheckIn, request.CheckOut, request.Notes);

        _attendance.Add(record);
        Audit("AttendanceRecorded", record.Id, $"Registro {record.Id} ({record.Type}) del {record.Date:yyyy-MM-dd}");
        return record.Id;
    }

    /// <summary>
    /// Unpaid absences of each employee in the month; the payroll run
    /// subtracts them from the days worked.
    /// </summary>
    public IReadOnlyDictionary<int, int> UnpaidAbsences(int year, int month) =>
        _attendance.All()
            .Where(record => record.Date.Year == year && record.Date.Month == month && record.IsUnpaidAbsence)
            .GroupBy(record => record.EmployeeId)
            .ToDictionary(group => group.Key, group => group.Count());

    // ------------------------------------------------------------ shifts

    public IReadOnlyList<ShiftRow> Shifts(DateOnly from, DateOnly to, int? storeId = null)
    {
        _accessControl.Demand(Permissions.HrShiftsManage);
        var names = EmployeeNames();

        return _shifts.All()
            .Where(shift => shift.Date >= from && shift.Date <= to)
            .Where(shift => storeId is null || shift.StoreId == storeId)
            .OrderBy(shift => shift.Date)
            .ThenBy(shift => shift.Start)
            .Select(shift => new ShiftRow(
                shift.Id, shift.StoreId, shift.EmployeeId, names.GetValueOrDefault(shift.EmployeeId, "—"),
                shift.Date, shift.Start, shift.End, shift.Hours))
            .ToList();
    }

    public int AssignShift(ShiftRequest request)
    {
        _accessControl.Demand(Permissions.HrShiftsManage);
        var employee = _employees.GetById(request.EmployeeId)
            ?? throw new EmployeeNotFoundException(request.EmployeeId);

        var shift = new WorkShift(employee.StoreId, employee.Id, request.Date, request.Start, request.End);

        if (_shifts.All().Any(existing => existing.Overlaps(shift)))
            throw new InvalidHrRecordException("The shift overlaps another shift of the same employee.");

        _shifts.Add(shift);
        Audit("ShiftAssigned", shift.Id, $"Turno {shift.Id} del {shift.Date:yyyy-MM-dd} en la tienda {shift.StoreId}");
        return shift.Id;
    }

    // ------------------------------------------------------------ helpers

    private Dictionary<int, string> EmployeeNames() =>
        _employees.List().ToDictionary(employee => employee.Id, employee => employee.Name);

    private static string ContractTypeLabelsFor(ContractType type) => Payroll.ContractTypeLabels.ToLabel(type);

    private void Audit(string action, int entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId.ToString(),
            detail,
            _currentUser.UserId,
            DateTime.UtcNow,
            module: "hr",
            ip: _currentUser.Ip));
}
