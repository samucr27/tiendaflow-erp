using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

/// <summary>
/// Sold units per SKU from issued invoices. The local-date range is converted
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

        return
            (from line in _context.InvoiceLines.AsNoTracking()
             join invoice in _context.SalesInvoices.AsNoTracking()
                 on line.InvoiceId equals invoice.Id
             where invoice.Status == InvoiceStatus.Issued
                 && invoice.IssueDate >= start
                 && invoice.IssueDate < endExclusive
             group line by line.ProductSku into sold
             select new ProductConsumption(sold.Key, sold.Sum(line => line.Quantity)))
            .ToList();
    }
}
