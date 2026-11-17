namespace TiendaFlow.Domain;

public enum EmploymentContractStatus
{
    Active = 1,
    Ended = 2
}

/// <summary>
/// Labor contract of an employee with its validity. A fixed-term contract has
/// an end date and can be extended; ending a contract keeps it as history.
/// </summary>
public class EmploymentContract
{
    public EmploymentContract(
        int employeeId,
        ContractType type,
        DateOnly startDate,
        DateOnly? endDate,
        decimal salary,
        string? notes = null)
    {
        if (employeeId <= 0)
            throw new InvalidHrRecordException("The employee is required.");
        if (!Enum.IsDefined(type))
            throw new InvalidHrRecordException("The contract type is not valid.");
        if (startDate == default)
            throw new InvalidHrRecordException("The start date is required.");
        if (type is ContractType.FixedTerm && endDate is null)
            throw new InvalidHrRecordException("A fixed-term contract needs an end date.");
        if (endDate is DateOnly end && end < startDate)
            throw new InvalidHrRecordException("The end date cannot be before the start date.");
        if (salary < 0)
            throw new InvalidHrRecordException("The salary cannot be negative.");

        EmployeeId = employeeId;
        Type = type;
        StartDate = startDate;
        EndDate = endDate;
        Salary = salary;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Status = EmploymentContractStatus.Active;
    }

    public int Id { get; private set; }

    public int EmployeeId { get; private set; }

    public ContractType Type { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public decimal Salary { get; private set; }

    public string? Notes { get; private set; }

    public EmploymentContractStatus Status { get; private set; }

    public int Extensions { get; private set; }

    public bool IsActiveOn(DateOnly date) =>
        Status == EmploymentContractStatus.Active
        && date >= StartDate
        && (EndDate is null || date <= EndDate);

    public void Extend(DateOnly newEndDate)
    {
        if (Status != EmploymentContractStatus.Active)
            throw new InvalidHrRecordException("Only an active contract can be extended.");
        if (EndDate is null)
            throw new InvalidHrRecordException("An indefinite contract has no end date to extend.");
        if (newEndDate <= EndDate)
            throw new InvalidHrRecordException("The new end date must be after the current one.");

        EndDate = newEndDate;
        Extensions++;
    }

    public void End(DateOnly endDate)
    {
        if (Status != EmploymentContractStatus.Active)
            throw new InvalidHrRecordException("The contract has already ended.");
        if (endDate < StartDate)
            throw new InvalidHrRecordException("The end date cannot be before the start date.");

        EndDate = endDate;
        Status = EmploymentContractStatus.Ended;
    }
}

public enum AttendanceType
{
    Worked = 1,
    Absence = 2,
    SickLeave = 3,
    Permission = 4,
    Vacation = 5
}

/// <summary>
/// One day of an employee: worked hours from check-in and check-out, or a
/// novelty (absence, sick leave, permission, vacation). Payroll counts the
/// unpaid absences.
/// </summary>
public class AttendanceRecord
{
    public AttendanceRecord(
        int employeeId,
        DateOnly date,
        AttendanceType type,
        TimeOnly? checkIn,
        TimeOnly? checkOut,
        string? notes = null)
    {
        if (employeeId <= 0)
            throw new InvalidHrRecordException("The employee is required.");
        if (date == default)
            throw new InvalidHrRecordException("The date is required.");
        if (!Enum.IsDefined(type))
            throw new InvalidHrRecordException("The attendance type is not valid.");

        if (type == AttendanceType.Worked)
        {
            if (checkIn is null || checkOut is null)
                throw new InvalidHrRecordException("A worked day needs check-in and check-out times.");
            if (checkOut <= checkIn)
                throw new InvalidHrRecordException("The check-out must be after the check-in.");
        }
        else
        {
            checkIn = null;
            checkOut = null;
        }

        EmployeeId = employeeId;
        Date = date;
        Type = type;
        CheckIn = checkIn;
        CheckOut = checkOut;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public int Id { get; private set; }

    public int EmployeeId { get; private set; }

    public DateOnly Date { get; private set; }

    public AttendanceType Type { get; private set; }

    public TimeOnly? CheckIn { get; private set; }

    public TimeOnly? CheckOut { get; private set; }

    public string? Notes { get; private set; }

    public decimal HoursWorked =>
        CheckIn is TimeOnly start && CheckOut is TimeOnly end
            ? (decimal)(end - start).TotalHours
            : 0m;

    /// <summary>Unpaid days reduce the days worked in the payroll run.</summary>
    public bool IsUnpaidAbsence => Type == AttendanceType.Absence;
}

/// <summary>
/// Work shift of an employee in a store on a given day.
/// </summary>
public class WorkShift
{
    public WorkShift(int storeId, int employeeId, DateOnly date, TimeOnly start, TimeOnly end)
    {
        if (storeId <= 0)
            throw new InvalidHrRecordException("The store is required.");
        if (employeeId <= 0)
            throw new InvalidHrRecordException("The employee is required.");
        if (date == default)
            throw new InvalidHrRecordException("The date is required.");
        if (end <= start)
            throw new InvalidHrRecordException("The shift must end after it starts.");

        StoreId = storeId;
        EmployeeId = employeeId;
        Date = date;
        Start = start;
        End = end;
    }

    public int Id { get; private set; }

    public int StoreId { get; private set; }

    public int EmployeeId { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly Start { get; private set; }

    public TimeOnly End { get; private set; }

    public decimal Hours => (decimal)(End - Start).TotalHours;

    public bool Overlaps(WorkShift other) =>
        other.EmployeeId == EmployeeId
        && other.Date == Date
        && other.Id != Id
        && other.Start < End
        && Start < other.End;
}

public class InvalidHrRecordException : Exception
{
    public InvalidHrRecordException(string message) : base(message) { }
}

public class HrRecordNotFoundException : Exception
{
    public HrRecordNotFoundException(string what, int id) : base($"{what} {id} was not found.") { }
}
