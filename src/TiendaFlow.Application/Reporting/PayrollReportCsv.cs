using TiendaFlow.Application.Payroll;

namespace TiendaFlow.Application.Reporting;

/// <summary>
/// Turns a payroll run into a CSV file. Lines without a result keep their
/// observation, so an employee with a data problem is visible in the export
/// instead of disappearing from the total.
/// </summary>
public static class PayrollReportCsv
{
    public static string Build(PayrollRun run) =>
        CsvReport.Build(
            [
                "Empleado",
                "Cargo",
                "Contrato",
                "Devengado",
                "Deducciones",
                "Neto",
                "Costo empleador",
                "Observacion"
            ],
            run.Lines.Select(line => new[]
            {
                line.Name,
                line.Position,
                line.ContractLabel,
                CsvReport.Number(line.Result?.GrossEarnings ?? 0m),
                CsvReport.Number(line.Result?.TotalDeductions ?? 0m),
                CsvReport.Number(line.Result?.NetPay ?? 0m),
                CsvReport.Number(line.Result?.TotalEmployerCost ?? 0m),
                line.Problem ?? string.Empty
            }));
}
