using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// Real SQL Server coverage for attendance and shifts: DateOnly columns, the
/// one-row-per-employee-and-day uniqueness, the shift read model and the audit
/// trail written by the application services. Authorization is covered by unit
/// and structural tests, so these scenarios allow every operation.
/// </summary>
public class HrWorkforceIntegrationTests : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(LocalTime.ToLocal(Now));

    private readonly SqlServerDatabaseFixture _database;

    public HrWorkforceIntegrationTests(SqlServerDatabaseFixture database) => _database = database;

    private TiendaFlowDbContext NewContext() =>
        new(new DbContextOptionsBuilder<TiendaFlowDbContext>()
            .UseSqlServer(_database.ConnectionString!)
            .Options);

    private static AttendanceService Attendance(TiendaFlowDbContext context, IClock? clock = null) =>
        new(
            new EfAttendanceRepository(context),
            new EfEmployeeRepository(context),
            new EfStoreRepository(context),
            new AllowAllAccessControl(),
            new EfAuditLog(context),
            new TestCurrentUser(),
            clock ?? new TestClock(Now),
            new EfUnitOfWork(context));

    private static ShiftService Shifts(TiendaFlowDbContext context) =>
        new(
            new EfShiftRepository(context),
            new EfAttendanceRepository(context),
            new EfEmployeeRepository(context),
            new EfStoreRepository(context),
            new ParameterValues(new EfSystemParameterRepository(context), new TestClock(Now)),
            new AllowAllAccessControl(),
            new EfAuditLog(context),
            new TestCurrentUser(),
            new TestClock(Now),
            new EfUnitOfWork(context));

    private static Employee NewEmployee(TiendaFlowDbContext context, string? name = null)
    {
        var employee = new Employee(
            0,
            1,
            $"CC-{Guid.NewGuid():N}"[..12],
            name ?? "Empleado de integración",
            "Cajero",
            "Indefinido",
            2_200_000m);

        new EfEmployeeRepository(context).Add(employee);
        return employee;
    }

    [SqlServerFact]
    public async Task CheckInAndCheckOutArePersistedOnceForTheEmployeeAndDay()
    {
        await using var context = NewContext();
        var employee = NewEmployee(context);
        var attendance = Attendance(context);

        var id = attendance.CheckIn(new AttendanceCheckInRequest(employee.Id));

        Assert.Single(await context.AttendanceRecords
            .Where(row => row.EmployeeId == employee.Id && row.WorkDate == Today)
            .ToListAsync());
        Assert.Throws<InvalidAttendanceException>(() =>
            attendance.CheckIn(new AttendanceCheckInRequest(employee.Id)));

        Attendance(context, new TestClock(Now.AddHours(8))).CheckOut(id);

        var stored = await context.AttendanceRecords.SingleAsync(row => row.Id == id);
        Assert.Equal(Now, stored.CheckIn);
        Assert.Equal(Now.AddHours(8), stored.CheckOut);
        Assert.Equal(8m, stored.HoursWorked);

        var actions = await context.AuditEntries
            .Where(entry => entry.Module == "hr"
                && (entry.Action == "AttendanceCheckedIn" || entry.Action == "AttendanceCheckedOut"))
            .OrderBy(entry => entry.Id)
            .Select(entry => entry.Action)
            .ToListAsync();
        Assert.Equal(new[] { "AttendanceCheckedIn", "AttendanceCheckedOut" }, actions);
    }

    [SqlServerFact]
    public async Task ANoveltyAndItsShiftProduceOneReportRowWithoutWorkedHours()
    {
        await using var context = NewContext();
        var employee = NewEmployee(context);
        var shifts = Shifts(context);

        var templateName = $"Mañana-{Guid.NewGuid():N}"[..18];
        var templateId = shifts.CreateTemplate(new ShiftTemplateRequest(
            templateName, new TimeOnly(7, 0), new TimeOnly(15, 0)));
        var assignmentId = shifts.Assign(new ShiftAssignmentRequest(
            employee.Id, 1, templateId, Today));

        Attendance(context).RegisterNovelty(new AttendanceNoveltyRequest(
            employee.Id, Today, AttendanceNovelty.JustifiedAbsence, "Permiso aprobado"));

        var report = shifts.AttendanceReport(Today, Today, null);
        // The SQL fixture is shared, so other tests may have shifts on the same
        // date. Filter this scenario by the employee it created.
        var row = Assert.Single(report.Rows, item => item.EmployeeId == employee.Id);
        Assert.Equal(ShiftAttendanceStatus.JustifiedAbsence, row.Status);
        Assert.Equal(0m, row.WorkedHours);
        Assert.Equal(0, row.TardinessMinutes);

        Assert.Equal(1, await context.ShiftAssignments.CountAsync(item => item.Id == assignmentId));
        Assert.Equal(1, await context.AttendanceRecords.CountAsync(item => item.EmployeeId == employee.Id));

        shifts.Cancel(assignmentId);
        Assert.Equal(0, await context.ShiftAssignments.CountAsync(item => item.Id == assignmentId));
    }

    [SqlServerFact]
    public async Task TheWeeklyScheduleAndTheExportReadThePersistedRows()
    {
        await using var context = NewContext();
        var employee = NewEmployee(context, "=SUM(1+1)");
        var shifts = Shifts(context);

        var templateName = $"Mañana-{Guid.NewGuid():N}"[..18];
        var templateId = shifts.CreateTemplate(new ShiftTemplateRequest(
            templateName, new TimeOnly(7, 0), new TimeOnly(15, 0)));
        shifts.Assign(new ShiftAssignmentRequest(employee.Id, 1, templateId, Today));

        var week = shifts.Week(Today, null);
        var assignment = Assert.Single(week.Assignments, item => item.EmployeeId == employee.Id);
        Assert.Equal(templateName, assignment.TemplateName);
        Assert.Equal(8m, assignment.Hours);

        var record = new AttendanceRecord(0, employee.Id, 1, Today, Now);
        record.RegisterCheckIn(LocalTime.ToUtc(Today).AddHours(8));
        record.RegisterCheckOut(LocalTime.ToUtc(Today).AddHours(16));
        new EfAttendanceRepository(context).Add(record);

        var csv = Attendance(context).SummaryCsv(Today, Today, null);
        Assert.DoesNotContain("=SUM(1+1)\r\n", csv, StringComparison.Ordinal);
        Assert.Contains("'=SUM(1+1)", csv, StringComparison.Ordinal);
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime utcNow) => UtcNow = utcNow;

        public DateTime UtcNow { get; }
    }
}
