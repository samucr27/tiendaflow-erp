using TiendaFlow.Application;
using TiendaFlow.Application.Parameters;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class ParameterValuesTests
{
    private static readonly DateTime Now = new(2026, 3, 27, 15, 0, 0, DateTimeKind.Utc);

    private readonly InMemorySystemParameterRepository _parameters = new();
    private readonly FixedClock _clock = new(Now);

    private ParameterValues CreateProvider() => new(_parameters, _clock);

    private SystemParameter Store(
        string key,
        string value,
        DateOnly effectiveFrom,
        ParameterScope scope = ParameterScope.Global,
        int? storeId = null,
        int version = 1) =>
        new(
            key,
            SystemParameterCatalog.Find(key)!.ValueType,
            scope,
            storeId,
            value,
            effectiveFrom,
            version,
            createdByUserId: 1,
            createdAt: Now);

    [Fact]
    public void ReadsTheCodeDefaultWhenThereIsNoStoredVersion()
    {
        var provider = CreateProvider();

        Assert.Equal(15, provider.GetInt(SystemParameterCatalog.PqrsResponseDays));
        Assert.Equal(1000m, provider.GetDecimal(SystemParameterCatalog.CopPerLoyaltyPoint));
    }

    [Fact]
    public void ReadsTheStoredGlobalValue()
    {
        _parameters.Add(Store(
            SystemParameterCatalog.PqrsResponseDays,
            "20",
            new DateOnly(2026, 3, 1)));

        Assert.Equal(20, CreateProvider().GetInt(SystemParameterCatalog.PqrsResponseDays));
    }

    [Fact]
    public void AGlobalStoreOverrideWinsOverTheGlobalValue()
    {
        _parameters.Add(Store(
            SystemParameterCatalog.PqrsResponseDays,
            "20",
            new DateOnly(2026, 3, 1)));
        _parameters.Add(Store(
            SystemParameterCatalog.PqrsResponseDays,
            "8",
            new DateOnly(2026, 3, 2),
            ParameterScope.Store,
            storeId: 1));

        var provider = CreateProvider();

        Assert.Equal(8, provider.GetInt(SystemParameterCatalog.PqrsResponseDays, storeId: 1));
        Assert.Equal(20, provider.GetInt(SystemParameterCatalog.PqrsResponseDays, storeId: 2));
        Assert.Equal(20, provider.GetInt(SystemParameterCatalog.PqrsResponseDays));
    }

    [Fact]
    public void AVersionsEffectiveDateIsRespected()
    {
        _parameters.Add(Store(
            SystemParameterCatalog.PqrsResponseDays,
            "30",
            new DateOnly(2026, 4, 1)));

        var provider = CreateProvider();

        Assert.Equal(15, provider.GetInt(SystemParameterCatalog.PqrsResponseDays));
    }

    [Fact]
    public void TheLatestEffectiveVersionWins()
    {
        _parameters.Add(Store(
            SystemParameterCatalog.PqrsResponseDays,
            "10",
            new DateOnly(2026, 3, 1)));
        _parameters.Add(Store(
            SystemParameterCatalog.PqrsResponseDays,
            "25",
            new DateOnly(2026, 3, 20),
            version: 2));

        Assert.Equal(25, CreateProvider().GetInt(SystemParameterCatalog.PqrsResponseDays));
    }

    [Fact]
    public void GetText_ReturnsTheCanonicalStoredValue()
    {
        _parameters.Add(Store(
            SystemParameterCatalog.PqrsResponseDays,
            "20",
            new DateOnly(2026, 3, 1)));

        Assert.Equal("20", CreateProvider().GetText(SystemParameterCatalog.PqrsResponseDays));
    }

    [Fact]
    public void AnUnknownKeyIsReported()
    {
        Assert.Throws<ParameterNotFoundException>(() =>
            CreateProvider().GetInt("modulo.clave.inexistente"));
    }
}
