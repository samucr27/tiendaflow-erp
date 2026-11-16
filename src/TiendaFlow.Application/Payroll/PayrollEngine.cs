namespace TiendaFlow.Application.Payroll;

/// <summary>Orchestrates the calculation: validates the input, picks the strategy for the contract type
/// and delegates. Depends only on abstractions.</summary>
public sealed class PayrollEngine : IPayrollEngine
{
    private readonly IPayrollParametersProvider _parameters;
    private readonly IReadOnlyList<IPayrollStrategy> _strategies;

    public PayrollEngine(IPayrollParametersProvider parameters, IEnumerable<IPayrollStrategy> strategies)
    {
        _parameters = parameters;
        _strategies = strategies.ToList();
    }

    public PayrollResult Calculate(PayrollInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Validate(input);

        var strategy = _strategies.FirstOrDefault(s => s.Supports(input.ContractType))
            ?? throw new UnsupportedContractTypeException(input.ContractType);

        return strategy.Calculate(input, _parameters.GetCurrent());
    }

    public IReadOnlyList<PayrollResult> CalculateBatch(IEnumerable<PayrollInput> inputs) =>
        inputs.Select(Calculate).ToList();

    private static void Validate(PayrollInput input)
    {
        if (input.BaseSalary <= 0m)
            throw new InvalidPayrollInputException("The base salary must be greater than zero.");
        if (input.DaysWorked is < 1 or > 30)
            throw new InvalidPayrollInputException("Days worked must be between 1 and 30.");
        if (input.OtherSalaryEarnings < 0m)
            throw new InvalidPayrollInputException("Other earnings cannot be negative.");
    }
}