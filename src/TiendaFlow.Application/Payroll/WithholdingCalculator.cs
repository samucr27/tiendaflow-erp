namespace TiendaFlow.Application.Payroll;

/// <summary>Single responsibility: monthly employee income-tax withholding (simplified table method).</summary>
public sealed class WithholdingCalculator : IWithholdingCalculator
{
    public decimal CalculateMonthlyEmployeeWithholding(
        decimal grossIncome, decimal mandatoryContributions, PayrollParameters parameters)
    {
        var netIncome = grossIncome - mandatoryContributions;
        if (netIncome <= 0m)
            return 0m;

        var monthlyExemptCap = parameters.ExemptIncomeAnnualCapUvt / 12m * parameters.Uvt;
        var exempt = Math.Min(netIncome * parameters.ExemptIncomeRate, monthlyExemptCap);
        var taxableUvt = (netIncome - exempt) / parameters.Uvt;

        foreach (var bracket in parameters.WithholdingBrackets)
        {
            if (taxableUvt > bracket.FromUvt && taxableUvt <= bracket.ToUvt)
            {
                var taxUvt = (taxableUvt - bracket.FromUvt) * bracket.Rate + bracket.AdditionalUvt;
                return PayrollMath.Round(taxUvt * parameters.Uvt);
            }
        }

        return 0m;
    }
}