using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using TiendaFlow.Infrastructure.Persistence;

namespace TiendaFlow.IntegrationTests;

public class ParameterVersioningIntegrationTests : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly DateTime Now = new(2026, 3, 27, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly First = new(2026, 3, 1);
    private static readonly DateOnly Override = new(2026, 3, 2);
    private static readonly DateOnly Future = new(2026, 4, 15);

    private readonly SqlServerDatabaseFixture _database;

    public ParameterVersioningIntegrationTests(SqlServerDatabaseFixture database) =>
        _database = database;

    [SqlServerFact]
    public void Versioning_ResolvesTheLatestEffectiveValueAndKeepsTheHistory()
    {
        using var context = _database.CreateContext();
        var parameters = new EfSystemParameterRepository(context);
        var service = new ParameterService(
            parameters,
            new EfStoreRepository(context),
            new EfParameterAuthorQuery(context),
            new EfUnitOfWork(context),
            new AllowAllAccessControl(),
            new EfAuditLog(context),
            new FixedCurrentUser(),
            new FixedClock(Now));
        var values = new ParameterValues(parameters, new FixedClock(Now));

        Assert.Equal(15, values.GetInt(SystemParameterCatalog.PqrsResponseDays));
        Assert.Equal(1000m, values.GetDecimal(SystemParameterCatalog.CopPerLoyaltyPoint));

        var globalId = service.CreateVersion(new CreateParameterVersionRequest(
            SystemParameterCatalog.PqrsResponseDays,
            ParameterScope.Global,
            null,
            "20",
            First));
        var overrideId = service.CreateVersion(new CreateParameterVersionRequest(
            SystemParameterCatalog.PqrsResponseDays,
            ParameterScope.Store,
            1,
            "8",
            Override));
        var futureId = service.CreateVersion(new CreateParameterVersionRequest(
            SystemParameterCatalog.PqrsResponseDays,
            ParameterScope.Global,
            null,
            "30",
            Future));

        Assert.True(globalId > 0);
        Assert.True(overrideId > 0);
        Assert.True(futureId > 0);

        Assert.Equal(20, values.GetInt(SystemParameterCatalog.PqrsResponseDays));
        Assert.Equal(8, values.GetInt(SystemParameterCatalog.PqrsResponseDays, storeId: 1));
        Assert.Equal(20, values.GetInt(SystemParameterCatalog.PqrsResponseDays, storeId: 2));

        var rows = context.SystemParameters
            .AsNoTracking()
            .Where(parameter => parameter.Key == SystemParameterCatalog.PqrsResponseDays)
            .ToList();

        Assert.Equal(3, rows.Count);
        Assert.Contains(rows, row => row.Value == "20" && row.Version == 1);
        Assert.Contains(rows, row => row.Value == "8" && row.Scope == ParameterScope.Store);
        Assert.Contains(rows, row => row.Value == "30" && row.EffectiveFrom == Future);

        var history = service.History(
            SystemParameterCatalog.PqrsResponseDays,
            ParameterScope.Global,
            null);

        Assert.Equal(2, history.Versions.Count);
        Assert.Equal(2, history.Versions[0].Version);
        Assert.Equal(new DateOnly(2026, 4, 15), history.Versions[0].EffectiveFrom);
        Assert.Equal("30", history.Versions[0].Value);
        Assert.Equal(1, history.Versions[1].Version);
        Assert.Equal("integration1@tiendaflow.local", history.Versions[1].Author);

        var audit = context.AuditEntries
            .AsNoTracking()
            .Where(entry => entry.Action == "ParameterVersionCreated")
            .ToList();

        Assert.Equal(3, audit.Count);
        Assert.All(audit, entry => Assert.Equal("security", entry.Module));
        Assert.All(audit, entry => Assert.Equal(1, entry.UserId));
        Assert.All(audit, entry => Assert.Equal(SystemParameterCatalog.PqrsResponseDays, entry.EntityId));

        Assert.Throws<InvalidParameterException>(() =>
            service.CreateVersion(new CreateParameterVersionRequest(
                SystemParameterCatalog.PqrsResponseDays,
                ParameterScope.Global,
                null,
                "40",
                Future)));

        using var duplicateContext = _database.CreateContext();
        duplicateContext.SystemParameters.Add(new SystemParameter(
            SystemParameterCatalog.PqrsResponseDays,
            ParameterValueType.Integer,
            ParameterScope.Global,
            null,
            "99",
            Future,
            9,
            createdByUserId: 1,
            createdAt: Now));

        // The unique index includes global rows through the persisted
        // store_scope_key, so a duplicate effective date cannot slip in.
        Assert.Throws<DbUpdateException>(() => duplicateContext.SaveChanges());
    }

    [SqlServerFact]
    public void Versioning_RejectsAnEarlierEffectiveDateThanTheLatestVersion()
    {
        using var context = _database.CreateContext();
        var key = SystemParameterCatalog.ReplenishmentCoverageDays;
        var service = new ParameterService(
            new EfSystemParameterRepository(context),
            new EfStoreRepository(context),
            new EfParameterAuthorQuery(context),
            new EfUnitOfWork(context),
            new AllowAllAccessControl(),
            new EfAuditLog(context),
            new FixedCurrentUser(),
            new FixedClock(Now));

        service.CreateVersion(new CreateParameterVersionRequest(
            key,
            ParameterScope.Global,
            null,
            "15",
            new DateOnly(2026, 3, 10)));

        Assert.Throws<InvalidParameterException>(() =>
            service.CreateVersion(new CreateParameterVersionRequest(
                key,
                ParameterScope.Global,
                null,
                "20",
                new DateOnly(2026, 3, 1))));

        Assert.Single(
            context.SystemParameters
                .AsNoTracking()
                .Where(parameter => parameter.Key == key)
                .ToList());
    }

    private sealed class AllowAllAccessControl : IAccessControl
    {
        public bool Has(string permission) => true;

        public void Demand(string permission)
        {
        }
    }

    private sealed class FixedCurrentUser : ICurrentUser
    {
        public bool IsAuthenticated => true;

        public int? UserId => 1;

        public string Name => "Usuario de integración";

        public string Email => "integration1@tiendaflow.local";

        public string Role => RoleNames.Administrator;

        public string RoleLabel => SystemRoles.Label(Role);

        public int? StoreId => 1;

        public string StoreName => "Tienda Centro";

        public string? Ip => "127.0.0.1";

        public IReadOnlySet<string> Permissions { get; } =
            TiendaFlow.Application.Security.Permissions.All.ToHashSet(StringComparer.Ordinal);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow) => UtcNow = UtcTimestamp.Normalize(utcNow);

        public DateTime UtcNow { get; }
    }
}
