using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public record InvoiceLineRequest(
    string ProductSku,
    string Description,
    int Quantity,
    decimal UnitPrice);

public record CreateInvoiceRequest(
    string CustomerName,
    string CustomerDocument,
    DateTime IssueDate,
    IReadOnlyList<InvoiceLineRequest> Lines);

/// <summary>
/// Sales invoices issued at the store. The document stores the customer and
/// product data as a snapshot. Issuing an invoice takes the units out of the
/// store stock and cancelling it puts them back, both in the same transaction
/// as the document. An issued invoice is never edited: a mistake is corrected
/// by cancelling it and issuing a new one, so numbering and history stay
/// intact.
/// </summary>
public sealed class SalesInvoiceService
{
    private readonly IInvoiceRepository _invoices;
    private readonly IInvoiceQuery _query;
    private readonly IStockRepository _stock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;

    public SalesInvoiceService(
        IInvoiceRepository invoices,
        IInvoiceQuery query,
        IStockRepository stock,
        IUnitOfWork unitOfWork,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser)
    {
        _invoices = invoices;
        _query = query;
        _stock = stock;
        _unitOfWork = unitOfWork;
        _accessControl = accessControl;
        _auditLog = auditLog;
        _currentUser = currentUser;
    }

    public IReadOnlyList<InvoiceRow> List()
    {
        _accessControl.Demand(Permissions.SalesInvoicesView);
        return _query.List();
    }

    public InvoiceDetail Get(int id)
    {
        _accessControl.Demand(Permissions.SalesInvoicesView);
        return _query.GetById(id) ?? throw new InvoiceNotFoundException(id);
    }

    public int Create(CreateInvoiceRequest request)
    {
        _accessControl.Demand(Permissions.SalesInvoicesManage);
        RequireLines(request.Lines);

        var storeId = _currentUser.StoreId
            ?? throw new InvalidInvoiceException("The user issuing the invoice has no store assigned.");

        var invoice = new SalesInvoice(
            id: 0,
            request.CustomerName,
            request.CustomerDocument,
            request.IssueDate,
            storeId);

        foreach (var line in request.Lines)
        {
            invoice.AddLine(line.ProductSku, line.Description, line.Quantity, line.UnitPrice);
        }

        _unitOfWork.ExecuteInTransaction(() =>
        {
            foreach (var line in invoice.Lines)
            {
                var item = _stock.Get(storeId, line.ProductSku)
                    ?? throw new StockNotFoundException(storeId, line.ProductSku);

                item.RegisterSale(line.Quantity, invoice.IssueDate);
                _stock.Update(item);
            }

            _invoices.Add(invoice);
            Record("InvoiceCreated", invoice.Id,
                $"Factura #{invoice.Id} emitida para {invoice.CustomerName} por {invoice.Total:C0}; descontó {invoice.Lines.Sum(line => line.Quantity)} unidades.");
        });

        return invoice.Id;
    }

    public void Cancel(int id)
    {
        _accessControl.Demand(Permissions.SalesInvoicesManage);

        var invoice = _invoices.GetById(id) ?? throw new InvoiceNotFoundException(id);

        _unitOfWork.ExecuteInTransaction(() =>
        {
            invoice.Cancel();

            // Invoices issued before invoicing moved stock have no store and
            // never took units out, so there is nothing to return.
            if (invoice.StoreId is int storeId)
            {
                foreach (var line in invoice.Lines)
                {
                    var item = _stock.Get(storeId, line.ProductSku)
                        ?? throw new StockNotFoundException(storeId, line.ProductSku);

                    item.RegisterReturn(line.Quantity, DateTime.UtcNow);
                    _stock.Update(item);
                }
            }

            _invoices.Update(invoice);
            Record("InvoiceCancelled", invoice.Id,
                invoice.StoreId is null
                    ? $"Factura #{invoice.Id} anulada para {invoice.CustomerName}."
                    : $"Factura #{invoice.Id} anulada para {invoice.CustomerName}; devolvió {invoice.Lines.Sum(line => line.Quantity)} unidades al stock.");
        });
    }

    private static void RequireLines(IReadOnlyList<InvoiceLineRequest> lines)
    {
        if (lines.Count == 0)
            throw new InvalidInvoiceException("An invoice needs at least one line.");
    }

    private void Record(string action, int entityId, string detail) =>
        _auditLog.Record(new AuditEntry(
            action,
            entityId.ToString(),
            detail,
            _currentUser.UserId,
            DateTime.UtcNow,
            module: "sales",
            ip: _currentUser.Ip));
}
