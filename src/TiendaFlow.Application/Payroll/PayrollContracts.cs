using TiendaFlow.Domain;

namespace TiendaFlow.Application.Payroll;

public interface IPayrollParametersProvider
{
    PayrollParameters GetCurrent();
}

public interface IPayrollStrategy
{
    bool Supports(ContractType contractType);
    PayrollResult Calculate(PayrollInput input, PayrollParameters parameters);
}

public interface IWithholdingCalculator
{
    decimal CalculateMonthlyEmployeeWithholding(
        decimal grossIncome, decimal mandatoryContributions, PayrollParameters parameters);
}

public interface IPayrollEngine
{
    PayrollResult Calculate(PayrollInput input);
    IReadOnlyList<PayrollResult> CalculateBatch(IEnumerable<PayrollInput> inputs);
}

/// <summary>Default provider with in-memory parameters. A database-backed provider can replace it later.</summary>
public sealed class DefaultPayrollParametersProvider : IPayrollParametersProvider
{
    private readonly PayrollParameters _parameters;

    public DefaultPayrollParametersProvider(PayrollParameters? parameters = null)
        => _parameters = parameters ?? new PayrollParameters();

    public PayrollParameters GetCurrent() => _parameters;
}

internal static class PayrollMath
{
    public static decimal Round(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);

    public static decimal Prorate(decimal monthlyAmount, int days) => Round(monthlyAmount * days / 30m);
}