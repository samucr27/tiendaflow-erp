using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Application.Security;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// Real SQL Server coverage for the security report: the EF date-range query,
/// its batched user-name resolution, the exclusive upper bound and the index
/// added by the new migration. The report is isolated in a 2020 date range that
/// no other test uses, so shared-fixture activity cannot change the counts.
/// </summary>
public class SecurityReportIntegrationTests : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateOnly Day = new(2020, 1, 5);
    private static readonly DateTime MorningUtc = LocalTime.ToUtc(Day).AddHours(9);

    private readonly SqlServerDatabaseFixture _database;

    public SecurityReportIntegrationTests(SqlServerDatabaseFixture database) => _database = database;

    [SqlServerFact]
    public async Task ReportReadsRealAuditRowsAndClassifiesKpis()
    {
        await using var context = _database.CreateContext();
        var tag = Guid.NewGuid().ToString("N");
        var detail = (string action) => $"SEED-{tag}-{action}";

        context.AuditEntries.AddRange(
            Entry(SecurityAuditActions.LoginSucceeded, detail("ok"), 1, MorningUtc),
            Entry(SecurityAuditActions.LoginFailed, detail("fail"), null, MorningUtc.AddHours(1)),
            Entry(SecurityAuditActions.AccessDenied, detail("denied"), 2, MorningUtc.AddHours(2)),
            Entry("UserCreated", detail("created"), 1, MorningUtc.AddHours(3)),
            // Outside the range: the report's upper bound is exclusive.
            Entry(SecurityAuditActions.LoginSucceeded, detail("outside"), 1, LocalTime.ToUtc(Day.AddDays(1))));
        await context.SaveChangesAsync();

        var query = new EfSecurityEventQuery(context);
        var service = new SecurityReportService(query, new AllowAllAccessControl());
        var report = service.Get(Day, Day);

        var ids = report.Events.Select(entry => entry.Detail).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(detail("ok"), ids);
        Assert.Contains(detail("fail"), ids);
        Assert.Contains(detail("denied"), ids);
        Assert.Contains(detail("created"), ids);
        Assert.DoesNotContain(detail("outside"), ids);

        Assert.Equal(1, report.SuccessfulLogins);
        Assert.Equal(1, report.FailedLogins);
        Assert.Equal(1, report.AccessDenials);
        Assert.Equal(1, report.UserChanges);
        Assert.Equal(4, report.TotalEvents);
        Assert.Equal(2, report.DistinctActors);

        var daily = Assert.Single(report.Daily);
        Assert.Equal(Day, daily.Date);
        Assert.Equal(4, daily.TotalEvents);

        // The resolver batched two lookups and showed the seeded user email
        // because those demo users have no employee record.
        var succeeded = report.Events.Single(entry => entry.Detail == detail("ok"));
        Assert.Equal("integration1@tiendaflow.local", succeeded.UserName);
    }

    [SqlServerFact]
    public async Task SecurityReportIndexIsPresentAfterMigration()
    {
        await using var context = _database.CreateContext();

        var exists = await context.Database.SqlQueryRaw<int>(
                """
                SELECT COUNT(*) AS Value
                FROM sys.indexes
                WHERE name = 'IX_audit_log_logged_at'
                  AND object_id = OBJECT_ID('audit_log')
                """)
            .SingleAsync();

        Assert.Equal(1, exists);
    }

    private static AuditEntry Entry(string action, string detail, int? userId, DateTime date) =>
        new(action, "integration", detail, userId, date, module: "security", ip: "127.0.0.1");
}
