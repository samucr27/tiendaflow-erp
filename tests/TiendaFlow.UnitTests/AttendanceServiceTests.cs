using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

/// <summary>
/// Application rules for attendance: check-in is limited to today, past days
/// are corrected with a novelty, the store scope is enforced before any
/// repository call, and the CSV uses the safe writer. No test uses a real
/// clock, a path or a database, so it behaves the same on every runner.
/// </summary>
public class AttendanceServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 16);

    private readonly InMemoryAttendanceRepository _attendance = new();
    private readonly FixedIdEmployeeRepository _employees = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly FakeCurrentUser _user = new(userId: 5, ip: "10.0.0.9");
    private readonly FixedClock _clock = new(Now);
    private readonly RecordingUnitOfWork _unitOfWork = new();

    public AttendanceServiceTests()
    {
        _employees.Add(new Employee(3, 1, "CC-1003", "Ana", "Cajera", "fijo", 2_000_000m));
        _employees.Add(new Employee(4, 2, "CC-1004", "Luis", "Vendedor", "fijo", 2_000_000m));
    }

    private AttendanceService Service(FakeCurrentUser? user = null, IAccessControl? access = null)
    {
        var activeUser = user ?? _user;
        return new AttendanceService(
            _attendance,
            _employees,
            new InMemoryStoreRepository(
                new Store(1, "Centro", "Calle 1"),
                new Store(2, "Norte", "Calle 2")),
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
        permissions: new HashSet<string>([Permissions.HrAttendanceManage], StringComparer.Ordinal));

    private static FakeCurrentUser GlobalHr() => new(
        userId: 9,
        role: RoleNames.HumanResourcesOfficer,
        permissions: new HashSet<string>([Permissions.HrAttendanceManage], StringComparer.Ordinal));

    [Fact]
    public void CheckInCreatesOneRecordForTodayAndAuditsWithoutPersonalData()
    {
        var id = Service().CheckIn(new AttendanceCheckInRequest(3));

        Assert.True(id > 0);
        var record = Assert.Single(_attendance.All());
        Assert.Equal(3, record.EmployeeId);
        Assert.Equal(1, record.StoreId);
        Assert.Equal(Today, record.WorkDate);
        Assert.Equal(Now, record.CheckIn);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("AttendanceCheckedIn", entry.Action);
        Assert.Equal("hr", entry.Module);
        Assert.Equal(5, entry.UserId);
        Assert.Equal("10.0.0.9", entry.Ip);
        Assert.Contains("empleado 3", entry.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("CC-1003", entry.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesAndTheirAuditShareOneTransaction()
    {
        var service = Service();

        service.CheckIn(new AttendanceCheckInRequest(3));
        Assert.Equal(1, _unitOfWork.Executions);

        var closingService = new AttendanceService(
            _attendance, _employees, new InMemoryStoreRepository(new Store(1, "Centro", "Calle 1")),
            new AlwaysAllowAccessControl(), _audit, _user, new FixedClock(Now.AddHours(8)), _unitOfWork);
        closingService.CheckOut(1);
        Assert.Equal(2, _unitOfWork.Executions);

        service.RegisterNovelty(new AttendanceNoveltyRequest(
            4, Today, AttendanceNovelty.JustifiedAbsence, "Control médico"));
        Assert.Equal(3, _unitOfWork.Executions);
    }

    [Fact]
    public void ASecondCheckInReusesTheSameDayRecord()
    {
        var service = Service();
        var first = service.CheckIn(new AttendanceCheckInRequest(3));
        Assert.Throws<InvalidAttendanceException>(() =>
            service.CheckIn(new AttendanceCheckInRequest(3)));

        Assert.Single(_attendance.All());
        Assert.Equal(first, _attendance.All()[0].Id);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void CheckInOnlyAcceptsToday(int offset)
    {
        var service = Service();
        var request = new AttendanceCheckInRequest(3, Today.AddDays(offset));

        Assert.Throws<InvalidAttendanceException>(() => service.CheckIn(request));
        Assert.Empty(_attendance.All());
    }

    [Fact]
    public void CheckOutClosesTheOpenRecordAndAuditsIt()
    {
        var service = Service();
        var id = service.CheckIn(new AttendanceCheckInRequest(3));
        // The clock is fixed, so simulate an instant after the check-in.
        var later = new FixedClock(Now.AddHours(8));
        var closingService = new AttendanceService(
            _attendance, _employees, new InMemoryStoreRepository(new Store(1, "Centro", "Calle 1")),
            new AlwaysAllowAccessControl(), _audit, _user, later, _unitOfWork);

        closingService.CheckOut(id);

        Assert.Equal(Now.AddHours(8), _attendance.GetById(id)!.CheckOut);
        Assert.Equal("AttendanceCheckedOut", _audit.Entries.Last().Action);
        Assert.Equal("hr", _audit.Entries.Last().Module);
    }

    [Fact]
    public void CheckOutOnAnUnknownRecordFails()
    {
        Assert.Throws<AttendanceRecordNotFoundException>(() => Service().CheckOut(999));
    }

    [Theory]
    [InlineData(-3)]
    [InlineData(0)]
    public void NoveltiesAcceptPastAndTodayButNotFuture(int offset)
    {
        var service = Service();
        var id = service.RegisterNovelty(new AttendanceNoveltyRequest(
            3, Today.AddDays(offset), AttendanceNovelty.JustifiedAbsence, "Permiso aprobado"));

        var record = _attendance.GetById(id)!;
        Assert.Equal(AttendanceNovelty.JustifiedAbsence, record.Novelty);
        Assert.Equal("Permiso aprobado", record.NoveltyReason);
        Assert.Equal(0m, record.HoursWorked);
        Assert.DoesNotContain("Permiso aprobado", _audit.Entries.Single().Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("CC-1003", _audit.Entries.Single().Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AFutureNoveltyIsRejectedBeforeWriting()
    {
        Assert.Throws<InvalidAttendanceException>(() => Service().RegisterNovelty(
            new AttendanceNoveltyRequest(3, Today.AddDays(1), AttendanceNovelty.Leave, "Motivo")));
        Assert.Empty(_attendance.All());
    }

    [Fact]
    public void AMarkedDayCannotAlsoHaveANovelty()
    {
        var service = Service();
        service.CheckIn(new AttendanceCheckInRequest(3));

        Assert.Throws<InvalidAttendanceException>(() => service.RegisterNovelty(
            new AttendanceNoveltyRequest(3, Today, AttendanceNovelty.Leave, "Motivo")));
    }

    [Fact]
    public void AnInactiveEmployeeCannotBeMarked()
    {
        _employees.Add(new Employee(5, 1, "CC-1005", "Inactivo", "Cajero", "fijo", 1m, active: false));

        Assert.Throws<InvalidAttendanceException>(() =>
            Service().CheckIn(new AttendanceCheckInRequest(5)));
        Assert.Empty(_attendance.All());
    }

    [Fact]
    public void ListsAreScopedByTheCurrentUserStore()
    {
        _attendance.Add(WorkedRecord(3, 1, Today));
        _attendance.Add(WorkedRecord(4, 2, Today));

        var managerRows = Service(ScopedManager(1)).List(Today, Today, null);
        Assert.Single(managerRows);
        Assert.Equal(3, managerRows[0].EmployeeId);

        var hrRows = Service(GlobalHr()).List(Today, Today, null);
        Assert.Equal(2, hrRows.Count);
    }

    [Fact]
    public void AScopedManagerCannotAskForAnotherStoreBeforeRepositoryAccess()
    {
        var service = Service(ScopedManager(1));

        Assert.Throws<AccessDeniedException>(() => service.List(Today, Today, 2));
        Assert.Throws<AccessDeniedException>(() => service.Summary(Today, Today, 2));
        Assert.Throws<AccessDeniedException>(() => service.Employees(2));
        Assert.Equal(0, _attendance.ListCalls);
    }

    [Fact]
    public void AScopedManagerCannotMarkOrCloseAnotherStoresEmployee()
    {
        var service = Service(ScopedManager(1));

        Assert.Throws<AccessDeniedException>(() =>
            service.CheckIn(new AttendanceCheckInRequest(4)));
        Assert.Throws<AccessDeniedException>(() =>
            service.RegisterNovelty(new AttendanceNoveltyRequest(
                4, Today, AttendanceNovelty.Leave, "Motivo")));
        Assert.Empty(_attendance.All());

        _attendance.Add(WorkedRecord(4, 2, Today));
        Assert.Throws<AccessDeniedException>(() => service.CheckOut(_attendance.All()[0].Id));
    }

    [Fact]
    public void AGlobalHrUserWorksAcrossStores()
    {
        var service = Service(GlobalHr());
        var id = service.CheckIn(new AttendanceCheckInRequest(4));

        Assert.Equal(2, _attendance.GetById(id)!.StoreId);
        Assert.Equal(2, service.Employees(null).Count);
    }

    [Fact]
    public void SummaryAggregatesPresenceHoursAndNovelties()
    {
        _attendance.Add(WorkedRecord(3, 1, Today));
        _attendance.Add(WorkedRecord(3, 1, Today.AddDays(-1)));
        var novelty = new AttendanceRecord(0, 3, 1, Today.AddDays(-2), Now);
        novelty.RegisterNovelty(AttendanceNovelty.Vacation, "Descanso", Now);
        _attendance.Add(novelty);

        var summary = Assert.Single(Service().Summary(Today.AddDays(-7), Today, null));

        Assert.Equal("Ana", summary.EmployeeName);
        Assert.Equal(2, summary.PresentDays);
        Assert.Equal(16m, summary.WorkedHours);
        Assert.Equal(1, summary.Vacations);
    }

    [Fact]
    public void ExportCsvNeutralizesSpreadsheetFormulas()
    {
        _employees.Add(new Employee(6, 1, "CC-1006", "=SUM(1+1)", "Cajera", "fijo", 1m));
        _attendance.Add(WorkedRecord(6, 1, Today));

        var csv = Service().SummaryCsv(Today, Today, null);

        Assert.Contains("'=SUM(1+1)", csv, StringComparison.Ordinal);
        Assert.Equal("AttendanceSummaryExported", _audit.Entries.Last().Action);
    }

    [Fact]
    public void RangeValidationRejectsInvertedAndTooLongRanges()
    {
        var service = Service();

        Assert.Throws<InvalidAttendanceException>(() => service.List(Today, Today.AddDays(-1), null));
        Assert.Throws<InvalidAttendanceException>(() => service.List(Today.AddDays(-400), Today, null));
    }

    [Fact]
    public void EveryDeniedOperationStopsBeforeRepositoriesAndAuditsTheAttempt()
    {
        var deniedAccess = new AccessControl(
            new FakeCurrentUser(permissions: new HashSet<string>(StringComparer.Ordinal)), _audit);
        var service = Service(access: deniedAccess);

        Assert.Throws<AccessDeniedException>(() => service.List(Today, Today, null));
        Assert.Throws<AccessDeniedException>(() => service.Employees(null));
        Assert.Throws<AccessDeniedException>(() => service.Stores());
        Assert.Throws<AccessDeniedException>(() => service.Summary(Today, Today, null));
        Assert.Throws<AccessDeniedException>(() =>
            service.CheckIn(new AttendanceCheckInRequest(3)));
        Assert.Throws<AccessDeniedException>(() => service.CheckOut(1));
        Assert.Throws<AccessDeniedException>(() => service.RegisterNovelty(
            new AttendanceNoveltyRequest(3, Today, AttendanceNovelty.Leave, "Motivo")));

        Assert.Equal(0, _attendance.ListCalls);
        Assert.Empty(_attendance.All());
        Assert.Equal(7, _audit.Entries.Count);
        Assert.All(_audit.Entries, entry =>
        {
            Assert.Equal("AccessDenied", entry.Action);
            Assert.Equal("security", entry.Module);
        });
    }

    private static AttendanceRecord WorkedRecord(int employeeId, int storeId, DateOnly date)
    {
        var record = new AttendanceRecord(0, employeeId, storeId, date, Now);
        record.RegisterCheckIn(date.ToDateTime(new TimeOnly(8, 0)));
        record.RegisterCheckOut(date.ToDateTime(new TimeOnly(16, 0)));
        return record;
    }
}
