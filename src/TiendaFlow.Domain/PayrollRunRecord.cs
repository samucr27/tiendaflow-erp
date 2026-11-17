namespace TiendaFlow.Domain;

/// <summary>
/// Saved payroll run of one month: the parameters used and a snapshot of each
/// employee's result, so the month can be consulted after the screen closes.
/// There is one saved run per period.
/// </summary>
public class PayrollRunRecord
{
    public PayrollRunRecord(string period, int daysWorked, int riskClass, int? createdByUserId, DateTime createdAt)
    {
        if (string.IsNullOrWhiteSpace(period) || period.Length != 7 || period[4] != '-')
            throw new InvalidHrRecordException("The period must have the form yyyy-MM.");
        if (daysWorked is < 1 or > 30)
            throw new InvalidHrRecordException("The days worked must be between 1 and 30.");

        Period = period;
        DaysWorked = daysWorked;
        RiskClass = riskClass;
        CreatedByUserId = createdByUserId;
        CreatedAt = UtcTimestamp.Normalize(createdAt);
    }

    public int Id { get; private set; }

    public string Period { get; private set; }

    public int DaysWorked { get; private set; }

    public int RiskClass { get; private set; }

    public int? CreatedByUserId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public List<PayrollRunRecordLine> Lines { get; } = new();

    public decimal TotalGross => Lines.Sum(line => line.GrossEarnings);

    public decimal TotalDeductions => Lines.Sum(line => line.Deductions);

    public decimal TotalNet => Lines.Sum(line => line.NetPay);

    public decimal TotalEmployerCost => Lines.Sum(line => line.EmployerCost);

    public void AddLine(int employeeId, string employeeName, int daysApplied, decimal gross, decimal deductions,
        decimal netPay, decimal employerCost)
    {
        if (Lines.Any(line => line.EmployeeId == employeeId))
            throw new InvalidHrRecordException("The employee is already part of the run.");

        Lines.Add(new PayrollRunRecordLine(employeeId, employeeName, daysApplied, gross, deductions, netPay, employerCost));
    }
}

public class PayrollRunRecordLine
{
    public PayrollRunRecordLine(int employeeId, string employeeName, int daysApplied, decimal grossEarnings,
        decimal deductions, decimal netPay, decimal employerCost)
    {
        EmployeeId = employeeId;
        EmployeeName = employeeName;
        DaysApplied = daysApplied;
        GrossEarnings = grossEarnings;
        Deductions = deductions;
        NetPay = netPay;
        EmployerCost = employerCost;
    }

    public int Id { get; private set; }

    public int PayrollRunRecordId { get; private set; }

    public int EmployeeId { get; private set; }

    public string EmployeeName { get; private set; }

    public int DaysApplied { get; private set; }

    public decimal GrossEarnings { get; private set; }

    public decimal Deductions { get; private set; }

    public decimal NetPay { get; private set; }

    public decimal EmployerCost { get; private set; }
}
