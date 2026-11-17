using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfSalesReturnRepository : ISalesReturnRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfSalesReturnRepository(TiendaFlowDbContext context) => _context = context;

    public SalesReturn? GetById(int id) =>
        _context.SalesReturns
            .Include(item => item.Lines)
            .FirstOrDefault(item => item.Id == id);

    public IReadOnlyList<SalesReturn> ListByInvoice(int invoiceId) =>
        _context.SalesReturns
            .AsNoTracking()
            .Include(item => item.Lines)
            .Where(item => item.InvoiceId == invoiceId)
            .OrderByDescending(item => item.Id)
            .ToList();

    public void Add(SalesReturn salesReturn)
    {
        _context.SalesReturns.Add(salesReturn);
        _context.SaveChanges();
    }

    public void Update(SalesReturn salesReturn)
    {
        if (_context.Entry(salesReturn).State == EntityState.Detached)
            _context.SalesReturns.Update(salesReturn);

        _context.SaveChanges();
    }
}

/// <summary>
/// Read models for the returns screens. Filters and orders run on the entity
/// query before materialization; the store name is joined afterwards.
/// </summary>
public sealed class EfSalesReturnQuery : ISalesReturnQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfSalesReturnQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<SalesReturnRow> List(int? storeId = null)
    {
        var stores = StoreNames();
        var query = _context.SalesReturns
            .AsNoTracking()
            .Include(item => item.Lines)
            .AsQueryable();

        if (storeId is not null)
            query = query.Where(item => item.StoreId == storeId);

        return query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ToList()
            .Select(item => ToRow(item, stores))
            .ToList();
    }

    public SalesReturnDetail? GetById(int id)
    {
        var salesReturn = _context.SalesReturns
            .AsNoTracking()
            .Include(item => item.Lines)
            .FirstOrDefault(item => item.Id == id);

        if (salesReturn is null)
            return null;

        var lines = salesReturn.Lines
            .OrderBy(line => line.Id)
            .Select(line => new SalesReturnLineRow(
                line.ProductSku,
                line.Description,
                line.Quantity,
                line.UnitPrice))
            .ToList();

        return new SalesReturnDetail(ToRow(salesReturn, StoreNames()), lines);
    }

    public IReadOnlyList<SalesReturnInvoiceOption> ListReturnableInvoices() =>
        _context.SalesInvoices
            .AsNoTracking()
            .Where(invoice => invoice.Status == InvoiceStatus.Issued)
            .OrderByDescending(invoice => invoice.IssueDate)
            .ThenByDescending(invoice => invoice.Id)
            .Select(invoice => new SalesReturnInvoiceOption(
                invoice.Id,
                invoice.CustomerName,
                invoice.CustomerDocument,
                invoice.IssueDate,
                invoice.Lines.Sum(line => line.Quantity * line.UnitPrice)))
            .ToList();

    public IReadOnlyList<SalesReturnInvoiceLineOption> ListInvoiceLines(int invoiceId)
    {
        var invoice = _context.SalesInvoices
            .AsNoTracking()
            .Include(item => item.Lines)
            .FirstOrDefault(item => item.Id == invoiceId);

        if (invoice is null)
            return Array.Empty<SalesReturnInvoiceLineOption>();

        var returned = _context.SalesReturns
            .AsNoTracking()
            .Where(item => item.InvoiceId == invoiceId
                && item.Status == SalesReturnStatus.Authorized)
            .SelectMany(item => item.Lines)
            .GroupBy(line => line.ProductSku)
            .Select(group => new
            {
                Sku = group.Key,
                Quantity = group.Sum(line => line.Quantity)
            })
            .ToList()
            .ToDictionary(row => row.Sku, row => row.Quantity, StringComparer.Ordinal);

        return invoice.Lines
            .OrderBy(line => line.Id)
            .Select(line =>
            {
                returned.TryGetValue(line.ProductSku, out var returnedQuantity);

                return new SalesReturnInvoiceLineOption(
                    line.ProductSku,
                    line.Description,
                    line.Quantity,
                    returnedQuantity,
                    Math.Max(0, line.Quantity - returnedQuantity),
                    line.UnitPrice);
            })
            .ToList();
    }

    private static SalesReturnRow ToRow(
        SalesReturn item,
        IReadOnlyDictionary<int, string> stores) =>
        new(
            item.Id,
            item.InvoiceId,
            item.StoreId,
            stores.TryGetValue(item.StoreId, out var name) ? name : $"Tienda {item.StoreId}",
            item.CustomerName,
            item.CustomerDocument,
            item.Reason,
            item.Status,
            item.CreditNoteNumber,
            item.Lines.Count,
            item.Total,
            item.CreatedAt,
            item.AuthorizedAt);

    private Dictionary<int, string> StoreNames() =>
        _context.Stores
            .AsNoTracking()
            .ToDictionary(store => store.Id, store => store.Name);
}
