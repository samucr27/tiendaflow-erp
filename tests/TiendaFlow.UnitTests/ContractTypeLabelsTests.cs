using TiendaFlow.Application.Payroll;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class ContractTypeLabelsTests
{
    [Fact]
    public void All_ContainsEveryContractType()
    {
        Assert.Equal(Enum.GetValues<ContractType>().Length, ContractTypeLabels.All.Count);
        Assert.Equal(
            Enum.GetValues<ContractType>().OrderBy(value => value),
            ContractTypeLabels.All.OrderBy(value => value));
    }

    [Theory]
    [InlineData(ContractType.Indefinite, "Indefinido")]
    [InlineData(ContractType.FixedTerm, "Término fijo")]
    [InlineData(ContractType.WorkOrService, "Obra o labor")]
    [InlineData(ContractType.ServiceProvision, "Prestación de servicios")]
    public void ToLabel_ReturnsTheSpanishLabel(ContractType contractType, string expected)
    {
        Assert.Equal(expected, ContractTypeLabels.ToLabel(contractType));
    }

    [Theory]
    [InlineData("Indefinido", ContractType.Indefinite)]
    [InlineData("  indefinido  ", ContractType.Indefinite)]
    [InlineData("Término fijo", ContractType.FixedTerm)]
    [InlineData("termino   FIJO", ContractType.FixedTerm)]
    [InlineData("Termino fijo", ContractType.FixedTerm)]
    [InlineData("Obra o labor", ContractType.WorkOrService)]
    [InlineData("Prestación de servicios", ContractType.ServiceProvision)]
    [InlineData("Prestacion de servicios", ContractType.ServiceProvision)]
    [InlineData("FixedTerm", ContractType.FixedTerm)]
    public void TryParse_AcceptsLabelsAliasesAndEnumNames(
        string value,
        ContractType expected)
    {
        Assert.True(ContractTypeLabels.TryParse(value, out var contractType));
        Assert.Equal(expected, contractType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Contrato raro")]
    public void TryParse_RejectsUnknownValues(string value)
    {
        Assert.False(ContractTypeLabels.TryParse(value, out _));
        Assert.False(ContractTypeLabels.IsKnown(value));
    }

    [Fact]
    public void ToLabel_FromStoredValue_NormalizesKnownValuesAndKeepsUnknownOnes()
    {
        Assert.Equal("Término fijo", ContractTypeLabels.ToLabel("Termino fijo"));
        Assert.Equal("Contrato raro", ContractTypeLabels.ToLabel("  Contrato raro  "));
    }

    [Fact]
    public void IsKnown_ReturnsTrueForStoredLabels()
    {
        Assert.True(ContractTypeLabels.IsKnown("Obra o labor"));
        Assert.False(ContractTypeLabels.IsKnown(null));
    }
}
