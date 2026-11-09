using TiendaFlow.Domain;

namespace TiendaFlow.Application.Payroll;

/// <summary>Service provision contracts: no employer contributions or provisions; only withholding.
/// The contractor pays their own social security (not deducted here).</summary>
public sealed class ServiceProvisionPayrollStrategy : IPayrollStrategy
{
    public bool Supports(ContractType contractType) => contractType == ContractType.ServiceProvision;

    public PayrollResult Calculate(PayrollInput input, PayrollParameters p)
    {
        var fees = PayrollMath.Prorate(input.BaseSalary, input.DaysWorked);
        var gross = fees + input.OtherSalaryEarnings;

        var withholding = gross >= p.ServiceProvisionWithholdingMinimumUvt * p.Uvt
            ? PayrollMath.Round(gross * p.ServiceProvisionWithholdingRate)
            : 0m;

        return new PayrollResult
        {
            EmployeeId = input.EmployeeId,
            ContractType = input.ContractType,
            DaysWorked = input.DaysWorked,
            EarnedSalary = fees,
            OtherSalaryEarnings = input.OtherSalaryEarnings,
            GrossEarnings = gross,
            Withholding = withholding
        };
    }
}