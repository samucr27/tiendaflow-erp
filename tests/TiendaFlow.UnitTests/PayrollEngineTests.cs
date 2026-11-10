using TiendaFlow.Application.Payroll;
using TiendaFlow.Domain;

namespace TiendaFlow.UnitTests;

public class PayrollEngineTests
{
    private static readonly PayrollParameters TestParameters = new()
    {
        MinimumWage = 1_000_000m,
        TransportSubsidy = 100_000m,
        Uvt = 50_000m
    };

    private static PayrollEngine CreateEngine(PayrollParameters? parameters = null) =>
        new(new DefaultPayrollParametersProvider(parameters ?? TestParameters),
            new IPayrollStrategy[]
            {
                new EmploymentPayrollStrategy(new WithholdingCalculator()),
                new ServiceProvisionPayrollStrategy()
            });

    [Fact]
    public void MinimumWageEmployee_GetsTransportSubsidyAndExpectedAmounts()
    {
        var r = CreateEngine().Calculate(new PayrollInput(1, ContractType.Indefinite, 1_000_000m));

        Assert.Equal(1_000_000m, r.EarnedSalary);
        Assert.Equal(100_000m, r.TransportSubsidy);
        Assert.Equal(1_100_000m, r.GrossEarnings);
        Assert.Equal(40_000m, r.EmployeeHealth);
        Assert.Equal(40_000m, r.EmployeePension);
        Assert.Equal(0m, r.SolidarityFund);
        Assert.Equal(0m, r.Withholding);
        Assert.Equal(1_020_000m, r.NetPay);

        Assert.Equal(0m, r.EmployerHealth);   // exonerated (< 10 minimum wages)
        Assert.Equal(0m, r.Sena);
        Assert.Equal(0m, r.Icbf);
        Assert.Equal(120_000m, r.EmployerPension);
        Assert.Equal(5_220m, r.Arl);
        Assert.Equal(40_000m, r.CompensationBox);
        Assert.Equal(165_220m, r.TotalEmployerContributions);

        Assert.Equal(91_630m, r.Severance);
        Assert.Equal(11_000m, r.SeveranceInterest);
        Assert.Equal(91_630m, r.ServiceBonus);
        Assert.Equal(41_700m, r.Vacations);
        Assert.Equal(1_501_180m, r.TotalEmployerCost);
    }

    [Fact]
    public void HighSalary_AppliesSolidarityFundWithholdingAndNoExoneration()
    {
        var r = CreateEngine().Calculate(new PayrollInput(2, ContractType.Indefinite, 10_000_000m));

        Assert.Equal(0m, r.TransportSubsidy);
        Assert.Equal(400_000m, r.EmployeeHealth);
        Assert.Equal(400_000m, r.EmployeePension);
        Assert.Equal(100_000m, r.SolidarityFund);
        Assert.Equal(394_250m, r.Withholding);
        Assert.Equal(8_705_750m, r.NetPay);

        // Exactly 10 minimum wages is NOT exonerated.
        Assert.Equal(850_000m, r.EmployerHealth);
        Assert.Equal(200_000m, r.Sena);
        Assert.Equal(300_000m, r.Icbf);
    }

    [Fact]
    public void PartialMonth_ProratesSalaryAndSubsidy()
    {
        var r = CreateEngine().Calculate(
            new PayrollInput(3, ContractType.FixedTerm, 1_000_000m, DaysWorked: 15));

        Assert.Equal(500_000m, r.EarnedSalary);
        Assert.Equal(50_000m, r.TransportSubsidy);
        Assert.Equal(550_000m, r.GrossEarnings);
        Assert.Equal(500_000m, r.ContributionBase);
        Assert.Equal(20_000m, r.EmployeeHealth);
    }

    [Fact]
    public void SalaryBelowMinimum_UsesMinimumContributionBase()
    {
        var r = CreateEngine().Calculate(new PayrollInput(4, ContractType.Indefinite, 800_000m));

        Assert.Equal(1_000_000m, r.ContributionBase);
        Assert.Equal(40_000m, r.EmployeeHealth);
        Assert.Equal(900_000m, r.GrossEarnings);
    }

    [Theory]
    [InlineData(ContractType.FixedTerm)]
    [InlineData(ContractType.WorkOrService)]
    public void LaborContractTypes_ShareTheSameCalculation(ContractType type)
    {
        var engine = CreateEngine();
        var baseline = engine.Calculate(new PayrollInput(5, ContractType.Indefinite, 2_500_000m));
        var other = engine.Calculate(new PayrollInput(5, type, 2_500_000m));

        Assert.Equal(baseline.NetPay, other.NetPay);
        Assert.Equal(baseline.TotalEmployerCost, other.TotalEmployerCost);
    }

    [Fact]
    public void ServiceProvision_OnlyWithholdsAndHasNoEmployerCostsBeyondFees()
    {
        var r = CreateEngine().Calculate(new PayrollInput(6, ContractType.ServiceProvision, 5_000_000m));

        Assert.Equal(5_000_000m, r.GrossEarnings);
        Assert.Equal(200_000m, r.Withholding);
        Assert.Equal(4_800_000m, r.NetPay);
        Assert.Equal(0m, r.TotalEmployerContributions);
        Assert.Equal(0m, r.TotalProvisions);
        Assert.Equal(5_000_000m, r.TotalEmployerCost);
    }

    [Fact]
    public void ServiceProvision_BelowMinimumBase_HasNoWithholding()
    {
        var r = CreateEngine().Calculate(new PayrollInput(7, ContractType.ServiceProvision, 150_000m));

        Assert.Equal(0m, r.Withholding);
        Assert.Equal(150_000m, r.NetPay);
    }

    [Theory]
    [InlineData(0, 30, 0)]
    [InlineData(-1, 30, 0)]
    [InlineData(1_000_000, 0, 0)]
    [InlineData(1_000_000, 31, 0)]
    [InlineData(1_000_000, 30, -1)]
    public void InvalidInput_IsRejected(int salary, int days, int otherEarnings)
    {
        var engine = CreateEngine();
        var input = new PayrollInput(8, ContractType.Indefinite, salary, days, otherEarnings);

        Assert.Throws<InvalidPayrollInputException>(() => engine.Calculate(input));
    }

    [Fact]
    public void UnknownRiskClass_IsRejected()
    {
        var input = new PayrollInput(9, ContractType.Indefinite, 1_000_000m, RiskClass: 9);

        Assert.Throws<InvalidPayrollInputException>(() => CreateEngine().Calculate(input));
    }

    [Fact]
    public void ContractTypeWithoutStrategy_IsRejected()
    {
        var engine = new PayrollEngine(
            new DefaultPayrollParametersProvider(TestParameters), Array.Empty<IPayrollStrategy>());

        Assert.Throws<UnsupportedContractTypeException>(() =>
            engine.Calculate(new PayrollInput(10, ContractType.Indefinite, 1_000_000m)));
    }

    [Fact]
    public void Batch_ReturnsOneResultPerEmployee()
    {
        var results = CreateEngine().CalculateBatch(new[]
        {
            new PayrollInput(1, ContractType.Indefinite, 1_000_000m),
            new PayrollInput(2, ContractType.ServiceProvision, 3_000_000m)
        });

        Assert.Equal(2, results.Count);
        Assert.Equal(new[] { 1, 2 }, results.Select(x => x.EmployeeId).ToArray());
    }
}