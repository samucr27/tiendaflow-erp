using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfInvoiceRepository : IInvoiceRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfInvoiceRepository(TiendaFlowDbContext context) => _context = context;

    public SalesInvoice? GetById(int id) =>
        _context.SalesInvoices
            .Include(invoice => invoice.Lines)
            .FirstOrDefault(invoice => invoice.Id == id);

    public void Add(SalesInvoice invoice)
    {
        _context.SalesInvoices.Add(invoice);
        _context.SaveChanges();
    }

    public void Update(SalesInvoice invoice)
    {
        if (_context.Entry(invoice).State == EntityState.Detached)
            _context.SalesInvoices.Update(invoice);

        _context.SaveChanges();
    }
}

public sealed class EfInvoiceQuery : IInvoiceQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfInvoiceQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<InvoiceRow> List() =>
        _context.SalesInvoices
            .AsNoTracking()
            .OrderByDescending(invoice => invoice.IssueDate)
            .ThenByDescending(invoice => invoice.Id)
            .Select(invoice => new InvoiceRow(
                invoice.Id,
                invoice.CustomerName,
                invoice.CustomerDocument,
                invoice.IssueDate,
                invoice.Status,
                invoice.Lines.Count,
                invoice.Lines.Sum(line => line.Quantity * line.UnitPrice)))
            .ToList();

    public InvoiceDetail? GetById(int id)
    {
        var invoice = _context.SalesInvoices
            .AsNoTracking()
            .Include(item => item.Lines)
            .FirstOrDefault(item => item.Id == id);

        if (invoice is null)
            return null;

        return new InvoiceDetail(
            invoice.Id,
            invoice.CustomerName,
            invoice.CustomerDocument,
            invoice.IssueDate,
            invoice.Status,
            invoice.Lines
                .OrderBy(line => line.Id)
                .Select(line => new InvoiceLineRow(
                    line.ProductSku,
                    line.Description,
                    line.Quantity,
                    line.UnitPrice))
                .ToList());
    }
}
