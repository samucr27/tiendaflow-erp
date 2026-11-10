namespace TiendaFlow.Application.Payroll;

/// <summary>One row of the withholding table: tax = (baseUvt - FromUvt) * Rate + AdditionalUvt.</summary>
public sealed record WithholdingBracket(decimal FromUvt, decimal ToUvt, decimal Rate, decimal AdditionalUvt);

/// <summary>Configurable payroll parameters. Defaults are ASSUMPTIONS and must be checked against
/// the current Colombian regulations before any real use.</summary>
public sealed record PayrollParameters
{
    private static readonly Dictionary<int, decimal> DefaultArlRates = new()
    {
        [1] = 0.00522m, [2] = 0.01044m, [3] = 0.02436m, [4] = 0.04350m, [5] = 0.06960m
    };

    private static readonly WithholdingBracket[] DefaultBrackets =
    {
        new(0m, 95m, 0m, 0m),
        new(95m, 150m, 0.19m, 0m),
        new(150m, 360m, 0.28m, 10m),
        new(360m, 640m, 0.33m, 69m),
        new(640m, 945m, 0.35m, 162m),
        new(945m, 2300m, 0.37m, 268m),
        new(2300m, decimal.MaxValue, 0.39m, 770m)
    };

    // Reference values
    public decimal MinimumWage { get; init; } = 1_750_905m;
    public decimal TransportSubsidy { get; init; } = 249_095m;
    public decimal TransportSubsidyLimitInMinimumWages { get; init; } = 2m;
    public decimal Uvt { get; init; } = 52_374m;
    public decimal MaxContributionBaseInMinimumWages { get; init; } = 25m;

    // Employee deductions
    public decimal EmployeeHealthRate { get; init; } = 0.04m;
    public decimal EmployeePensionRate { get; init; } = 0.04m;
    public decimal SolidarityFundThresholdInMinimumWages { get; init; } = 4m;
    public decimal SolidarityFundRate { get; init; } = 0.01m;

    // Employer contributions
    public decimal EmployerHealthRate { get; init; } = 0.085m;
    public decimal EmployerPensionRate { get; init; } = 0.12m;
    public IReadOnlyDictionary<int, decimal> ArlRates { get; init; } = DefaultArlRates;
    public decimal CompensationBoxRate { get; init; } = 0.04m;
    public decimal SenaRate { get; init; } = 0.02m;
    public decimal IcbfRate { get; init; } = 0.03m;
    public bool ApplyExoneration { get; init; } = true;
    public decimal ExonerationLimitInMinimumWages { get; init; } = 10m;

    // Provisions (monthly, as a fraction of the base)
    public decimal SeveranceRate { get; init; } = 0.0833m;
    public decimal SeveranceInterestRate { get; init; } = 0.01m;
    public decimal ServiceBonusRate { get; init; } = 0.0833m;
    public decimal VacationRate { get; init; } = 0.0417m;

    // Withholding (simplified)
    public decimal ExemptIncomeRate { get; init; } = 0.25m;
    public decimal ExemptIncomeAnnualCapUvt { get; init; } = 790m;
    public IReadOnlyList<WithholdingBracket> WithholdingBrackets { get; init; } = DefaultBrackets;

    // Service provision contracts
    public decimal ServiceProvisionWithholdingRate { get; init; } = 0.04m;
    public decimal ServiceProvisionWithholdingMinimumUvt { get; init; } = 4m;
}