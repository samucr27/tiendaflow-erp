using TiendaFlow.Application.Parameters;
using TiendaFlow.Application.Reporting;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public enum ShiftAttendanceStatus
{
    OnTime = 1,
    Late = 2,
    Absent = 3,
    Incomplete = 4,
    JustifiedAbsence = 5,
    UnjustifiedAbsence = 6,
    Leave = 7,
    Vacation = 8
}

public static class WorkforceText
{
    public static string NoveltyLabel(AttendanceNovelty? novelty) => novelty switch
    {
        AttendanceNovelty.JustifiedAbsence => "Ausencia justificada",
        AttendanceNovelty.UnjustifiedAbsence => "Ausencia injustificada",
        AttendanceNovelty.Leave => "Licencia",
        AttendanceNovelty.Vacation => "Vacaciones",
        null => "—",
        _ => novelty.Value.ToString()
    };

    public static string StatusLabel(ShiftAttendanceStatus status) => status switch
    {
        ShiftAttendanceStatus.OnTime => "A tiempo",
        ShiftAttendanceStatus.Late => "Tarde",
        ShiftAttendanceStatus.Absent => "Ausente",
        ShiftAttendanceStatus.Incomplete => "Sin salida",
        ShiftAttendanceStatus.JustifiedAbsence => "Ausencia justificada",
        ShiftAttendanceStatus.UnjustifiedAbsence => "Ausencia injustificada",
        ShiftAttendanceStatus.Leave => "Licencia",
        ShiftAttendanceStatus.Vacation => "Vacaciones",
        _ => status.ToString()
    };
}

public sealed record AttendanceRow(
    int Id,
    int EmployeeId,
    string EmployeeName,
    string IdNumber,
    int StoreId,
    string StoreName,
    DateOnly WorkDate,
    DateTime? CheckIn,
    DateTime? CheckOut,
    AttendanceNovelty? Novelty,
    string? NoveltyReason,
    decimal HoursWorked)
{
    public bool IsOpen => CheckIn is not null && CheckOut is null;

    public string StatusLabel => Novelty is AttendanceNovelty novelty
        ? WorkforceText.NoveltyLabel(novelty)
        : CheckIn is null
            ? "Sin marcación"
            : CheckOut is null
                ? "En turno"
                : "Completo";
}

public sealed record WorkforceEmployeeOption(
    int Id,
    string Name,
    string IdNumber,
    string Position,
    int StoreId,
    string StoreName);

public sealed record WorkforceStoreOption(int Id, string Name);

public sealed record AttendanceSummaryRow(
    int EmployeeId,
    string EmployeeName,
    string IdNumber,
    int PresentDays,
    int OpenDays,
    decimal WorkedHours,
    int JustifiedAbsences,
    int UnjustifiedAbsences,
    int Leaves,
    int Vacations);

public sealed record ShiftTemplateRow(
    int Id,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal Hours,
    bool Active);

public sealed record ShiftAssignmentRow(
    int Id,
    int EmployeeId,
    string EmployeeName,
    int StoreId,
    string StoreName,
    DateOnly Date,
    int TemplateId,
    string TemplateName,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal Hours);

public sealed record ShiftWeek(
    DateOnly Start,
    IReadOnlyList<DateOnly> Days,
    IReadOnlyList<WorkforceEmployeeOption> Employees,
    IReadOnlyList<ShiftAssignmentRow> Assignments);

public sealed record ShiftAttendanceRow(
    DateOnly Date,
    int EmployeeId,
    string EmployeeName,
    string IdNumber,
    string StoreName,
    TimeOnly ExpectedStart,
    TimeOnly ExpectedEnd,
    decimal ShiftHours,
    DateTime? CheckIn,
    DateTime? CheckOut,
    int TardinessMinutes,
    decimal WorkedHours,
    ShiftAttendanceStatus Status)
{
    public string StatusLabel => WorkforceText.StatusLabel(Status);
}

public sealed record ShiftAttendanceReport(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<ShiftAttendanceRow> Rows)
{
    public int LateCount => Rows.Count(row => row.Status == ShiftAttendanceStatus.Late);

    public int AbsenceCount => Rows.Count(row =>
        row.Status is ShiftAttendanceStatus.Absent
            or ShiftAttendanceStatus.JustifiedAbsence
            or ShiftAttendanceStatus.UnjustifiedAbsence
            or ShiftAttendanceStatus.Leave
            or ShiftAttendanceStatus.Vacation);

    public decimal WorkedHours => Rows.Sum(row => row.WorkedHours);
}

public sealed record AttendanceCheckInRequest(int EmployeeId, DateOnly? WorkDate = null);

public sealed record AttendanceNoveltyRequest(
    int EmployeeId,
    DateOnly WorkDate,
    AttendanceNovelty Novelty,
    string? Reason);

public sealed record ShiftTemplateRequest(string Name, TimeOnly StartTime, TimeOnly EndTime);

public sealed record ShiftAssignmentRequest(
    int EmployeeId,
    int StoreId,
    int ShiftTemplateId,
    DateOnly WorkDate);

public interface IAttendanceRepository
{
    AttendanceRecord? GetById(int id);

    AttendanceRecord? GetByEmployeeAndDate(int employeeId, DateOnly workDate);

    IReadOnlyList<AttendanceRecord> List(DateOnly from, DateOnly to, int? storeId);

    void Add(AttendanceRecord record);

    void Update(AttendanceRecord record);
}

public interface IShiftRepository
{
    IReadOnlyList<ShiftTemplate> ListTemplates();

    ShiftTemplate? GetTemplate(int id);

    bool ExistsTemplateName(string name, int? exceptTemplateId = null);

    void AddTemplate(ShiftTemplate template);

    ShiftAssignment? GetAssignment(int id);

    IReadOnlyList<ShiftAssignment> ListAssignments(
        DateOnly from,
        DateOnly to,
        int? storeId);

    IReadOnlyList<ShiftAssignment> ListAssignmentsByEmployeeAndDate(
        int employeeId,
        DateOnly workDate);

    void AddAssignment(ShiftAssignment assignment);

    void RemoveAssignment(ShiftAssignment assignment);
}

/// <summary>
/// Attendance of every employee per day. A day is either a check-in/check-out
/// pair or a novelty; the domain prevents mixing them. Every method demands
/// hr.attendance.manage before touching a repository and writes an audit entry
/// for the sensitive actions, with module, user, IP and a detail that never
/// copies the novelty reason or any personal data.
/// </summary>
public sealed class AttendanceService
{
    private const int MaxRangeDays = 366;

    private readonly IAttendanceRepository _attendance;
    private readonly IEmployeeRepository _employees;
    private readonly IStoreRepository _stores;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public AttendanceService(
        IAttendanceRepository attendance,
        IEmployeeRepository employees,
        IStoreRepository stores,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _attendance = attendance;
        _employees = employees;
        _stores = stores;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public IReadOnlyList<AttendanceRow> List(DateOnly from, DateOnly to, int? storeId)
    {
        _accessControl.Demand(Permissions.HrAttendanceManage);

        var scopedStoreId = HrStoreScope.Restrict(_currentUser, storeId);
        ValidateRange(from, to);

        return MapRows(_attendance.List(from, to, scopedStoreId));
    }

    public IReadOnlyList<WorkforceEmployeeOption> Employees(int? storeId)
    {
        _accessControl.Demand(Permissions.HrAttendanceManage);

        var scopedStoreId = HrStoreScope.Restrict(_currentUser, storeId);
        var stores = _stores.List().ToDictionary(store => store.Id);

        return _employees.List()
            .Where(employee => employee.Active)
            .Where(employee => scopedStoreId is null || employee.StoreId == scopedStoreId)
            .OrderBy(employee => employee.Name)
            .Select(employee => new WorkforceEmployeeOption(
                employee.Id,
                employee.Name,
                employee.IdNumber,
                employee.Position,
                employee.StoreId,
                stores.TryGetValue(employee.StoreId, out var store)
                    ? store.Name
                    : "Sin tienda"))
            .ToList();
    }

    public IReadOnlyList<WorkforceStoreOption> Stores()
    {
        _accessControl.Demand(Permissions.HrAttendanceManage);

        var scopedStoreId = HrStoreScope.Restrict(_currentUser, null);

        return _stores.List()
            .Where(store => scopedStoreId is null || store.Id == scopedStoreId)
            .OrderBy(store => store.Name)
            .Select(store => new WorkforceStoreOption(store.Id, store.Name))
            .ToList();
    }

    public int CheckIn(AttendanceCheckInRequest request)
    {
        _accessControl.Demand(Permissions.HrAttendanceManage);

        var employee = ActiveEmployee(request.EmployeeId);
        HrStoreScope.Demand(_currentUser, employee.StoreId);

        var today = Today();
        var workDate = request.WorkDate ?? today;

        if (workDate > today)
            throw new InvalidAttendanceException("No se puede registrar asistencia de una fecha futura.");
        if (workDate < today)
            throw new InvalidAttendanceException(
                "Solo se puede marcar la entrada del día de hoy. Usa una novedad para corregir un día anterior.");

        var record = _attendance.GetByEmployeeAndDate(employee.Id, workDate);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            if (record is null)
            {
                record = new AttendanceRecord(
                    id: 0,
                    employee.Id,
                    employee.StoreId,
                    workDate,
                    _clock.UtcNow);

                record.RegisterCheckIn(_clock.UtcNow);
                _attendance.Add(record);
            }
            else
            {
                record.RegisterCheckIn(_clock.UtcNow);
                _attendance.Update(record);
            }

            Record(
                "AttendanceCheckedIn",
                record.Id,
                $"Entrada del empleado {employee.Id} el {workDate:yyyy-MM-dd}");
        });

        return record!.Id;
    }

    public void CheckOut(int recordId)
    {
        _accessControl.Demand(Permissions.HrAttendanceManage);

        var record = _attendance.GetById(recordId)
            ?? throw new AttendanceRecordNotFoundException(recordId);

        HrStoreScope.Demand(_currentUser, record.StoreId);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            record.RegisterCheckOut(_clock.UtcNow);
            _attendance.Update(record);

            Record(
                "AttendanceCheckedOut",
                record.Id,
                $"Salida del registro {record.Id} del {record.WorkDate:yyyy-MM-dd}");
        });
    }

    public int RegisterNovelty(AttendanceNoveltyRequest request)
    {
        _accessControl.Demand(Permissions.HrAttendanceManage);

        var employee = ActiveEmployee(request.EmployeeId);
        HrStoreScope.Demand(_currentUser, employee.StoreId);

        var today = Today();

        if (request.WorkDate > today)
            throw new InvalidAttendanceException("No se puede registrar una novedad de una fecha futura.");

        var record = _attendance.GetByEmployeeAndDate(employee.Id, request.WorkDate);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            if (record is null)
            {
                record = new AttendanceRecord(
                    id: 0,
                    employee.Id,
                    employee.StoreId,
                    request.WorkDate,
                    _clock.UtcNow);

                record.RegisterNovelty(request.Novelty, request.Reason, _clock.UtcNow);
                _attendance.Add(record);
            }
            else
            {
                record.RegisterNovelty(request.Novelty, request.Reason, _clock.UtcNow);
                _attendance.Update(record);
            }

            Record(
                "AttendanceNoveltyRegistered",
                record.Id,
                $"Novedad {request.Novelty} del empleado {employee.Id} el {request.WorkDate:yyyy-MM-dd}");
        });

        return record!.Id;
    }

    public IReadOnlyList<AttendanceSummaryRow> Summary(
        DateOnly from,
        DateOnly to,
        int? storeId)
    {
        _accessControl.Demand(Permissions.HrAttendanceManage);

        var scopedStoreId = HrStoreScope.Restrict(_currentUser, storeId);
        ValidateRange(from, to);

        return BuildSummary(_attendance.List(from, to, scopedStoreId));
    }

    public string SummaryCsv(DateOnly from, DateOnly to, int? storeId)
    {
        var rows = Summary(from, to, storeId);

        Record(
            "AttendanceSummaryExported",
            0,
            $"Exportación de asistencia del {from:yyyy-MM-dd} al {to:yyyy-MM-dd}");

        return CsvReport.Build(
            [
                "Empleado",
                "Documento",
                "Días con marcación",
                "Días abiertos",
                "Horas trabajadas",
                "Ausencias justificadas",
                "Ausencias injustificadas",
                "Licencias",
                "Vacaciones"
            ],
            rows.Select(row => new[]
            {
                row.EmployeeName,
                row.IdNumber,
                row.PresentDays.ToString(),
                row.OpenDays.ToString(),
                CsvReport.Number(row.WorkedHours),
                row.JustifiedAbsences.ToString(),
                row.UnjustifiedAbsences.ToString(),
                row.Leaves.ToString(),
                row.Vacations.ToString()
            }));
    }

    private IReadOnlyList<AttendanceSummaryRow> BuildSummary(
        IReadOnlyList<AttendanceRecord> records) =>
        records
            .GroupBy(record => record.EmployeeId)
            .Select(group =>
            {
                var employee = _employees.GetById(group.Key);
                var items = group.ToList();

                return new AttendanceSummaryRow(
                    group.Key,
                    employee?.Name ?? "—",
                    employee?.IdNumber ?? "—",
                    items.Count(record => record.IsPresent),
                    items.Count(record => record.IsOpen),
                    items.Sum(record => record.HoursWorked),
                    items.Count(record => record.Novelty == AttendanceNovelty.JustifiedAbsence),
                    items.Count(record => record.Novelty == AttendanceNovelty.UnjustifiedAbsence),
                    items.Count(record => record.Novelty == AttendanceNovelty.Leave),
                    items.Count(record => record.Novelty == AttendanceNovelty.Vacation));
            })
            .OrderBy(row => row.EmployeeName)
            .ToList();

    private IReadOnlyList<AttendanceRow> MapRows(IReadOnlyList<AttendanceRecord> records)
    {
        if (records.Count == 0)
            return Array.Empty<AttendanceRow>();

        var employees = _employees.List().ToDictionary(employee => employee.Id);
        var stores = _stores.List().ToDictionary(store => store.Id);

        return records
            .Select(record =>
            {
                employees.TryGetValue(record.EmployeeId, out var employee);
                stores.TryGetValue(record.StoreId, out var store);

                return new AttendanceRow(
                    record.Id,
                    record.EmployeeId,
                    employee?.Name ?? "—",
                    employee?.IdNumber ?? "—",
                    record.StoreId,
                    store?.Name ?? "Sin tienda",
                    record.WorkDate,
                    record.CheckIn,
                    record.CheckOut,
                    record.Novelty,
                    record.NoveltyReason,
                    record.HoursWorked);
            })
            .OrderByDescending(row => row.WorkDate)
            .ThenBy(row => row.EmployeeName)
            .ToList();
    }

    private Employee ActiveEmployee(int employeeId) =>
        _employees.GetById(employeeId) is Employee employee && employee.Active
            ? employee
            : throw new InvalidAttendanceException("El empleado no existe o está inactivo.");

    private static void ValidateRange(DateOnly from, DateOnly to)
    {
        if (from > to)
            throw new InvalidAttendanceException("La fecha inicial no puede ser posterior a la final.");
        if (to.DayNumber - from.DayNumber > MaxRangeDays)
            throw new InvalidAttendanceException(
                $"El rango no puede superar los {MaxRangeDays} días.");
    }

    private DateOnly Today() => DateOnly.FromDateTime(LocalTime.ToLocal(_clock.UtcNow));

    private void Record(string action, int entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId.ToString(),
            detail,
            _currentUser.UserId,
            _clock.UtcNow,
            module: "hr",
            ip: _currentUser.Ip));
}

/// <summary>
/// Shift templates, their weekly schedule and the shift-versus-attendance
/// report. Overlaps and the daily hour cap are validated with the configurable
/// parameters hr.shifts.max_hours_per_shift and hr.shifts.max_hours_per_day
/// before the row is written.
/// </summary>
public sealed class ShiftService
{
    private const int MaxWeekOffsetDays = 366;

    private readonly IShiftRepository _shifts;
    private readonly IAttendanceRepository _attendance;
    private readonly IEmployeeRepository _employees;
    private readonly IStoreRepository _stores;
    private readonly IParameterValues _parameters;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ShiftService(
        IShiftRepository shifts,
        IAttendanceRepository attendance,
        IEmployeeRepository employees,
        IStoreRepository stores,
        IParameterValues parameters,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _shifts = shifts;
        _attendance = attendance;
        _employees = employees;
        _stores = stores;
        _parameters = parameters;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public IReadOnlyList<WorkforceStoreOption> Stores()
    {
        _accessControl.Demand(Permissions.HrShiftsManage);

        var scopedStoreId = HrStoreScope.Restrict(_currentUser, null);

        return _stores.List()
            .Where(store => scopedStoreId is null || store.Id == scopedStoreId)
            .OrderBy(store => store.Name)
            .Select(store => new WorkforceStoreOption(store.Id, store.Name))
            .ToList();
    }

    public IReadOnlyList<ShiftTemplateRow> Templates()
    {
        _accessControl.Demand(Permissions.HrShiftsManage);

        return _shifts.ListTemplates()
            .OrderBy(template => template.StartTime)
            .ThenBy(template => template.Name)
            .Select(template => new ShiftTemplateRow(
                template.Id,
                template.Name,
                template.StartTime,
                template.EndTime,
                template.DurationHours,
                template.Active))
            .ToList();
    }

    public int CreateTemplate(ShiftTemplateRequest request)
    {
        _accessControl.Demand(Permissions.HrShiftsManage);

        var name = request.Name?.Trim() ?? string.Empty;

        if (_shifts.ExistsTemplateName(name))
            throw new InvalidShiftException("Ya existe una plantilla de turno con ese nombre.");

        var template = new ShiftTemplate(
            id: 0,
            name,
            request.StartTime,
            request.EndTime,
            _clock.UtcNow);

        var maxHours = _parameters.GetDecimal(SystemParameterCatalog.MaxHoursPerShift);

        if (template.DurationHours > maxHours)
            throw new InvalidShiftException(
                $"El turno supera el máximo configurado de {maxHours:0.##} horas.");

        _unitOfWork.ExecuteInTransaction(() =>
        {
            _shifts.AddTemplate(template);

            Record(
                "ShiftTemplateCreated",
                template.Id,
                $"Plantilla {template.Name} de {template.StartTime:HH\\:mm} a {template.EndTime:HH\\:mm}");
        });

        return template.Id;
    }

    public ShiftWeek Week(DateOnly? date, int? storeId)
    {
        _accessControl.Demand(Permissions.HrShiftsManage);

        var scopedStoreId = HrStoreScope.Restrict(_currentUser, storeId);
        var target = date ?? Today();

        if (Math.Abs(target.DayNumber - Today().DayNumber) > MaxWeekOffsetDays)
            throw new InvalidShiftException("La semana solicitada está fuera del rango permitido.");

        var start = StartOfWeek(target);
        var days = Enumerable.Range(0, 7).Select(offset => start.AddDays(offset)).ToList();
        var end = start.AddDays(6);

        var employees = _employees.List()
            .Where(employee => employee.Active)
            .Where(employee => scopedStoreId is null || employee.StoreId == scopedStoreId)
            .OrderBy(employee => employee.Name)
            .ToList();

        var stores = _stores.List().ToDictionary(store => store.Id);
        var templates = _shifts.ListTemplates().ToDictionary(template => template.Id);

        var assignments = _shifts.ListAssignments(start, end, scopedStoreId)
            .Select(assignment =>
            {
                var employee = employees.FirstOrDefault(item => item.Id == assignment.EmployeeId);
                stores.TryGetValue(assignment.StoreId, out var store);
                templates.TryGetValue(assignment.ShiftTemplateId, out var template);

                return new ShiftAssignmentRow(
                    assignment.Id,
                    assignment.EmployeeId,
                    employee?.Name ?? "—",
                    assignment.StoreId,
                    store?.Name ?? "Sin tienda",
                    assignment.WorkDate,
                    assignment.ShiftTemplateId,
                    template?.Name ?? "—",
                    assignment.StartTime,
                    assignment.EndTime,
                    assignment.DurationHours);
            })
            .OrderBy(row => row.Date)
            .ThenBy(row => row.StartTime)
            .ToList();

        return new ShiftWeek(
            start,
            days,
            employees.Select(employee => new WorkforceEmployeeOption(
                employee.Id,
                employee.Name,
                employee.IdNumber,
                employee.Position,
                employee.StoreId,
                stores.TryGetValue(employee.StoreId, out var store)
                    ? store.Name
                    : "Sin tienda")).ToList(),
            assignments);
    }

    public int Assign(ShiftAssignmentRequest request)
    {
        _accessControl.Demand(Permissions.HrShiftsManage);

        var today = Today();

        if (request.WorkDate < today)
            throw new InvalidShiftException("No se puede asignar un turno en una fecha pasada.");

        var employee = _employees.GetById(request.EmployeeId)
            ?? throw new InvalidShiftException("El empleado seleccionado no existe.");

        if (!employee.Active)
            throw new InvalidShiftException("No se puede asignar un turno a un empleado inactivo.");
        if (employee.StoreId != request.StoreId)
            throw new InvalidShiftException("El empleado no pertenece a la tienda seleccionada.");

        var store = _stores.GetById(request.StoreId)
            ?? throw new InvalidShiftException("La tienda seleccionada no existe.");

        if (!store.Active)
            throw new InvalidShiftException("No se puede asignar un turno en una tienda inactiva.");

        HrStoreScope.Demand(_currentUser, store.Id);

        var template = _shifts.GetTemplate(request.ShiftTemplateId)
            ?? throw new ShiftTemplateNotFoundException(request.ShiftTemplateId);

        if (!template.Active)
            throw new InvalidShiftException("La plantilla de turno está inactiva.");

        var maxShiftHours = _parameters.GetDecimal(SystemParameterCatalog.MaxHoursPerShift);

        if (template.DurationHours > maxShiftHours)
            throw new InvalidShiftException(
                $"El turno supera el máximo configurado de {maxShiftHours:0.##} horas.");

        var existing = _shifts.ListAssignmentsByEmployeeAndDate(employee.Id, request.WorkDate);

        if (existing.Any(assignment => assignment.Overlaps(template.StartTime, template.EndTime)))
            throw new InvalidShiftException(
                "El empleado ya tiene un turno que se cruza con ese horario.");

        var maxDayHours = _parameters.GetDecimal(SystemParameterCatalog.MaxHoursPerDay);
        var assignedHours = existing.Sum(assignment => assignment.DurationHours);

        if (assignedHours + template.DurationHours > maxDayHours)
            throw new InvalidShiftException(
                $"La suma de turnos del día supera el máximo configurado de {maxDayHours:0.##} horas.");

        var assignment = new ShiftAssignment(
            id: 0,
            employee.Id,
            store.Id,
            template.Id,
            request.WorkDate,
            template.StartTime,
            template.EndTime,
            _clock.UtcNow);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            _shifts.AddAssignment(assignment);

            Record(
                "ShiftAssigned",
                assignment.Id,
                $"Turno {template.Name} del empleado {employee.Id} en la tienda {store.Id} el {request.WorkDate:yyyy-MM-dd}");
        });

        return assignment.Id;
    }

    public void Cancel(int assignmentId)
    {
        _accessControl.Demand(Permissions.HrShiftsManage);

        var assignment = _shifts.GetAssignment(assignmentId)
            ?? throw new ShiftAssignmentNotFoundException(assignmentId);

        HrStoreScope.Demand(_currentUser, assignment.StoreId);
        assignment.EnsureCancellable(Today());

        _unitOfWork.ExecuteInTransaction(() =>
        {
            _shifts.RemoveAssignment(assignment);

            Record(
                "ShiftCancelled",
                assignment.Id,
                $"Turno {assignment.Id} del {assignment.WorkDate:yyyy-MM-dd} cancelado");
        });
    }

    public ShiftAttendanceReport AttendanceReport(
        DateOnly from,
        DateOnly to,
        int? storeId)
    {
        _accessControl.Demand(Permissions.HrShiftsManage);

        var scopedStoreId = HrStoreScope.Restrict(_currentUser, storeId);
        ValidateRange(from, to);

        var assignments = _shifts.ListAssignments(from, to, scopedStoreId);

        if (assignments.Count == 0)
            return new ShiftAttendanceReport(from, to, Array.Empty<ShiftAttendanceRow>());

        var employees = _employees.List().ToDictionary(employee => employee.Id);
        var stores = _stores.List().ToDictionary(store => store.Id);
        var attendance = _attendance.List(from, to, scopedStoreId)
            .ToDictionary(record => (record.EmployeeId, record.WorkDate));

        var rows = assignments
            .GroupBy(assignment => (assignment.EmployeeId, assignment.WorkDate))
            .Select(group =>
            {
                var expectedStart = group.Min(assignment => assignment.StartTime);
                var expectedEnd = group.Max(assignment => assignment.EndTime);
                var shiftHours = group.Sum(assignment => assignment.DurationHours);

                employees.TryGetValue(group.Key.EmployeeId, out var employee);
                stores.TryGetValue(group.First().StoreId, out var store);

                attendance.TryGetValue(group.Key, out var record);

                var status = StatusOf(record);
                var tardiness = TardinessMinutes(group.Key.WorkDate, expectedStart, record);

                if (status == ShiftAttendanceStatus.OnTime && tardiness > 0)
                    status = ShiftAttendanceStatus.Late;

                return new ShiftAttendanceRow(
                    group.Key.WorkDate,
                    group.Key.EmployeeId,
                    employee?.Name ?? "—",
                    employee?.IdNumber ?? "—",
                    store?.Name ?? "Sin tienda",
                    expectedStart,
                    expectedEnd,
                    shiftHours,
                    record?.CheckIn,
                    record?.CheckOut,
                    tardiness,
                    record?.HoursWorked ?? 0m,
                    status);
            })
            .OrderBy(row => row.Date)
            .ThenBy(row => row.EmployeeName)
            .ToList();

        return new ShiftAttendanceReport(from, to, rows);
    }

    public string AttendanceReportCsv(DateOnly from, DateOnly to, int? storeId)
    {
        var report = AttendanceReport(from, to, storeId);

        Record(
            "ShiftAttendanceExported",
            0,
            $"Exportación turnos contra asistencia del {from:yyyy-MM-dd} al {to:yyyy-MM-dd}");

        return CsvReport.Build(
            [
                "Fecha",
                "Empleado",
                "Documento",
                "Tienda",
                "Inicio esperado",
                "Fin esperado",
                "Horas del turno",
                "Entrada",
                "Salida",
                "Minutos de retraso",
                "Horas trabajadas",
                "Estado"
            ],
            report.Rows.Select(row => new[]
            {
                row.Date.ToString("yyyy-MM-dd"),
                row.EmployeeName,
                row.IdNumber,
                row.StoreName,
                row.ExpectedStart.ToString("HH:mm"),
                row.ExpectedEnd.ToString("HH:mm"),
                CsvReport.Number(row.ShiftHours),
                row.CheckIn is DateTime checkIn
                    ? CsvReport.Date(LocalTime.ToLocal(checkIn))
                    : string.Empty,
                row.CheckOut is DateTime checkOut
                    ? CsvReport.Date(LocalTime.ToLocal(checkOut))
                    : string.Empty,
                row.TardinessMinutes.ToString(),
                CsvReport.Number(row.WorkedHours),
                row.StatusLabel
            }));
    }

    private static ShiftAttendanceStatus StatusOf(AttendanceRecord? record)
    {
        if (record?.Novelty is AttendanceNovelty novelty)
        {
            return novelty switch
            {
                AttendanceNovelty.JustifiedAbsence => ShiftAttendanceStatus.JustifiedAbsence,
                AttendanceNovelty.UnjustifiedAbsence => ShiftAttendanceStatus.UnjustifiedAbsence,
                AttendanceNovelty.Leave => ShiftAttendanceStatus.Leave,
                AttendanceNovelty.Vacation => ShiftAttendanceStatus.Vacation,
                _ => ShiftAttendanceStatus.Absent
            };
        }

        if (record is null || record.CheckIn is null)
            return ShiftAttendanceStatus.Absent;
        if (record.CheckOut is null)
            return ShiftAttendanceStatus.Incomplete;

        return ShiftAttendanceStatus.OnTime;
    }

    private static int TardinessMinutes(
        DateOnly date,
        TimeOnly expectedStart,
        AttendanceRecord? record)
    {
        if (record?.CheckIn is not DateTime checkIn)
            return 0;

        var localCheckIn = LocalTime.ToLocal(checkIn);
        var expected = date.ToDateTime(expectedStart);
        var minutes = (localCheckIn - expected).TotalMinutes;

        return minutes <= 0 ? 0 : (int)Math.Round(minutes);
    }

    private static void ValidateRange(DateOnly from, DateOnly to)
    {
        if (from > to)
            throw new InvalidShiftException("La fecha inicial no puede ser posterior a la final.");
        if (to.DayNumber - from.DayNumber > MaxWeekOffsetDays)
            throw new InvalidShiftException(
                $"El rango no puede superar los {MaxWeekOffsetDays} días.");
    }

    private static DateOnly StartOfWeek(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private DateOnly Today() => DateOnly.FromDateTime(LocalTime.ToLocal(_clock.UtcNow));

    private void Record(string action, int entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId.ToString(),
            detail,
            _currentUser.UserId,
            _clock.UtcNow,
            module: "hr",
            ip: _currentUser.Ip));
}
