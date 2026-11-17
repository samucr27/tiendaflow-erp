using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public record SalesReturnLineRequest(string ProductSku, int Quantity);

public record SalesReturnRow(
    int Id,
    int InvoiceId,
    DateTime CreatedAt,
    string Reason,
    int Units,
    decimal Total);

/// <summary>
/// What can still be returned from each line of an invoice.
/// </summary>
public record ReturnableLine(
    string ProductSku,
    string Description,
    int Invoiced,
    int AlreadyReturned,
    decimal UnitPrice)
{
    public int Returnable => Invoiced - AlreadyReturned;
}

/// <summary>
/// Returns and credit notes on issued invoices. The store manager authorizes
/// them (sales.returns.authorize); the returned units go back to the stock of
/// the store that issued the invoice, in the same transaction. A line cannot
/// return more units than were invoiced minus earlier returns.
/// </summary>
public sealed class SalesReturnService
{
    private readonly IInvoiceRepository _invoices;
    private readonly IEntityStore<SalesReturn> _returns;
    private readonly IStockRepository _stock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public SalesReturnService(
        IInvoiceRepository invoices,
        IEntityStore<SalesReturn> returns,
        IStockRepository stock,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock)
    {
        _invoices = invoices;
        _returns = returns;
        _stock = stock;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
    }

    public IReadOnlyList<SalesReturnRow> ListForInvoice(int invoiceId)
    {
        _accessControl.Demand(Permissions.SalesInvoicesView);

        return _returns.All()
            .Where(item => item.InvoiceId == invoiceId)
            .OrderByDescending(item => item.CreatedAt)
            .Select(ToRow)
            .ToList();
    }

    public IReadOnlyList<SalesReturnRow> ListBetween(DateTime fromUtc, DateTime toUtcExclusive) =>
        _returns.All()
            .Where(item => item.CreatedAt >= fromUtc && item.CreatedAt < toUtcExclusive)
            .OrderByDescending(item => item.CreatedAt)
            .Select(ToRow)
            .ToList();

    public IReadOnlyList<ReturnableLine> Returnable(int invoiceId)
    {
        _accessControl.Demand(Permissions.SalesReturnsAuthorize);

        var invoice = _invoices.GetById(invoiceId) ?? throw new InvoiceNotFoundException(invoiceId);
        return ReturnableLines(invoice);
    }

    public int Register(int invoiceId, string reason, IReadOnlyList<SalesReturnLineRequest> lines)
    {
        _accessControl.Demand(Permissions.SalesReturnsAuthorize);

        var invoice = _invoices.GetById(invoiceId) ?? throw new InvoiceNotFoundException(invoiceId);

        if (invoice.Status != InvoiceStatus.Issued)
            throw new InvalidSalesReturnException("Only an issued invoice accepts returns.");
        if (invoice.StoreId is not int storeId)
            throw new InvalidSalesReturnException("The invoice predates stock movements; cancel it instead.");

        var requested = lines.Where(line => line.Quantity > 0).ToList();
        if (requested.Count == 0)
            throw new InvalidSalesReturnException("Select at least one unit to return.");

        var available = ReturnableLines(invoice)
            .ToDictionary(line => line.ProductSku, StringComparer.OrdinalIgnoreCase);

        var salesReturn = new SalesReturn(invoice.Id, storeId, reason, _currentUser.UserId, _clock.UtcNow);

        foreach (var line in requested)
        {
            if (!available.TryGetValue(line.ProductSku, out var source))
                throw new InvalidSalesReturnException($"{line.ProductSku} is not part of the invoice.");
            if (line.Quantity > source.Returnable)
                throw new InvalidSalesReturnException(
                    $"{line.ProductSku}: only {source.Returnable} units can still be returned.");

            salesReturn.AddLine(source.ProductSku, source.Description, line.Quantity, source.UnitPrice);
        }

        _unitOfWork.ExecuteInTransaction(() =>
        {
            foreach (var line in salesReturn.Lines)
            {
                var item = _stock.Get(storeId, line.ProductSku)
                    ?? throw new StockNotFoundException(storeId, line.ProductSku);
                item.RegisterReturn(line.Quantity, salesReturn.CreatedAt);
                _stock.Update(item);
            }

            _returns.Add(salesReturn);

            _auditLog.Record(new AuditEntry(
                "SalesReturnRegistered",
                salesReturn.Id.ToString(),
                $"Devolución de la factura #{invoice.Id}: {salesReturn.Lines.Sum(line => line.Quantity)} unidades por {salesReturn.Total:C0}.",
                _currentUser.UserId,
                salesReturn.CreatedAt,
                module: "sales",
                ip: _currentUser.Ip));
        });

        return salesReturn.Id;
    }

    private IReadOnlyList<ReturnableLine> ReturnableLines(SalesInvoice invoice)
    {
        var returned = _returns.All()
            .Where(item => item.InvoiceId == invoice.Id)
            .SelectMany(item => item.Lines)
            .GroupBy(line => line.ProductSku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity), StringComparer.OrdinalIgnoreCase);

        return invoice.Lines
            .Select(line => new ReturnableLine(
                line.ProductSku,
                line.Description,
                line.Quantity,
                returned.GetValueOrDefault(line.ProductSku),
                line.UnitPrice))
            .ToList();
    }

    private static SalesReturnRow ToRow(SalesReturn item) =>
        new(item.Id, item.InvoiceId, item.CreatedAt, item.Reason, item.Lines.Sum(line => line.Quantity), item.Total);
}
