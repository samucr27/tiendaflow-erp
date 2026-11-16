using TiendaFlow.Application;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Application.Security;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class ParameterServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 27, 15, 0, 0, DateTimeKind.Utc);

    private readonly InMemorySystemParameterRepository _parameters = new();
    private readonly InMemoryStoreRepository _stores = new(
        new Store(1, "Tienda Centro", "Calle 10 # 20-30"),
        new Store(2, "Tienda Norte", "Carrera 15 # 80-20"),
        new Store(3, "Tienda Cerrada", "Calle 1 # 2-3", active: false));
    private readonly FakeParameterAuthorQuery _authors = new();
    private readonly RecordingAuditLog _audit = new();
    private readonly FixedClock _clock = new(Now);

    private ParameterService CreateService(
        IAccessControl? accessControl = null,
        ICurrentUser? currentUser = null) =>
        new(
            _parameters,
            _stores,
            _authors,
            NoOpUnitOfWork.Instance,
            accessControl ?? new AlwaysAllowAccessControl(),
            _audit,
            currentUser ?? new FakeCurrentUser(),
            _clock);

    private static CreateParameterVersionRequest Request(
        string key = SystemParameterCatalog.CopPerLoyaltyPoint,
        ParameterScope scope = ParameterScope.Global,
        int? storeId = null,
        string value = "1200",
        DateOnly? effectiveFrom = null) =>
        new(key, scope, storeId, value, effectiveFrom ?? new DateOnly(2026, 3, 1));

    [Fact]
    public void List_ReturnsTheWholeCatalogWithCodeDefaultsWhenNothingIsStored()
    {
        var rows = CreateService().List(null);

        Assert.Equal(SystemParameterCatalog.All.Count, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal("Valor de código", row.SourceLabel);
            Assert.Equal(row.DefaultValue, row.Value);
            Assert.Null(row.EffectiveFrom);
            Assert.Equal(0, row.VersionCount);
        });
    }

    [Fact]
    public void List_UsesTheLatestStoredGlobalValue()
    {
        var service = CreateService();

        service.CreateVersion(Request(value: "1000", effectiveFrom: new DateOnly(2026, 3, 1)));
        service.CreateVersion(Request(value: "1300", effectiveFrom: new DateOnly(2026, 3, 20)));

        var row = Assert.Single(
            service.List(null),
            candidate => candidate.Key == SystemParameterCatalog.CopPerLoyaltyPoint);

        Assert.Equal("1300", row.Value);
        Assert.Equal("Global", row.SourceLabel);
        Assert.Equal(new DateOnly(2026, 3, 20), row.EffectiveFrom);
        Assert.Equal(2, row.VersionCount);
    }

    [Fact]
    public void List_AppliesTheStoreOverrideOnlyForThatStore()
    {
        var service = CreateService();

        service.CreateVersion(Request(value: "1000", effectiveFrom: new DateOnly(2026, 3, 1)));
        service.CreateVersion(Request(
            scope: ParameterScope.Store,
            storeId: 1,
            value: "700",
            effectiveFrom: new DateOnly(2026, 3, 2)));

        var storeOne = service.List(1).Single(row => row.Key == SystemParameterCatalog.CopPerLoyaltyPoint);
        var storeTwo = service.List(2).Single(row => row.Key == SystemParameterCatalog.CopPerLoyaltyPoint);
        var global = service.List(null).Single(row => row.Key == SystemParameterCatalog.CopPerLoyaltyPoint);

        Assert.Equal("700", storeOne.Value);
        Assert.Equal("Tienda", storeOne.SourceLabel);
        Assert.Equal(1, storeOne.VersionCount);
        Assert.Equal("1000", storeTwo.Value);
        Assert.Equal("Global", storeTwo.SourceLabel);
        Assert.Equal("1000", global.Value);
        Assert.Equal(1, global.VersionCount);
    }

    [Fact]
    public void CreateVersion_AppendsANewVersionAndAuditsIt()
    {
        var id = CreateService().CreateVersion(Request(value: "1500"));

        Assert.True(id > 0);

        var stored = Assert.Single(_parameters.List());
        Assert.Equal("1500", stored.Value);
        Assert.Equal(1, stored.Version);
        Assert.Equal(1, stored.CreatedByUserId);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal("ParameterVersionCreated", entry.Action);
        Assert.Equal("security", entry.Module);
        Assert.Equal(SystemParameterCatalog.CopPerLoyaltyPoint, entry.EntityId);
        Assert.Equal(1, entry.UserId);
        Assert.Equal("127.0.0.1", entry.Ip);
        Assert.Contains("'1000' -> '1500'", entry.Detail);
        Assert.Contains("v1", entry.Detail);
    }

    [Fact]
    public void CreateVersion_IncrementsTheVersionAndPreservesThePreviousRow()
    {
        var service = CreateService();

        service.CreateVersion(Request(value: "1000", effectiveFrom: new DateOnly(2026, 3, 1)));
        service.CreateVersion(Request(value: "1500", effectiveFrom: new DateOnly(2026, 3, 10)));

        var versions = _parameters.List()
            .Where(parameter => parameter.Key == SystemParameterCatalog.CopPerLoyaltyPoint)
            .OrderBy(parameter => parameter.Version)
            .ToList();

        Assert.Equal(2, versions.Count);
        Assert.Equal("1000", versions[0].Value);
        Assert.Equal(1, versions[0].Version);
        Assert.Equal("1500", versions[1].Value);
        Assert.Equal(2, versions[1].Version);
        Assert.Equal(2, _audit.Entries.Count);
    }

    [Fact]
    public void CreateVersion_RejectsADuplicateEffectiveDate()
    {
        var service = CreateService();
        service.CreateVersion(Request());

        Assert.Throws<InvalidParameterException>(() =>
            service.CreateVersion(Request(value: "2000")));

        Assert.Single(_parameters.List());
        Assert.Single(_audit.Entries);
    }

    [Fact]
    public void CreateVersion_RejectsAnEarlierDateThanTheLatestVersion()
    {
        var service = CreateService();
        service.CreateVersion(Request(effectiveFrom: new DateOnly(2026, 3, 10)));

        Assert.Throws<InvalidParameterException>(() =>
            service.CreateVersion(Request(value: "2000", effectiveFrom: new DateOnly(2026, 3, 1))));

        Assert.Single(_parameters.List());
    }

    [Fact]
    public void CreateVersion_RejectsAnUnknownKey()
    {
        Assert.Throws<InvalidParameterException>(() =>
            CreateService().CreateVersion(Request(key: "modulo.clave.inexistente")));

        Assert.Empty(_parameters.List());
        Assert.Empty(_audit.Entries);
    }

    [Theory]
    [InlineData("crm.points.cop_per_point", "0")]
    [InlineData("crm.points.cop_per_point", "1000001")]
    [InlineData("crm.pqrs.response_days", "0")]
    [InlineData("crm.pqrs.response_days", "91")]
    public void CreateVersion_EnforcesTheCatalogRange(string key, string value)
    {
        Assert.Throws<InvalidParameterException>(() =>
            CreateService().CreateVersion(Request(key: key, value: value)));
    }

    [Fact]
    public void CreateVersion_StoresTheCanonicalValue()
    {
        CreateService().CreateVersion(Request(value: "1.500,50"));

        Assert.Equal("1500.50", Assert.Single(_parameters.List()).Value);
    }

    [Fact]
    public void CreateVersion_RejectsAStoreOverrideForAnUnknownStore()
    {
        Assert.Throws<StoreNotFoundException>(() =>
            CreateService().CreateVersion(Request(
                scope: ParameterScope.Store,
                storeId: 99,
                value: "900")));
    }

    [Fact]
    public void CreateVersion_RejectsAStoreOverrideForAnInactiveStore()
    {
        Assert.Throws<InvalidParameterException>(() =>
            CreateService().CreateVersion(Request(
                scope: ParameterScope.Store,
                storeId: 3,
                value: "900")));
    }

    [Fact]
    public void CreateVersion_RequiresAStoreForAStoreScopedParameter()
    {
        Assert.Throws<InvalidParameterException>(() =>
            CreateService().CreateVersion(Request(scope: ParameterScope.Store, storeId: null)));
    }

    [Fact]
    public void List_RejectsAnUnknownStore()
    {
        Assert.Throws<StoreNotFoundException>(() => CreateService().List(99));
    }

    [Fact]
    public void History_ReturnsVersionsNewestFirstWithAuthorNames()
    {
        var service = CreateService();
        _authors.Authors[1] = "Ana Administradora";

        service.CreateVersion(Request(value: "1000", effectiveFrom: new DateOnly(2026, 3, 1)));
        service.CreateVersion(Request(value: "1200", effectiveFrom: new DateOnly(2026, 3, 15)));

        var history = service.History(
            SystemParameterCatalog.CopPerLoyaltyPoint,
            ParameterScope.Global,
            null);

        Assert.Equal(2, history.Versions.Count);
        Assert.Equal(2, history.Versions[0].Version);
        Assert.Equal("1200", history.Versions[0].Value);
        Assert.Equal(new DateOnly(2026, 3, 15), history.Versions[0].EffectiveFrom);
        Assert.Equal("Ana Administradora", history.Versions[0].Author);
        Assert.Equal("Todas las tiendas", history.Versions[0].StoreName);
        Assert.Equal(1, history.Versions[1].Version);
    }

    [Fact]
    public void History_UsesSistemaWhenTheAuthorIsUnknown()
    {
        var service = CreateService();
        service.CreateVersion(Request());

        var history = service.History(
            SystemParameterCatalog.CopPerLoyaltyPoint,
            ParameterScope.Global,
            null);

        Assert.Equal("Sistema", Assert.Single(history.Versions).Author);
    }

    [Fact]
    public void History_ShowsTheStoreNameForAnOverride()
    {
        var service = CreateService();
        service.CreateVersion(Request(
            scope: ParameterScope.Store,
            storeId: 2,
            value: "900"));

        var history = service.History(
            SystemParameterCatalog.CopPerLoyaltyPoint,
            ParameterScope.Store,
            2);

        var version = Assert.Single(history.Versions);
        Assert.Equal("Tienda", version.ScopeLabel);
        Assert.Equal("Tienda Norte", version.StoreName);
    }

    [Fact]
    public void Stores_ListsTheBranchesAlphabetically()
    {
        var stores = CreateService().Stores();

        Assert.Equal(new[] { "Tienda Centro", "Tienda Cerrada", "Tienda Norte" },
            stores.Select(store => store.Name));
        Assert.False(stores.Single(store => store.Id == 3).Active);
    }

    [Fact]
    public void List_WithoutPermission_IsRejectedBeforeTouchingTheRepository()
    {
        Assert.Throws<AccessDeniedException>(() =>
            CreateService(new AlwaysDenyAccessControl()).List(null));

        Assert.Equal(0, _parameters.ListCalls);
    }

    [Fact]
    public void CreateVersion_WithoutPermission_IsRejectedBeforeTouchingTheRepository()
    {
        Assert.Throws<AccessDeniedException>(() =>
            CreateService(new AlwaysDenyAccessControl()).CreateVersion(Request()));

        Assert.Equal(0, _parameters.AddCalls);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public void History_WithoutPermission_IsRejectedBeforeTouchingTheRepository()
    {
        Assert.Throws<AccessDeniedException>(() =>
            CreateService(new AlwaysDenyAccessControl()).History(
                SystemParameterCatalog.CopPerLoyaltyPoint,
                ParameterScope.Global,
                null));

        Assert.Equal(0, _parameters.ListForKeyCalls);
        Assert.Equal(0, _authors.Calls);
    }

    [Fact]
    public void History_RejectsAnUnknownKey()
    {
        Assert.Throws<InvalidParameterException>(() =>
            CreateService().History("modulo.clave.inexistente", ParameterScope.Global, null));
    }

    [Fact]
    public void History_RejectsAnUnknownStore()
    {
        Assert.Throws<StoreNotFoundException>(() =>
            CreateService().History(
                SystemParameterCatalog.CopPerLoyaltyPoint,
                ParameterScope.Store,
                99));
    }
}
