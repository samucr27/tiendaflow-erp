using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class SystemParameterTests
{
    private static readonly DateOnly Today = new(2026, 3, 27);
    private static readonly DateTime CreatedAt = new(2026, 3, 27, 14, 0, 0, DateTimeKind.Utc);

    private static SystemParameter Create(
        string key = "crm.points.cop_per_point",
        ParameterValueType type = ParameterValueType.Decimal,
        string value = "1000",
        ParameterScope scope = ParameterScope.Global,
        int? storeId = null,
        DateOnly? effectiveFrom = null,
        int version = 1) =>
        new(
            key,
            type,
            scope,
            storeId,
            value,
            effectiveFrom ?? Today,
            version,
            createdByUserId: 1,
            createdAt: CreatedAt);

    [Theory]
    [InlineData(ParameterValueType.Integer, "  12 ", "12")]
    [InlineData(ParameterValueType.Decimal, "1.500,25", "1500.25")]
    [InlineData(ParameterValueType.Decimal, "12.5", "12.5")]
    [InlineData(ParameterValueType.Boolean, "Sí", "true")]
    [InlineData(ParameterValueType.Boolean, "0", "false")]
    [InlineData(ParameterValueType.Boolean, " TRUE ", "true")]
    [InlineData(ParameterValueType.Text, "  valor libre  ", "valor libre")]
    public void NormalizeValue_StoresACanonicalForm(
        ParameterValueType type,
        string input,
        string expected)
    {
        Assert.Equal(expected, SystemParameter.NormalizeValue(type, input));
    }

    [Theory]
    [InlineData(ParameterValueType.Integer, "1.5")]
    [InlineData(ParameterValueType.Integer, "abc")]
    [InlineData(ParameterValueType.Decimal, "no es número")]
    [InlineData(ParameterValueType.Boolean, "quizá")]
    [InlineData(ParameterValueType.Text, "   ")]
    public void NormalizeValue_RejectsInvalidValues(ParameterValueType type, string input)
    {
        Assert.Throws<InvalidParameterException>(
            () => SystemParameter.NormalizeValue(type, input));
    }

    [Fact]
    public void NormalizeValue_RejectsTextOverFiveHundredCharacters()
    {
        var text = new string('a', 501);

        Assert.Throws<InvalidParameterException>(
            () => SystemParameter.NormalizeValue(ParameterValueType.Text, text));
    }

    [Fact]
    public void Constructor_KeepsTheVersionMetadataAndNormalizesTheTimestamp()
    {
        var local = new DateTime(2026, 3, 27, 9, 0, 0, DateTimeKind.Local);
        var parameter = new SystemParameter(
            "crm.points.cop_per_point",
            ParameterValueType.Decimal,
            ParameterScope.Global,
            null,
            "1000",
            Today,
            3,
            createdByUserId: 7,
            createdAt: local);

        Assert.Equal("crm.points.cop_per_point", parameter.Key);
        Assert.Equal("1000", parameter.Value);
        Assert.Equal(Today, parameter.EffectiveFrom);
        Assert.Equal(3, parameter.Version);
        Assert.Equal(7, parameter.CreatedByUserId);
        Assert.Equal(DateTimeKind.Utc, parameter.CreatedAt.Kind);
        Assert.Equal(local.ToUniversalTime(), parameter.CreatedAt);
    }

    [Fact]
    public void Constructor_TrimsTheKey()
    {
        var parameter = Create(key: "  inventory.replenishment.coverage_days  ");

        Assert.Equal("inventory.replenishment.coverage_days", parameter.Key);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Clave.Mayuscula")]
    [InlineData("clave con espacio")]
    [InlineData("clave/con/slash")]
    public void Constructor_RejectsInvalidKeys(string key)
    {
        Assert.Throws<InvalidParameterException>(() => Create(key: key));
    }

    [Fact]
    public void Constructor_RejectsAGlobalParameterWithStore()
    {
        Assert.Throws<InvalidParameterException>(
            () => Create(scope: ParameterScope.Global, storeId: 1));
    }

    [Fact]
    public void Constructor_RejectsAStoreParameterWithoutStore()
    {
        Assert.Throws<InvalidParameterException>(
            () => Create(scope: ParameterScope.Store, storeId: null));
    }

    [Fact]
    public void Constructor_AcceptsAStoreParameterWithAValidStore()
    {
        var parameter = Create(scope: ParameterScope.Store, storeId: 4);

        Assert.Equal(ParameterScope.Store, parameter.Scope);
        Assert.Equal(4, parameter.StoreId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveVersions(int version)
    {
        Assert.Throws<InvalidParameterException>(() => Create(version: version));
    }

    [Fact]
    public void Constructor_RejectsADefaultEffectiveDate()
    {
        Assert.Throws<InvalidParameterException>(
            () => Create(effectiveFrom: default(DateOnly)));
    }

    [Fact]
    public void Constructor_RejectsADefaultCreationTimestamp()
    {
        Assert.Throws<InvalidParameterException>(() =>
            new SystemParameter(
                "crm.points.cop_per_point",
                ParameterValueType.Decimal,
                ParameterScope.Global,
                null,
                "1000",
                Today,
                1,
                null,
                default));
    }

    [Fact]
    public void Constructor_RejectsAnUndefinedValueType()
    {
        Assert.Throws<InvalidParameterException>(
            () => Create(type: (ParameterValueType)99));
    }

    [Fact]
    public void Constructor_RejectsAnUndefinedScope()
    {
        Assert.Throws<InvalidParameterException>(
            () => Create(scope: (ParameterScope)99));
    }
}
