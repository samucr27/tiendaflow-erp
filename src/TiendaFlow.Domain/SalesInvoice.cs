namespace TiendaFlow.Domain;

public enum InvoiceStatus
{
    Issued,
    Cancelled
}

/// <summary>
/// A sales invoice issued at the store. It stores the customer data as a
/// snapshot and the product data of each line, so later changes to the
/// customer registry or the catalog never rewrite an issued document.
/// The electronic invoice (DIAN) is out of scope for this sprint.
/// </summary>
public class SalesInvoice
{
    public SalesInvoice(
        int id,
        string customerName,
        string customerDocument,
        DateTime issueDate)
    {
        RequireText(customerName, "The customer name is required.");
        RequireText(customerDocument, "The customer document is required.");

        Id = id;
        CustomerName = customerName.Trim();
        CustomerDocument = customerDocument.Trim();
        IssueDate = UtcTimestamp.Normalize(issueDate);
        Status = InvoiceStatus.Issued;
        Total = 0m;
    }

    public int Id { get; private set; }

    public string CustomerName { get; private set; }

    public string CustomerDocument { get; private set; }

    public DateTime IssueDate { get; private set; }

    public InvoiceStatus Status { get; private set; }

    public decimal Total { get; private set; }

    public List<InvoiceLine> Lines { get; } = new();

    public void UpdateDetails(string customerName, string customerDocument, DateTime issueDate)
    {
        RequireIssued("edit");
        RequireText(customerName, "The customer name is required.");
        RequireText(customerDocument, "The customer document is required.");

        CustomerName = customerName.Trim();
        CustomerDocument = customerDocument.Trim();
        IssueDate = UtcTimestamp.Normalize(issueDate);
    }

    public void AddLine(string productSku, string description, int quantity, decimal unitPrice)
    {
        RequireIssued("add lines");

        var sku = productSku.Trim();
        if (Lines.Any(line => line.ProductSku == sku))
            throw new InvalidInvoiceException($"{sku} is already part of the invoice.");

        Lines.Add(new InvoiceLine(Id, sku, description, quantity, unitPrice));
        RecalculateTotal();
    }

    public void ClearLines()
    {
        RequireIssued("edit");
        Lines.Clear();
        Total = 0m;
    }

    public void Cancel()
    {
        if (Status == InvoiceStatus.Cancelled)
            throw new InvalidInvoiceException("The invoice is already cancelled.");

        Status = InvoiceStatus.Cancelled;
    }

    private void RequireIssued(string action)
    {
        if (Status != InvoiceStatus.Issued)
            throw new InvalidInvoiceException($"Cannot {action} a cancelled invoice.");
    }

    private void RecalculateTotal() =>
        Total = Lines.Sum(line => line.LineTotal);

    private static void RequireText(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidInvoiceException(message);
    }
}

/// <summary>
/// One product sold in the invoice. The description and the unit price are
/// copied at issue time; the line does not depend on the current catalog.
/// </summary>
public class InvoiceLine
{
    public InvoiceLine(
        int invoiceId,
        string productSku,
        string description,
        int quantity,
        decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(productSku))
            throw new InvalidInvoiceException("The product SKU is required.");
        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidInvoiceException("The line description is required.");
        if (quantity <= 0)
            throw new InvalidInvoiceException("The quantity must be greater than zero.");
        if (unitPrice <= 0)
            throw new InvalidInvoiceException("The unit price must be greater than zero.");

        InvoiceId = invoiceId;
        ProductSku = productSku.Trim();
        Description = description.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public int Id { get; private set; }

    public int InvoiceId { get; private set; }

    public string ProductSku { get; private set; }

    public string Description { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;
}
