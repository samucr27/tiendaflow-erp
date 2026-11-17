using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application.Payroll;

public sealed record SavedPayrollRunRow(
    int Id,
    string Period,
    int DaysWorked,
    int RiskClass,
    DateTime CreatedAt,
    int EmployeeCount,
    decimal TotalGross,
    decimal TotalNet,
    decimal TotalEmployerCost);

/// <summary>
/// Saves a calculated payroll run as the run of its month and lists the saved
/// runs (hr.payroll.manage). A month has one saved run; employees with a
/// problem are not part of the snapshot. Only ids and totals reach the audit.
/// </summary>
public sealed class PayrollHistoryService
{
    private readonly IEntityStore<PayrollRunRecord> _runs;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public PayrollHistoryService(
        IEntityStore<PayrollRunRecord> runs,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock)
    {
        _runs = runs;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
    }

    public int Save(PayrollRun run, int year, int month)
    {
        _accessControl.Demand(Permissions.HrPayrollManage);

        var period = $"{year:D4}-{month:D2}";
        if (_runs.All().Any(saved => saved.Period == period))
            throw new InvalidHrRecordException("A payroll run is already saved for the period.");

        var calculated = run.Lines.Where(line => line.Result is not null).ToList();
        if (calculated.Count == 0)
            throw new InvalidHrRecordException("The run has no calculated employees to save.");

        var record = new PayrollRunRecord(period, run.DaysWorked, run.RiskClass, _currentUser.UserId, _clock.UtcNow);
        foreach (var line in calculated)
        {
            var result = line.Result!;
            record.AddLine(line.EmployeeId, line.Name, line.DaysApplied ?? run.DaysWorked, result.GrossEarnings,
                result.TotalDeductions, result.NetPay, result.TotalEmployerCost);
        }

        _runs.Add(record);
        _auditLog.Record(new AuditEntry(
            "PayrollRunSaved",
            record.Id.ToString(),
            $"Corrida {period} guardada con {record.Lines.Count} empleados",
            _currentUser.UserId,
            _clock.UtcNow,
            module: "hr",
            ip: _currentUser.Ip));

        return record.Id;
    }

    public IReadOnlyList<SavedPayrollRunRow> List()
    {
        _accessControl.Demand(Permissions.HrPayrollManage);

        return _runs.All()
            .OrderByDescending(run => run.Period)
            .Select(run => new SavedPayrollRunRow(run.Id, run.Period, run.DaysWorked, run.RiskClass, run.CreatedAt,
                run.Lines.Count, run.TotalGross, run.TotalNet, run.TotalEmployerCost))
            .ToList();
    }
}
