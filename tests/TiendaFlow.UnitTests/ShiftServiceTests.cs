using TiendaFlow.Application;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

/// <summary>
/// Application rules for shifts: duplicate and overlong templates, employee /
/// store consistency, active template, overlap and daily cap, weekly scope and
/// the shift-versus-attendance report. The report uses Colombia-local times
/// built through LocalTime, so the assertions are timezone independent.
/// </summary>
public class ShiftServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 16);
    private const string MaxPerShift = SystemParameterCatalog.MaxHoursPerShift;
    private const string MaxPerDay = SystemParameterCatalog.MaxHoursPerDay;

    private readonly InMemoryShiftRepository _shifts = new();
    private readonly InMemoryAttendanceRepository _attendance = new();
    private readonly FixedIdEmployeeRepository _employees = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly FakeCurrentUser _user = new(userId: 5, ip: "10.0.0.9");
    private readonly FixedClock _clock = new(Now);
    private readonly StubDecimalParameterValues _parameters = new();
    private readonly RecordingUnitOfWork _unitOfWork = new();

    public ShiftServiceTests()
    {
        _parameters.With(MaxPerShift, 10m).With(MaxPerDay, 12m);
        _employees.Add(new Employee(3, 1, "CC-1003", "Ana", "Cajera", "fijo", 1m));
        _employees.Add(new Employee(4, 2, "CC-1004", "Luis", "Vendedor", "fijo", 1m));
    }

    private ShiftService Service(FakeCurrentUser? user = null, IAccessControl? access = null)
    {
        var activeUser = user ?? _user;
        return new ShiftService(
            _shifts,
            _attendance,
            _employees,
            new InMemoryStoreRepository(
                new Store(1, "Centro", "Calle 1"),
                new Store(2, "Norte", "Calle 2")),
            _parameters,
            access ?? new AccessControl(activeUser, _audit),
            _audit,
            activeUser,
            _clock,
            _unitOfWork);
    }

    private static FakeCurrentUser ScopedManager(int storeId) => new(
        userId: 8,
        role: RoleNames.StoreManager,
        storeId: storeId,
        permissions: new HashSet<string>([Permissions.HrShiftsManage], StringComparer.Ordinal));

    private static FakeCurrentUser GlobalHr() => new(
        userId: 9,
        role: RoleNames.HumanResourcesOfficer,
        permissions: new HashSet<string>([Permissions.HrShiftsManage], StringComparer.Ordinal));

    private int MorningTemplate()
    {
        var id = Service().CreateTemplate(new ShiftTemplateRequest(
            "Mañana", new TimeOnly(7, 0), new TimeOnly(15, 0)));
        _audit.Entries.Clear();
        return id;
    }

    [Fact]
    public void TemplatesAreCreatedValidatedAndAudited()
    {
        var service = Service();
        var id = service.CreateTemplate(new ShiftTemplateRequest(
            "  Mañana  ", new TimeOnly(7, 0), new TimeOnly(15, 0)));

        var template = Assert.Single(_shifts.AllTemplates());
        Assert.Equal("Mañana", template.Name);
        Assert.Equal(8m, template.DurationHours);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("ShiftTemplateCreated", entry.Action);
        Assert.Equal("hr", entry.Module);
        Assert.Equal(5, entry.UserId);
        Assert.Equal("10.0.0.9", entry.Ip);

        Assert.Throws<InvalidShiftException>(() => service.CreateTemplate(
            new ShiftTemplateRequest("Mañana", new TimeOnly(8, 0), new TimeOnly(16, 0))));
    }

    [Fact]
    public void TemplateAssignmentAndCancellationShareOneTransactionWithTheirAudit()
    {
        var service = Service();
        var templateId = service.CreateTemplate(new ShiftTemplateRequest(
            "Mañana", new TimeOnly(7, 0), new TimeOnly(15, 0)));
        Assert.Equal(1, _unitOfWork.Executions);

        var assignmentId = service.Assign(new ShiftAssignmentRequest(
            3, 1, templateId, Today.AddDays(1)));
        Assert.Equal(2, _unitOfWork.Executions);

        service.Cancel(assignmentId);
        Assert.Equal(3, _unitOfWork.Executions);
    }

    [Fact]
    public void AnOverlongTemplateIsRejectedByTheConfiguredCap()
    {
        _parameters.With(MaxPerShift, 6m);

        Assert.Throws<InvalidShiftException>(() => Service().CreateTemplate(
            new ShiftTemplateRequest("Larga", new TimeOnly(7, 0), new TimeOnly(15, 0))));
        Assert.Empty(_shifts.AllTemplates());
    }

    [Fact]
    public void AssignCopiesTheTemplateTimesAndAuditsWithAnEmployeeId()
    {
        var templateId = MorningTemplate();
        var id = Service().Assign(new ShiftAssignmentRequest(
            3, 1, templateId, Today.AddDays(1)));

        var assignment = _shifts.GetAssignment(id)!;
        Assert.Equal(3, assignment.EmployeeId);
        Assert.Equal(new TimeOnly(7, 0), assignment.StartTime);
        Assert.Equal(new TimeOnly(15, 0), assignment.EndTime);
        Assert.Equal(8m, assignment.DurationHours);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("ShiftAssigned", entry.Action);
        Assert.Equal("hr", entry.Module);
        Assert.Contains("empleado 3", entry.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("CC-1003", entry.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void APastShiftIsRejectedEvenWithAValidTemplate()
    {
        var templateId = MorningTemplate();

        Assert.Throws<InvalidShiftException>(() => Service().Assign(
            new ShiftAssignmentRequest(3, 1, templateId, Today.AddDays(-1))));
        Assert.Empty(_shifts.AllAssignments());
    }

    [Fact]
    public void AssignmentRejectsInactiveEmployeesStoresAndTemplates()
    {
        var templateId = MorningTemplate();
        _employees.Add(new Employee(5, 1, "CC-1005", "Inactivo", "Cajero", "fijo", 1m, active: false));
        var inactiveStore = new InMemoryStoreRepository(new Store(1, "Centro", "Calle 1", active: false));
        var inactiveTemplateService = new ShiftService(
            _shifts, _attendance, _employees,
            new InMemoryStoreRepository(new Store(1, "Centro", "Calle 1")),
            _parameters, new AlwaysAllowAccessControl(), _audit, _user, _clock, _unitOfWork);
        var inactiveTemplate = new ShiftTemplate(
            0, "Inactiva", new TimeOnly(7, 0), new TimeOnly(15, 0), Now, active: false);
        _shifts.AddTemplate(inactiveTemplate);

        Assert.Throws<InvalidShiftException>(() => Service().Assign(
            new ShiftAssignmentRequest(5, 1, templateId, Today)));
        Assert.Throws<InvalidShiftException>(() => Service().Assign(
            new ShiftAssignmentRequest(3, 2, templateId, Today)));

        var withInactiveStore = new ShiftService(
            _shifts, _attendance, _employees, inactiveStore,
            _parameters, new AlwaysAllowAccessControl(), _audit, _user, _clock, _unitOfWork);
        Assert.Throws<InvalidShiftException>(() => withInactiveStore.Assign(
            new ShiftAssignmentRequest(3, 1, templateId, Today)));

        Assert.Throws<InvalidShiftException>(() => inactiveTemplateService.Assign(
            new ShiftAssignmentRequest(3, 1, inactiveTemplate.Id, Today)));
        Assert.Empty(_shifts.AllAssignments());
    }

    [Fact]
    public void AssignmentNeedsAnExistingTemplate()
    {
        Assert.Throws<ShiftTemplateNotFoundException>(() => Service().Assign(
            new ShiftAssignmentRequest(3, 1, 999, Today)));
    }

    [Fact]
    public void OverlappingShiftsCannotBeAssignedOnTheSameDay()
    {
        var morning = MorningTemplate();
        var afternoon = Service().CreateTemplate(new ShiftTemplateRequest(
            "Tarde", new TimeOnly(14, 0), new TimeOnly(22, 0)));
        var service = Service();
        service.Assign(new ShiftAssignmentRequest(3, 1, morning, Today.AddDays(1)));

        Assert.Throws<InvalidShiftException>(() => service.Assign(
            new ShiftAssignmentRequest(3, 1, afternoon, Today.AddDays(1))));
        Assert.Single(_shifts.AllAssignments());
    }

    [Fact]
    public void TheSumOfShiftsCannotExceedTheDailyCap()
    {
        var morning = MorningTemplate();
        var evening = Service().CreateTemplate(new ShiftTemplateRequest(
            "Cierre", new TimeOnly(15, 30), new TimeOnly(20, 0)));
        var service = Service();
        service.Assign(new ShiftAssignmentRequest(3, 1, morning, Today.AddDays(1)));

        Assert.Throws<InvalidShiftException>(() => service.Assign(
            new ShiftAssignmentRequest(3, 1, evening, Today.AddDays(1))));
        Assert.Single(_shifts.AllAssignments());
    }

    [Fact]
    public void AScopedManagerCannotAssignOrCancelAnotherStoresShift()
    {
        var templateId = MorningTemplate();
        var manager = ScopedManager(1);
        var service = Service(manager);

        Assert.Throws<AccessDeniedException>(() => service.Assign(
            new ShiftAssignmentRequest(4, 2, templateId, Today.AddDays(1))));
        Assert.Empty(_shifts.AllAssignments());

        var otherStoreAssignment = new ShiftAssignment(
            0, 4, 2, templateId, Today.AddDays(1), new TimeOnly(7, 0), new TimeOnly(15, 0), Now);
        _shifts.AddAssignment(otherStoreAssignment);

        Assert.Throws<AccessDeniedException>(() => service.Cancel(otherStoreAssignment.Id));
        Assert.Single(_shifts.AllAssignments());
    }

    [Fact]
    public void AGlobalHrUserAssignsInAnyStoreAndCancelsFutureShifts()
    {
        var templateId = MorningTemplate();
        var hrService = Service(GlobalHr());
        var id = hrService.Assign(new ShiftAssignmentRequest(
            4, 2, templateId, Today.AddDays(1)));

        hrService.Cancel(id);

        Assert.Empty(_shifts.AllAssignments());
        Assert.Equal("ShiftCancelled", _audit.Entries.Last().Action);
        Assert.Equal("hr", _audit.Entries.Last().Module);
    }

    [Fact]
    public void AScopedManagerCannotReadAnotherStoresWeekBeforeRepositoryAccess()
    {
        var service = Service(ScopedManager(1));

        Assert.Throws<AccessDeniedException>(() => service.Week(Today, 2));
        Assert.Throws<AccessDeniedException>(() => service.AttendanceReport(Today, Today, 2));
        Assert.Equal(0, _shifts.AssignmentListCalls);
        Assert.Equal(0, _attendance.ListCalls);
    }

    [Fact]
    public void AWeeksEmployeesAreScopedAndOutOfRangeDatesAreRejected()
    {
        var manager = Service(ScopedManager(1));
        var week = manager.Week(Today, null);

        Assert.Single(week.Employees);
        Assert.Equal(3, week.Employees[0].Id);
        Assert.Equal(7, week.Days.Count);
        Assert.Equal(Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7)), week.Start);

        Assert.Throws<InvalidShiftException>(() => manager.Week(Today.AddDays(400), null));
    }

    [Fact]
    public void TheReportDistinguishesOnTimeLateIncompleteAbsentAndNoveltyDays()
    {
        var templateId = MorningTemplate();
        var from = Today.AddDays(-10);

        // One assignment per date keeps each report row independent.
        var dates = Enumerable.Range(1, 5).Select(offset => Today.AddDays(-offset)).ToArray();
        foreach (var date in dates)
        {
            _shifts.AddAssignment(new ShiftAssignment(
                0, 3, 1, templateId, date, new TimeOnly(7, 0), new TimeOnly(15, 0), Now));
        }

        AddMarks(dates[0], lateMinutes: 0, withCheckOut: true);
        AddMarks(dates[1], lateMinutes: 30, withCheckOut: true);
        AddMarks(dates[2], lateMinutes: 0, withCheckOut: false);
        AddNovelty(dates[4], AttendanceNovelty.Vacation);
        // dates[3] deliberately has no attendance record.

        var report = Service().AttendanceReport(from, Today, null);

        Assert.Equal(5, report.Rows.Count);
        Assert.Equal(ShiftAttendanceStatus.OnTime, StatusOf(report, dates[0]));
        Assert.Equal(ShiftAttendanceStatus.Late, StatusOf(report, dates[1]));
        Assert.Equal(ShiftAttendanceStatus.Incomplete, StatusOf(report, dates[2]));
        Assert.Equal(ShiftAttendanceStatus.Absent, StatusOf(report, dates[3]));
        Assert.Equal(ShiftAttendanceStatus.Vacation, StatusOf(report, dates[4]));

        Assert.Equal(30, report.Rows.Single(row => row.Date == dates[1]).TardinessMinutes);
        Assert.Equal(1, report.LateCount);
        Assert.Equal(2, report.AbsenceCount); // absent + vacation
        Assert.Equal(15.5m, report.WorkedHours); // 8h on time + 7.5h late
    }

    [Fact]
    public void TheReportCsvUsesLocalTimesAndNeutralizesFormulas()
    {
        _employees.Add(new Employee(6, 1, "CC-1006", "+SUM(1+1)", "Cajera", "fijo", 1m));
        var templateId = MorningTemplate();
        _shifts.AddAssignment(new ShiftAssignment(
            0, 6, 1, templateId, Today.AddDays(-1), new TimeOnly(7, 0), new TimeOnly(15, 0), Now));
        AddMarks(Today.AddDays(-1), lateMinutes: 0, withCheckOut: true, employeeId: 6);

        var csv = Service().AttendanceReportCsv(Today.AddDays(-2), Today, null);

        Assert.Contains("'+SUM(1+1)", csv, StringComparison.Ordinal);
        Assert.Contains("07:00", csv, StringComparison.Ordinal);
        Assert.Equal("ShiftAttendanceExported", _audit.Entries.Last().Action);
    }

    [Fact]
    public void AnEmptyReportNeedsNoAttendanceRepository()
    {
        var report = Service().AttendanceReport(Today.AddDays(-3), Today, null);

        Assert.Empty(report.Rows);
        Assert.Equal(0m, report.WorkedHours);
    }

    [Fact]
    public void ReportRangeValidationRejectsInvertedAndTooLongRanges()
    {
        var service = Service();

        Assert.Throws<InvalidShiftException>(() => service.AttendanceReport(Today, Today.AddDays(-1), null));
        Assert.Throws<InvalidShiftException>(() => service.AttendanceReport(Today.AddDays(-400), Today, null));
    }

    [Fact]
    public void EveryDeniedOperationStopsBeforeRepositoriesAndAuditsTheAttempt()
    {
        var deniedAccess = new AccessControl(
            new FakeCurrentUser(permissions: new HashSet<string>(StringComparer.Ordinal)), _audit);
        var service = Service(access: deniedAccess);

        Assert.Throws<AccessDeniedException>(() => service.Stores());
        Assert.Throws<AccessDeniedException>(() => service.Templates());
        Assert.Throws<AccessDeniedException>(() => service.CreateTemplate(
            new ShiftTemplateRequest("Mañana", new TimeOnly(7, 0), new TimeOnly(15, 0))));
        Assert.Throws<AccessDeniedException>(() => service.Week(Today, null));
        Assert.Throws<AccessDeniedException>(() => service.Assign(
            new ShiftAssignmentRequest(3, 1, 1, Today)));
        Assert.Throws<AccessDeniedException>(() => service.Cancel(1));
        Assert.Throws<AccessDeniedException>(() => service.AttendanceReport(Today, Today, null));

        Assert.Equal(0, _shifts.TemplateListCalls);
        Assert.Equal(0, _shifts.AssignmentListCalls);
        Assert.Equal(0, _attendance.ListCalls);
        Assert.Empty(_shifts.AllTemplates());
        Assert.Empty(_shifts.AllAssignments());
        Assert.Equal(7, _audit.Entries.Count);
        Assert.All(_audit.Entries, entry =>
        {
            Assert.Equal("AccessDenied", entry.Action);
            Assert.Equal("security", entry.Module);
        });
    }

    private void AddMarks(
        DateOnly date,
        int lateMinutes,
        bool withCheckOut,
        int employeeId = 3)
    {
        var checkIn = LocalTime.ToUtc(date).AddHours(7).AddMinutes(lateMinutes);
        var record = new AttendanceRecord(0, employeeId, 1, date, Now);
        record.RegisterCheckIn(checkIn);
        if (withCheckOut)
            record.RegisterCheckOut(LocalTime.ToUtc(date).AddHours(15));
        _attendance.Add(record);
    }

    private void AddNovelty(DateOnly date, AttendanceNovelty novelty)
    {
        var record = new AttendanceRecord(0, 3, 1, date, Now);
        record.RegisterNovelty(novelty, "Descanso programado", Now);
        _attendance.Add(record);
    }

    private static ShiftAttendanceStatus StatusOf(ShiftAttendanceReport report, DateOnly date) =>
        report.Rows.Single(row => row.Date == date).Status;
}
