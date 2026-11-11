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

public record UpdateInvoiceRequest(
    string CustomerName,
    string CustomerDocument,
    DateTime IssueDate,
    IReadOnlyList<InvoiceLineRequest> Lines);

/// <summary>
/// Sales invoices issued at the store. The document stores the customer and
/// product data as a snapshot; cancellation replaces physical deletion so the
/// numbering and the history stay intact.
/// </summary>
public sealed class SalesInvoiceService
{
    private readonly IInvoiceRepository _invoices;
    private readonly IInvoiceQuery _query;
    private readonly IAccessControl _accessControl;
    private readonly IAuditLog _auditLog;
    private readonly ICurrentUser _currentUser;

    public SalesInvoiceService(
        IInvoiceRepository invoices,
        IInvoiceQuery query,
        IAccessControl accessControl,
        IAuditLog auditLog,
        ICurrentUser currentUser)
    {
        _invoices = invoices;
        _query = query;
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

    public SalesInvoice GetForEdit(int id)
    {
        _accessControl.Demand(Permissions.SalesInvoicesManage);
        return _invoices.GetById(id) ?? throw new InvoiceNotFoundException(id);
    }

    public int Create(CreateInvoiceRequest request)
    {
        _accessControl.Demand(Permissions.SalesInvoicesManage);
        RequireLines(request.Lines);

        var invoice = new SalesInvoice(
            id: 0,
            request.CustomerName,
            request.CustomerDocument,
            request.IssueDate);

        foreach (var line in request.Lines)
        {
            invoice.AddLine(line.ProductSku, line.Description, line.Quantity, line.UnitPrice);
        }

        _invoices.Add(invoice);
        Record("InvoiceCreated", invoice.Id, $"Factura #{invoice.Id} emitida para {invoice.CustomerName} por {invoice.Total:C0}.");
        return invoice.Id;
    }

    public void Update(int id, UpdateInvoiceRequest request)
    {
        _accessControl.Demand(Permissions.SalesInvoicesManage);
        RequireLines(request.Lines);

        var invoice = _invoices.GetById(id) ?? throw new InvoiceNotFoundException(id);

        invoice.UpdateDetails(request.CustomerName, request.CustomerDocument, request.IssueDate);
        invoice.ClearLines();

        foreach (var line in request.Lines)
        {
            invoice.AddLine(line.ProductSku, line.Description, line.Quantity, line.UnitPrice);
        }

        _invoices.Update(invoice);
        Record("InvoiceUpdated", invoice.Id, $"Factura #{invoice.Id} actualizada para {invoice.CustomerName} por {invoice.Total:C0}.");
    }

    public void Cancel(int id)
    {
        _accessControl.Demand(Permissions.SalesInvoicesManage);

        var invoice = _invoices.GetById(id) ?? throw new InvoiceNotFoundException(id);
        invoice.Cancel();
        _invoices.Update(invoice);
        Record("InvoiceCancelled", invoice.Id, $"Factura #{invoice.Id} anulada para {invoice.CustomerName}.");
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
