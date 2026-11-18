using Moq;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

/// <summary>
/// The report is pure application logic over the audit log, so its KPI
/// classification, local-date grouping and CSV export are verified without SQL
/// Server. The integration tests cover the real EF query against audit_log.
/// </summary>
public class SecurityReportServiceTests
{
    private static readonly DateOnly Day = new(2026, 11, 10);

    [Fact]
    public void WithoutTheReportsPermission_IsRejectedBeforeQuerying()
    {
        var query = new Mock<ISecurityEventQuery>();
        var service = new SecurityReportService(query.Object, RestrictedTo(Permissions.SecurityAuditView));

        Assert.Throws<AccessDeniedException>(() => service.Get(Day, Day));
        query.Verify(
            source => source.InRange(It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public void PassesTheUtcInstantsOfTheInclusiveLocalRange()
    {
        var query = new Mock<ISecurityEventQuery>();
        query
            .Setup(source => source.InRange(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(Array.Empty<AuditTrailRow>());

        var service = new SecurityReportService(query.Object, AllowAll);
        service.Get(Day, Day);

        query.Verify(source => source.InRange(
            LocalTime.ToUtc(Day),
            LocalTime.ToUtc(Day.AddDays(1))), Times.Once);
    }

    [Fact]
    public void RejectsAnInvertedRange()
    {
        var query = new Mock<ISecurityEventQuery>();
        var service = new SecurityReportService(query.Object, AllowAll);

        Assert.Throws<ArgumentException>(() => service.Get(Day, Day.AddDays(-1)));
        query.Verify(
            source => source.InRange(It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public void ClassifiesKpisAndGroupsByColombianBusinessDate()
    {
        // 02:00 UTC on the 11th is 21:00 local on the 10th: the daily bucket
        // follows the store's date, not the instant's UTC date.
        var localTenMorning = LocalTime.ToUtc(Day).AddHours(9);
        var localTenNight = LocalTime.ToUtc(Day).AddHours(21);
        var events = new[]
        {
            Row(1, SecurityAuditActions.LoginSucceeded, 1, localTenMorning),
            Row(2, SecurityAuditActions.LoginSucceeded, 2, localTenMorning.AddHours(1)),
            Row(3, SecurityAuditActions.LoginFailed, null, localTenMorning.AddHours(2)),
            Row(4, SecurityAuditActions.AccessDenied, 1, localTenNight),
            Row(5, "UserCreated", 3, localTenNight),
            Row(6, "UserUpdated", 3, localTenNight),
            Row(7, "UserDeactivated", 3, localTenNight),
            Row(8, "PasswordReset", 3, localTenNight),
            Row(9, "SomeOtherAction", 1, localTenNight),
            // This instant is UTC on the 11th but local on the 10th.
            Row(10, SecurityAuditActions.LoginFailed, null, new DateTime(2026, 11, 11, 2, 0, 0, DateTimeKind.Utc))
        };

        var report = CreateService(events).Get(Day, Day);

        Assert.Equal(2, report.SuccessfulLogins);
        Assert.Equal(2, report.FailedLogins);
        Assert.Equal(1, report.AccessDenials);
        Assert.Equal(4, report.UserChanges);
        Assert.Equal(10, report.TotalEvents);
        Assert.Equal(3, report.DistinctActors);

        var daily = Assert.Single(report.Daily);
        Assert.Equal(Day, daily.Date);
        Assert.Equal(2, daily.SuccessfulLogins);
        Assert.Equal(2, daily.FailedLogins);
        Assert.Equal(1, daily.AccessDenials);
        Assert.Equal(4, daily.UserChanges);
        Assert.Equal(10, daily.TotalEvents);
    }

    [Fact]
    public void EventsAreOrderedByInstantAndId()
    {
        var earlier = LocalTime.ToUtc(Day).AddHours(8);
        var events = new[]
        {
            Row(3, SecurityAuditActions.LoginSucceeded, 1, earlier.AddHours(1)),
            Row(2, SecurityAuditActions.LoginSucceeded, 1, earlier),
            Row(1, SecurityAuditActions.LoginSucceeded, 1, earlier)
        };

        var report = CreateService(events).Get(Day, Day);

        Assert.Equal(new long[] { 1, 2, 3 }, report.Events.Select(entry => entry.Id));
    }

    [Fact]
    public void CsvUsesLocalDatesAndNeutralizesFormulaInjection()
    {
        var events = new[]
        {
            new AuditTrailRow(
                1,
                SecurityAuditActions.AccessDenied,
                "security",
                "crm.pqrs.manage",
                "=1+1",
                7,
                "Ana",
                "10.0.0.1",
                LocalTime.ToUtc(Day).AddHours(9))
        };

        var csv = CreateService(events).Get(Day, Day).ToCsv();

        Assert.Contains("Fecha,Hora,Evento,Modulo,Usuario,IP,Detalle\r\n", csv);
        Assert.Contains("2026-11-10,09:00,AccessDenied,security,Ana,10.0.0.1,'=1+1", csv);
    }

    private static SecurityReportService CreateService(IReadOnlyList<AuditTrailRow> events)
    {
        var query = new Mock<ISecurityEventQuery>();
        query
            .Setup(source => source.InRange(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(events);

        return new SecurityReportService(query.Object, AllowAll);
    }

    private static AuditTrailRow Row(long id, string action, int? userId, DateTime date) =>
        new(id, action, "security", "test", $"SEED-{id}", userId, userId?.ToString(), "127.0.0.1", date);

    private static IAccessControl AllowAll
    {
        get
        {
            var access = new Mock<IAccessControl>();
            access
                .Setup(control => control.Demand(It.IsAny<string>()))
                .Callback((string _) => { });
            return access.Object;
        }
    }

    private static IAccessControl RestrictedTo(params string[] permissions)
    {
        var granted = new HashSet<string>(permissions, StringComparer.Ordinal);
        var access = new Mock<IAccessControl>();
        access
            .Setup(control => control.Demand(It.IsAny<string>()))
            .Callback((string permission) =>
            {
                if (!granted.Contains(permission))
                    throw new AccessDeniedException(permission);
            });
        return access.Object;
    }
}
