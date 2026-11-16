using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

/// <summary>
/// Sold units per SKU from issued invoices and point-of-sale sales. The local-date range is converted
/// to UTC instants before querying, so the window matches the dates the
/// reports show even though the invoice timestamp is stored in UTC.
/// </summary>
public sealed class EfConsumptionQuery : IConsumptionQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfConsumptionQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<ProductConsumption> Get(DateOnly from, DateOnly to)
    {
        var start = LocalTime.ToUtc(from);
        var endExclusive = LocalTime.ToUtc(to.AddDays(1));

        var invoiced =
            (from line in _context.InvoiceLines.AsNoTracking()
             join invoice in _context.SalesInvoices.AsNoTracking()
                 on line.InvoiceId equals invoice.Id
             where invoice.Status == InvoiceStatus.Issued
                 && invoice.IssueDate >= start
                 && invoice.IssueDate < endExclusive
             group line by line.ProductSku into sold
             select new { Sku = sold.Key, Units = sold.Sum(line => line.Quantity) })
            .ToList();

        var soldAtPos =
            (from line in _context.PosSaleLines.AsNoTracking()
             join sale in _context.PosSales.AsNoTracking()
                 on line.PosSaleId equals sale.Id
             where sale.SoldAt >= start && sale.SoldAt < endExclusive
             group line by line.ProductSku into sold
             select new { Sku = sold.Key, Units = sold.Sum(line => line.Quantity) })
            .ToList();

        return invoiced
            .Concat(soldAtPos)
            .GroupBy(row => row.Sku, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ProductConsumption(group.Key, group.Sum(row => row.Units)))
            .ToList();
    }
}
