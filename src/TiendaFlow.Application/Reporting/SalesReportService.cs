using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application.Reporting;

public sealed record SalesReport(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<InvoiceRow> Invoices)
{
    public int IssuedCount => Invoices.Count(invoice => invoice.Status != InvoiceStatus.Cancelled);

    public int CancelledCount => Invoices.Count(invoice => invoice.Status == InvoiceStatus.Cancelled);

    public decimal IssuedTotal =>
        Invoices
            .Where(invoice => invoice.Status != InvoiceStatus.Cancelled)
            .Sum(invoice => invoice.Total);

    public decimal AverageTicket => IssuedCount == 0 ? 0m : IssuedTotal / IssuedCount;

    public string ToCsv() =>
        CsvReport.Build(
            ["Factura", "Fecha", "Cliente", "Documento", "Estado", "Lineas", "Total"],
            Invoices.Select(invoice => new[]
            {
                invoice.Id.ToString(),
                CsvReport.Date(LocalTime.ToLocal(invoice.IssueDate)),
                invoice.CustomerName,
                invoice.CustomerDocument,
                invoice.StatusLabel,
                invoice.LineCount.ToString(),
                CsvReport.Number(invoice.Total)
            }));
}

/// <summary>
/// Sales report for an inclusive local-date range. The range is interpreted in
/// the store time zone (Bogota) so "today" in the report matches the dates the
/// cashiers see, not the UTC date.
/// </summary>
public sealed class SalesReportService
{
    private readonly IInvoiceQuery _invoices;
    private readonly IAccessControl _accessControl;

    public SalesReportService(IInvoiceQuery invoices, IAccessControl accessControl)
    {
        _invoices = invoices;
        _accessControl = accessControl;
    }

    public SalesReport Get(DateOnly fromDate, DateOnly toDate)
    {
        _accessControl.Demand(Permissions.SalesReportsView);

        if (fromDate > toDate)
            throw new ArgumentException("The start date cannot be after the end date.");

        var invoices = _invoices
            .List()
            .Where(invoice =>
            {
                var localDate = DateOnly.FromDateTime(LocalTime.ToLocal(invoice.IssueDate));
                return localDate >= fromDate && localDate <= toDate;
            })
            .OrderByDescending(invoice => invoice.IssueDate)
            .ThenByDescending(invoice => invoice.Id)
            .ToList();

        return new SalesReport(fromDate, toDate, invoices);
    }
}
