using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public sealed record SalesReturnRow(
    int Id,
    int InvoiceId,
    int StoreId,
    string StoreName,
    string CustomerName,
    string CustomerDocument,
    string Reason,
    SalesReturnStatus Status,
    string? CreditNoteNumber,
    int LineCount,
    decimal Total,
    DateTime CreatedAt,
    DateTime? AuthorizedAt)
{
    public string StatusLabel => Status switch
    {
        SalesReturnStatus.Pending => "Pendiente",
        SalesReturnStatus.Authorized => "Autorizada",
        SalesReturnStatus.Rejected => "Rechazada",
        _ => Status.ToString()
    };
}

public sealed record SalesReturnLineRow(
    string ProductSku,
    string Description,
    int Quantity,
    decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

public sealed record SalesReturnDetail(
    SalesReturnRow Return,
    IReadOnlyList<SalesReturnLineRow> Lines)
{
    public decimal Total => Lines.Sum(line => line.LineTotal);
}

public sealed record SalesReturnInvoiceOption(
    int Id,
    string CustomerName,
    string CustomerDocument,
    DateTime IssueDate,
    decimal Total);

public sealed record SalesReturnInvoiceLineOption(
    string ProductSku,
    string Description,
    int InvoicedQuantity,
    int ReturnedQuantity,
    int ReturnableQuantity,
    decimal UnitPrice)
{
    public decimal LineTotal => ReturnableQuantity * UnitPrice;
}

public sealed record SalesReturnStoreOption(int Id, string Name);

public interface ISalesReturnRepository
{
    SalesReturn? GetById(int id);

    IReadOnlyList<SalesReturn> ListByInvoice(int invoiceId);

    void Add(SalesReturn salesReturn);

    void Update(SalesReturn salesReturn);
}

/// <summary>
/// Read side of the returns module. The invoice options exclude cancelled
/// invoices and already show the units that remain returnable, so the screen
/// never offers a quantity the business rule would reject.
/// </summary>
public interface ISalesReturnQuery
{
    IReadOnlyList<SalesReturnRow> List(int? storeId = null);

    SalesReturnDetail? GetById(int id);

    IReadOnlyList<SalesReturnInvoiceOption> ListReturnableInvoices(int? storeId = null);

    IReadOnlyList<SalesReturnInvoiceLineOption> ListInvoiceLines(int invoiceId, int? storeId = null);
}
