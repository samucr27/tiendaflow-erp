namespace TiendaFlow.Domain;

/// <summary>
/// Non-working day reasons that can be registered instead of a check-in. There
/// is deliberately no health or incapacity value: the module records attendance
/// for payroll and scheduling, not medical information.
/// </summary>
public enum AttendanceNovelty
{
    JustifiedAbsence = 1,
    UnjustifiedAbsence = 2,
    Leave = 3,
    Vacation = 4
}

/// <summary>
/// Attendance of one employee for one business day. A day is either worked
/// (check-in and optional check-out) or covered by a novelty (justified or
/// unjustified absence, leave, vacation); the two states cannot be mixed, so a
/// day with a novelty can never produce worked hours.
/// </summary>
public class AttendanceRecord
{
    public const int MaxReasonLength = 300;

    public AttendanceRecord(
        int id,
        int employeeId,
        int storeId,
        DateOnly workDate,
        DateTime createdAt)
    {
        if (id < 0)
            throw new InvalidAttendanceException("El identificador del registro no puede ser negativo.");
        if (employeeId <= 0)
            throw new InvalidAttendanceException("El empleado es obligatorio.");
        if (storeId <= 0)
            throw new InvalidAttendanceException("La tienda es obligatoria.");
        if (workDate == default)
            throw new InvalidAttendanceException("La fecha de trabajo es obligatoria.");

        Id = id;
        EmployeeId = employeeId;
        StoreId = storeId;
        WorkDate = workDate;
        CreatedAt = UtcTimestamp.Normalize(createdAt);
    }

    public int Id { get; private set; }

    public int EmployeeId { get; private set; }

    public int StoreId { get; private set; }

    public DateOnly WorkDate { get; private set; }

    public DateTime? CheckIn { get; private set; }

    public DateTime? CheckOut { get; private set; }

    public AttendanceNovelty? Novelty { get; private set; }

    public string? NoveltyReason { get; private set; }

    public DateTime CreatedAt { get; }

    public bool IsPresent => CheckIn is not null;

    public bool IsOpen => CheckIn is not null && CheckOut is null;

    public bool HasNovelty => Novelty is not null;

    public decimal HoursWorked =>
        CheckIn is DateTime start && CheckOut is DateTime end
            ? Math.Round((decimal)(end - start).TotalHours, 2)
            : 0m;

    public void RegisterCheckIn(DateTime at)
    {
        if (HasNovelty)
            throw new InvalidAttendanceException(
                "El día ya tiene una novedad registrada; no se puede marcar entrada.");
        if (CheckIn is not null)
            throw new InvalidAttendanceException("El empleado ya tiene una entrada registrada para este día.");

        CheckIn = UtcTimestamp.Normalize(at);
    }

    public void RegisterCheckOut(DateTime at)
    {
        if (CheckIn is null)
            throw new InvalidAttendanceException("No se puede registrar la salida sin una entrada previa.");
        if (CheckOut is not null)
            throw new InvalidAttendanceException("El empleado ya tiene una salida registrada para este día.");

        var instant = UtcTimestamp.Normalize(at);

        if (instant <= CheckIn.Value)
            throw new InvalidAttendanceException("La salida debe ser posterior a la entrada.");

        CheckOut = instant;
    }

    public void RegisterNovelty(
        AttendanceNovelty novelty,
        string? reason,
        DateTime at)
    {
        if (CheckIn is not null || CheckOut is not null)
            throw new InvalidAttendanceException(
                "El día tiene marcaciones de entrada o salida; no se puede registrar una novedad.");
        if (Novelty is not null)
            throw new InvalidAttendanceException("El día ya tiene una novedad registrada.");
        if (!Enum.IsDefined(novelty))
            throw new InvalidAttendanceException("El tipo de novedad no es válido.");

        var normalizedReason = reason?.Trim();

        if (novelty != AttendanceNovelty.UnjustifiedAbsence
            && string.IsNullOrWhiteSpace(normalizedReason))
        {
            throw new InvalidAttendanceException(
                "Describe el motivo de la novedad.");
        }

        if (normalizedReason?.Length > MaxReasonLength)
            throw new InvalidAttendanceException(
                $"El motivo no puede superar los {MaxReasonLength} caracteres.");

        Novelty = novelty;
        NoveltyReason = string.IsNullOrWhiteSpace(normalizedReason) ? null : normalizedReason;
    }
}

/// <summary>
/// Reusable shift definition: a name plus a start and end time of the same day.
/// Assignments copy the times so changing a template never rewrites a shift
/// that was already scheduled.
/// </summary>
public class ShiftTemplate
{
    public const int MaxNameLength = 80;

    public ShiftTemplate(
        int id,
        string name,
        TimeOnly startTime,
        TimeOnly endTime,
        DateTime createdAt,
        bool active = true)
    {
        if (id < 0)
            throw new InvalidShiftException("El identificador de la plantilla no puede ser negativo.");

        var normalizedName = name?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedName))
            throw new InvalidShiftException("El nombre de la plantilla es obligatorio.");
        if (normalizedName.Length > MaxNameLength)
            throw new InvalidShiftException(
                $"El nombre de la plantilla no puede superar los {MaxNameLength} caracteres.");
        if (endTime <= startTime)
            throw new InvalidShiftException("La hora de fin debe ser posterior a la hora de inicio.");

        Id = id;
        Name = normalizedName;
        StartTime = startTime;
        EndTime = endTime;
        Active = active;
        CreatedAt = UtcTimestamp.Normalize(createdAt);
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    public bool Active { get; private set; }

    public DateTime CreatedAt { get; }

    public decimal DurationHours => (decimal)(EndTime - StartTime).TotalHours;

    public void Deactivate() => Active = false;
}

/// <summary>
/// A shift scheduled for one employee, one date and one store. The template is
/// referenced for traceability, but the start and end times are copied so the
/// history of the schedule stays stable.
/// </summary>
public class ShiftAssignment
{
    public ShiftAssignment(
        int id,
        int employeeId,
        int storeId,
        int shiftTemplateId,
        DateOnly workDate,
        TimeOnly startTime,
        TimeOnly endTime,
        DateTime createdAt)
    {
        if (id < 0)
            throw new InvalidShiftException("El identificador del turno no puede ser negativo.");
        if (employeeId <= 0)
            throw new InvalidShiftException("El empleado es obligatorio.");
        if (storeId <= 0)
            throw new InvalidShiftException("La tienda es obligatoria.");
        if (shiftTemplateId <= 0)
            throw new InvalidShiftException("La plantilla de turno es obligatoria.");
        if (workDate == default)
            throw new InvalidShiftException("La fecha del turno es obligatoria.");
        if (endTime <= startTime)
            throw new InvalidShiftException("La hora de fin debe ser posterior a la hora de inicio.");

        Id = id;
        EmployeeId = employeeId;
        StoreId = storeId;
        ShiftTemplateId = shiftTemplateId;
        WorkDate = workDate;
        StartTime = startTime;
        EndTime = endTime;
        CreatedAt = UtcTimestamp.Normalize(createdAt);
    }

    public int Id { get; private set; }

    public int EmployeeId { get; private set; }

    public int StoreId { get; private set; }

    public int ShiftTemplateId { get; private set; }

    public DateOnly WorkDate { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    public DateTime CreatedAt { get; }

    public decimal DurationHours => (decimal)(EndTime - StartTime).TotalHours;

    public bool Overlaps(TimeOnly start, TimeOnly end) =>
        start < EndTime && end > StartTime;

    public bool Overlaps(ShiftAssignment other) =>
        other.WorkDate == WorkDate && Overlaps(other.StartTime, other.EndTime);

    public void EnsureCancellable(DateOnly today)
    {
        if (WorkDate < today)
            throw new InvalidShiftException("No se puede cancelar un turno de una fecha pasada.");
    }
}
