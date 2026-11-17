using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public record SalesReturnLineRequest(string ProductSku, int Quantity);

public record CreateSalesReturnRequest(
    int InvoiceId,
    int StoreId,
    string Reason,
    IReadOnlyList<SalesReturnLineRequest> Lines);

/// <summary>
/// Customer returns and credit notes (Fase 5). A return is registered against
/// an issued invoice, cannot exceed the units sold minus the units already
/// authorized, and only becomes a credit note when an authorized user approves
/// it. Approval, the stock re-entry and the audit share one transaction; a
/// rejected return never touches the inventory.
/// </summary>
public sealed class SalesReturnService
{
    private readonly ISalesReturnRepository _returns;
    private readonly ISalesReturnQuery _query;
    private readonly IInvoiceRepository _invoices;
    private readonly IStockRepository _stock;
    private readonly IStoreRepository _stores;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public SalesReturnService(
        ISalesReturnRepository returns,
        ISalesReturnQuery query,
        IInvoiceRepository invoices,
        IStockRepository stock,
        IStoreRepository stores,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser,
        IClock clock)
    {
        _returns = returns;
        _query = query;
        _invoices = invoices;
        _stock = stock;
        _stores = stores;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
        _clock = clock;
    }

    public IReadOnlyList<SalesReturnRow> List(int? storeId = null)
    {
        _accessControl.Demand(Permissions.SalesReturnsAuthorize);
        return _query.List(storeId);
    }

    public SalesReturnDetail Get(int id)
    {
        _accessControl.Demand(Permissions.SalesReturnsAuthorize);
        return _query.GetById(id) ?? throw new SalesReturnNotFoundException(id);
    }

    public IReadOnlyList<SalesReturnStoreOption> Stores()
    {
        _accessControl.Demand(Permissions.SalesReturnsAuthorize);

        return _stores.List()
            .Where(store => store.Active)
            .OrderBy(store => store.Name, StringComparer.OrdinalIgnoreCase)
            .Select(store => new SalesReturnStoreOption(store.Id, store.Name))
            .ToList();
    }

    public IReadOnlyList<SalesReturnInvoiceOption> Invoices()
    {
        _accessControl.Demand(Permissions.SalesReturnsAuthorize);
        return _query.ListReturnableInvoices();
    }

    public IReadOnlyList<SalesReturnInvoiceLineOption> InvoiceLines(int invoiceId)
    {
        _accessControl.Demand(Permissions.SalesReturnsAuthorize);
        return _query.ListInvoiceLines(invoiceId);
    }

    public int Create(CreateSalesReturnRequest request)
    {
        _accessControl.Demand(Permissions.SalesReturnsAuthorize);

        var invoice = _invoices.GetById(request.InvoiceId)
            ?? throw new InvoiceNotFoundException(request.InvoiceId);

        if (invoice.Status != InvoiceStatus.Issued)
            throw new InvalidSalesReturnException("Only an issued invoice can be returned.");

        var store = _stores.GetById(request.StoreId)
            ?? throw new StoreNotFoundException(request.StoreId);

        if (!store.Active)
            throw new InvalidSalesReturnException("An inactive store cannot receive a return.");

        var lines = ValidateLines(
            invoice,
            request.Lines,
            AuthorizedQuantities(invoice.Id));

        var salesReturn = new SalesReturn(
            id: 0,
            invoice.Id,
            store.Id,
            invoice.CustomerName,
            invoice.CustomerDocument,
            request.Reason,
            _clock.UtcNow);

        foreach (var (line, quantity) in lines)
            salesReturn.AddLine(line.ProductSku, line.Description, quantity, line.UnitPrice);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            _returns.Add(salesReturn);
            Audit(
                "SalesReturnCreated",
                salesReturn.Id.ToString(),
                $"Devolución {salesReturn.Id} registrada para la factura #{invoice.Id} " +
                $"en {store.Name}: {salesReturn.Lines.Count} productos por {salesReturn.Total:C0}.");
        });

        return salesReturn.Id;
    }

    public void Authorize(int id)
    {
        _accessControl.Demand(Permissions.SalesReturnsAuthorize);

        var salesReturn = _returns.GetById(id)
            ?? throw new SalesReturnNotFoundException(id);

        if (salesReturn.Status != SalesReturnStatus.Pending)
            throw new InvalidSalesReturnException("Only a pending return can be authorized.");

        var invoice = _invoices.GetById(salesReturn.InvoiceId)
            ?? throw new InvoiceNotFoundException(salesReturn.InvoiceId);

        // Revalidate the units at approval time: another return of the same
        // invoice may have been authorized while this one waited.
        ValidateLines(
            invoice,
            salesReturn.Lines
                .Select(line => new SalesReturnLineRequest(line.ProductSku, line.Quantity))
                .ToList(),
            AuthorizedQuantities(invoice.Id, exceptReturnId: salesReturn.Id));

        // Resolve every stock row before mutating anything so a missing row
        // never leaves a half-updated return in the tracking context.
        var stockRows = new List<(SalesReturnLine Line, StockItem Stock)>();

        foreach (var line in salesReturn.Lines)
        {
            var stock = _stock.Get(salesReturn.StoreId, line.ProductSku)
                ?? throw new StockNotFoundException(salesReturn.StoreId, line.ProductSku);

            stockRows.Add((line, stock));
        }

        _unitOfWork.ExecuteInTransaction(() =>
        {
            var now = _clock.UtcNow;

            salesReturn.Authorize(now, CreditNoteNumber(salesReturn.Id));

            foreach (var (line, stock) in stockRows)
            {
                stock.RegisterReturnIn(line.Quantity, now);
                _stock.Update(stock);
            }

            _returns.Update(salesReturn);

            var units = salesReturn.Lines.Sum(line => line.Quantity);

            Audit(
                "SalesReturnAuthorized",
                salesReturn.Id.ToString(),
                $"Devolución {salesReturn.Id} autorizada: nota crédito " +
                $"{salesReturn.CreditNoteNumber}, {units} unidades por {salesReturn.Total:C0}.");
        });
    }

    public void Reject(int id)
    {
        _accessControl.Demand(Permissions.SalesReturnsAuthorize);

        var salesReturn = _returns.GetById(id)
            ?? throw new SalesReturnNotFoundException(id);

        if (salesReturn.Status != SalesReturnStatus.Pending)
            throw new InvalidSalesReturnException("Only a pending return can be rejected.");

        _unitOfWork.ExecuteInTransaction(() =>
        {
            salesReturn.Reject(_clock.UtcNow);
            _returns.Update(salesReturn);

            Audit(
                "SalesReturnRejected",
                salesReturn.Id.ToString(),
                $"Devolución {salesReturn.Id} rechazada para la factura #{salesReturn.InvoiceId}.");
        });
    }

    private static string CreditNoteNumber(int id) => $"NC-{id:D6}";

    private static List<(InvoiceLine Line, int Quantity)> ValidateLines(
        SalesInvoice invoice,
        IReadOnlyList<SalesReturnLineRequest> requested,
        IReadOnlyDictionary<string, int> authorized)
    {
        if (requested.Count == 0)
            throw new InvalidSalesReturnException("A return needs at least one line.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<(InvoiceLine Line, int Quantity)>();

        foreach (var item in requested)
        {
            var sku = item.ProductSku?.Trim() ?? string.Empty;

            if (sku.Length == 0)
                throw new InvalidSalesReturnException("The product SKU is required.");

            if (!seen.Add(sku))
                throw new InvalidSalesReturnException($"{sku} appears more than once in the return.");

            var line = invoice.Lines.FirstOrDefault(candidate =>
                string.Equals(candidate.ProductSku, sku, StringComparison.Ordinal))
                ?? throw new InvalidSalesReturnException(
                    $"{sku} is not part of invoice #{invoice.Id}.");

            if (item.Quantity <= 0)
                throw new InvalidSalesReturnException("The quantity must be greater than zero.");

            authorized.TryGetValue(sku, out var returned);

            var available = line.Quantity - returned;

            if (item.Quantity > available)
                throw new InvalidSalesReturnException(
                    $"Only {available} units of {sku} can still be returned.");

            result.Add((line, item.Quantity));
        }

        return result;
    }

    private Dictionary<string, int> AuthorizedQuantities(int invoiceId, int? exceptReturnId = null) =>
        _returns.ListByInvoice(invoiceId)
            .Where(item => item.Status == SalesReturnStatus.Authorized
                && item.Id != exceptReturnId)
            .SelectMany(item => item.Lines)
            .GroupBy(line => line.ProductSku, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(line => line.Quantity),
                StringComparer.Ordinal);

    private void Audit(string action, string entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId,
            detail,
            _currentUser.UserId,
            _clock.UtcNow,
            module: "sales",
            ip: _currentUser.Ip));
}
