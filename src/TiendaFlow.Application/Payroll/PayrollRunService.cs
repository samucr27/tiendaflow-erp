using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application.Payroll;

public sealed record PayrollRunLine(
    int EmployeeId,
    string Name,
    string Position,
    string ContractLabel,
    PayrollResult? Result,
    string? Problem,
    int? DaysApplied = null);

public sealed record PayrollRun(
    int DaysWorked,
    int RiskClass,
    IReadOnlyList<PayrollRunLine> Lines)
{
    public int CalculatedCount => Lines.Count(line => line.Result is not null);

    public int ProblemCount => Lines.Count(line => line.Result is null);

    public decimal TotalGrossEarnings => Sum(result => result.GrossEarnings);

    public decimal TotalDeductions => Sum(result => result.TotalDeductions);

    public decimal TotalNetPay => Sum(result => result.NetPay);

    public decimal TotalEmployerCost => Sum(result => result.TotalEmployerCost);

    private decimal Sum(Func<PayrollResult, decimal> selector) =>
        Lines
            .Where(line => line.Result is not null)
            .Sum(line => selector(line.Result!));
}

/// <summary>
/// Runs the monthly payroll for every active employee. One employee with a
/// missing salary, an unknown contract or an invalid input produces a line
/// with a visible problem instead of failing the whole run. Results are not
/// persisted: the run is a calculation screen only.
/// </summary>
public sealed class PayrollRunService
{
    private readonly IEmployeeRepository _employees;
    private readonly IPayrollEngine _engine;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly AdministratorVisibility? _visibility;

    public PayrollRunService(
        IEmployeeRepository employees,
        IPayrollEngine engine,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        AdministratorVisibility? visibility = null)
    {
        _employees = employees;
        _engine = engine;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _visibility = visibility;
    }

    /// <summary>
    /// Calculates the month for every active employee. When the unpaid
    /// absences recorded in attendance are given, each employee is paid for
    /// the days worked minus their absences.
    /// </summary>
    public PayrollRun Calculate(
        int daysWorked,
        int riskClass,
        IReadOnlyDictionary<int, int>? unpaidAbsences = null)
    {
        _accessControl.Demand(Permissions.HrPayrollManage);

        if (daysWorked is < 1 or > 30)
            throw new InvalidPayrollInputException("Los días trabajados deben estar entre 1 y 30.");
        if (riskClass is < 1 or > 5)
            throw new InvalidPayrollInputException("La clase de riesgo ARL debe estar entre 1 y 5.");

        var hidden = _visibility?.HiddenEmployeeIds() ?? new HashSet<int>();

        var employees = _employees.List()
            .Where(employee => employee.Active)
            .Where(employee => !hidden.Contains(employee.Id))
            .OrderBy(employee => employee.Name)
            .ToList();

        var lines = employees
            .Select(employee => CalculateLine(
                employee,
                daysWorked - (unpaidAbsences?.GetValueOrDefault(employee.Id) ?? 0),
                riskClass))
            .ToList();

        Record("PayrollCalculated", $"Nómina calculada para {employees.Count} empleados activos; {daysWorked} días");

        return new PayrollRun(daysWorked, riskClass, lines);
    }

    private PayrollRunLine CalculateLine(Employee employee, int daysWorked, int riskClass)
    {
        var contractLabel = ContractTypeLabels.ToLabel(employee.ContractType);

        if (daysWorked < 1)
        {
            return Line(
                employee,
                contractLabel,
                problem: "Las ausencias registradas cubren todo el periodo: no hay días por liquidar.");
        }

        if (employee.Salary <= 0m)
        {
            return Line(
                employee,
                contractLabel,
                problem: "El empleado no tiene salario asignado. Asigna un salario para incluirlo en la nómina.");
        }

        if (!ContractTypeLabels.TryParse(employee.ContractType, out var contractType))
        {
            return Line(
                employee,
                contractLabel,
                problem: "El tipo de contrato registrado no es reconocido.");
        }

        try
        {
            var result = _engine.Calculate(new PayrollInput(
                employee.Id,
                contractType,
                employee.Salary,
                daysWorked,
                OtherSalaryEarnings: 0m,
                RiskClass: riskClass));

            return Line(employee, contractLabel, result: result, days: daysWorked);
        }
        catch (Exception exception) when (exception is InvalidPayrollInputException
                                             or UnsupportedContractTypeException)
        {
            var problem = exception is UnsupportedContractTypeException
                ? "No hay una regla de nómina configurada para el tipo de contrato."
                : "Los datos del empleado no permiten calcular la nómina.";

            return Line(employee, contractLabel, problem: problem);
        }
    }

    private static PayrollRunLine Line(
        Employee employee,
        string contractLabel,
        PayrollResult? result = null,
        string? problem = null,
        int? days = null) =>
        new(employee.Id, employee.Name, employee.Position, contractLabel, result, problem, days);

    private void Record(string action, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            "payroll-run",
            detail,
            _currentUser.UserId,
            DateTime.UtcNow,
            module: "hr",
            ip: _currentUser.Ip));
}