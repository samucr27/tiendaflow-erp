using TiendaFlow.Domain;

namespace TiendaFlow.Application.Payroll;

/// <summary>Input of a monthly payroll calculation for one person.
/// For ServiceProvision, BaseSalary is the monthly fee.</summary>
public sealed record PayrollInput(
    int EmployeeId,
    ContractType ContractType,
    decimal BaseSalary,
    int DaysWorked = 30,
    decimal OtherSalaryEarnings = 0m,
    int RiskClass = 1);

public sealed record PayrollResult
{
    public int EmployeeId { get; init; }
    public ContractType ContractType { get; init; }
    public int DaysWorked { get; init; }

    // Earnings (devengado)
    public decimal EarnedSalary { get; init; }
    public decimal OtherSalaryEarnings { get; init; }
    public decimal TransportSubsidy { get; init; }
    public decimal GrossEarnings { get; init; }

    // Employee deductions
    public decimal ContributionBase { get; init; }
    public decimal EmployeeHealth { get; init; }
    public decimal EmployeePension { get; init; }
    public decimal SolidarityFund { get; init; }
    public decimal Withholding { get; init; }
    public decimal TotalDeductions => EmployeeHealth + EmployeePension + SolidarityFund + Withholding;
    public decimal NetPay => GrossEarnings - TotalDeductions;

    // Employer contributions (aportes y parafiscales)
    public decimal EmployerHealth { get; init; }
    public decimal EmployerPension { get; init; }
    public decimal Arl { get; init; }
    public decimal CompensationBox { get; init; }
    public decimal Sena { get; init; }
    public decimal Icbf { get; init; }
    public decimal TotalEmployerContributions =>
        EmployerHealth + EmployerPension + Arl + CompensationBox + Sena + Icbf;

    // Provisions (prestaciones sociales)
    public decimal Severance { get; init; }
    public decimal SeveranceInterest { get; init; }
    public decimal ServiceBonus { get; init; }
    public decimal Vacations { get; init; }
    public decimal TotalProvisions => Severance + SeveranceInterest + ServiceBonus + Vacations;

    public decimal TotalEmployerCost => GrossEarnings + TotalEmployerContributions + TotalProvisions;
}

public sealed class InvalidPayrollInputException : Exception
{
    public InvalidPayrollInputException(string message) : base(message) { }
}

public sealed class UnsupportedContractTypeException : Exception
{
    public ContractType ContractType { get; }

    public UnsupportedContractTypeException(ContractType contractType)
        : base($"No payroll strategy is registered for contract type {contractType}.")
        => ContractType = contractType;
}