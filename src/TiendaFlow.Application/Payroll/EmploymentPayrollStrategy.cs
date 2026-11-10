using TiendaFlow.Domain;

namespace TiendaFlow.Application.Payroll;

/// <summary>Payroll for labor contracts (indefinite, fixed term, work or service).</summary>
public sealed class EmploymentPayrollStrategy : IPayrollStrategy
{
    private readonly IWithholdingCalculator _withholding;

    public EmploymentPayrollStrategy(IWithholdingCalculator withholding) => _withholding = withholding;

    public bool Supports(ContractType contractType) =>
        contractType is ContractType.Indefinite or ContractType.FixedTerm or ContractType.WorkOrService;

    public PayrollResult Calculate(PayrollInput input, PayrollParameters p)
    {
        if (!p.ArlRates.TryGetValue(input.RiskClass, out var arlRate))
            throw new InvalidPayrollInputException($"Risk class {input.RiskClass} is not configured.");

        var earnedSalary = PayrollMath.Prorate(input.BaseSalary, input.DaysWorked);
        var salaryIncome = earnedSalary + input.OtherSalaryEarnings;

        var transport = input.BaseSalary <= p.MinimumWage * p.TransportSubsidyLimitInMinimumWages
            ? PayrollMath.Prorate(p.TransportSubsidy, input.DaysWorked)
            : 0m;
        var gross = salaryIncome + transport;

        var minimumBase = PayrollMath.Prorate(p.MinimumWage, input.DaysWorked);
        var maximumBase = p.MinimumWage * p.MaxContributionBaseInMinimumWages;
        var contributionBase = Math.Min(Math.Max(salaryIncome, minimumBase), maximumBase);

        var health = PayrollMath.Round(contributionBase * p.EmployeeHealthRate);
        var pension = PayrollMath.Round(contributionBase * p.EmployeePensionRate);
        var solidarity = contributionBase >= p.MinimumWage * p.SolidarityFundThresholdInMinimumWages
            ? PayrollMath.Round(contributionBase * p.SolidarityFundRate)
            : 0m;
        var withholding = _withholding.CalculateMonthlyEmployeeWithholding(
            gross, health + pension + solidarity, p);

        var exonerated = p.ApplyExoneration
            && salaryIncome < p.ExonerationLimitInMinimumWages * p.MinimumWage;

        var severanceBase = salaryIncome + transport;

        return new PayrollResult
        {
            EmployeeId = input.EmployeeId,
            ContractType = input.ContractType,
            DaysWorked = input.DaysWorked,
            EarnedSalary = earnedSalary,
            OtherSalaryEarnings = input.OtherSalaryEarnings,
            TransportSubsidy = transport,
            GrossEarnings = gross,
            ContributionBase = contributionBase,
            EmployeeHealth = health,
            EmployeePension = pension,
            SolidarityFund = solidarity,
            Withholding = withholding,
            EmployerHealth = exonerated ? 0m : PayrollMath.Round(contributionBase * p.EmployerHealthRate),
            EmployerPension = PayrollMath.Round(contributionBase * p.EmployerPensionRate),
            Arl = PayrollMath.Round(contributionBase * arlRate),
            CompensationBox = PayrollMath.Round(salaryIncome * p.CompensationBoxRate),
            Sena = exonerated ? 0m : PayrollMath.Round(salaryIncome * p.SenaRate),
            Icbf = exonerated ? 0m : PayrollMath.Round(salaryIncome * p.IcbfRate),
            Severance = PayrollMath.Round(severanceBase * p.SeveranceRate),
            SeveranceInterest = PayrollMath.Round(severanceBase * p.SeveranceInterestRate),
            ServiceBonus = PayrollMath.Round(severanceBase * p.ServiceBonusRate),
            Vacations = PayrollMath.Round(salaryIncome * p.VacationRate)
        };
    }
}