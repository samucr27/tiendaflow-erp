using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application.Reporting;

public sealed record SalesReport(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<InvoiceRow> Invoices,
    IReadOnlyList<PosSaleRow>? PosSales = null)
{
    public IReadOnlyList<PosSaleRow> PointOfSaleSales => PosSales ?? Array.Empty<PosSaleRow>();

    public int PosSalesCount => PointOfSaleSales.Count;

    public decimal PosSalesTotal => PointOfSaleSales.Sum(sale => sale.Total);

    /// <summary>Issued invoices plus point-of-sale sales.</summary>
    public decimal GrandTotal => IssuedTotal + PosSalesTotal;

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
            }).Concat(PointOfSaleSales.Select(sale => new[]
            {
                sale.Code,
                CsvReport.Date(LocalTime.ToLocal(sale.SoldAt)),
                "Venta de mostrador (POS)",
                sale.StoreName,
                "Venta POS",
                sale.Units.ToString(),
                CsvReport.Number(sale.Total)
            })));
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
    private readonly IPosSaleQuery? _posSales;

    public SalesReportService(
        IInvoiceQuery invoices,
        IAccessControl accessControl,
        IPosSaleQuery? posSales = null)
    {
        _invoices = invoices;
        _accessControl = accessControl;
        _posSales = posSales;
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

        var posSales = _posSales?
            .List(LocalTime.ToUtc(fromDate), LocalTime.ToUtc(toDate.AddDays(1)))
            .OrderByDescending(sale => sale.SoldAt)
            .ToList()
            ?? new List<PosSaleRow>();

        return new SalesReport(fromDate, toDate, invoices, posSales);
    }
}
