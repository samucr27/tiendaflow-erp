using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public record InvoiceRow(
    int Id,
    string CustomerName,
    string CustomerDocument,
    DateTime IssueDate,
    InvoiceStatus Status,
    int LineCount,
    decimal Total)
{
    public string StatusLabel => Status == InvoiceStatus.Cancelled ? "Anulada" : "Emitida";
}

public record InvoiceLineRow(
    string ProductSku,
    string Description,
    int Quantity,
    decimal UnitPrice)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

public record InvoiceDetail(
    int Id,
    string CustomerName,
    string CustomerDocument,
    DateTime IssueDate,
    InvoiceStatus Status,
    IReadOnlyList<InvoiceLineRow> Lines)
{
    public decimal Total => Lines.Sum(line => line.LineTotal);

    public string StatusLabel => Status == InvoiceStatus.Cancelled ? "Anulada" : "Emitida";
}
