namespace TiendaFlow.Domain;

public enum SalesReturnStatus
{
    Pending,
    Authorized,
    Rejected
}

/// <summary>
/// A customer return tied to an issued invoice. The operator registers the
/// units that come back; the document stays pending until an authorized user
/// approves it. Approval assigns the credit-note number, returns the units to
/// the store stock and locks the document forever.
/// </summary>
public class SalesReturn
{
    public SalesReturn(
        int id,
        int invoiceId,
        int storeId,
        string customerName,
        string customerDocument,
        string reason,
        DateTime createdAt)
    {
        if (invoiceId <= 0)
            throw new InvalidSalesReturnException("An issued invoice is required.");
        if (storeId <= 0)
            throw new InvalidSalesReturnException("A store is required.");
        RequireText(customerName, "The customer name is required.");
        RequireText(customerDocument, "The customer document is required.");
        RequireText(reason, "A return reason is required.");

        Id = id;
        InvoiceId = invoiceId;
        StoreId = storeId;
        CustomerName = customerName.Trim();
        CustomerDocument = customerDocument.Trim();
        Reason = reason.Trim();
        CreatedAt = UtcTimestamp.Normalize(createdAt);
        Status = SalesReturnStatus.Pending;
        Total = 0m;
    }

    public int Id { get; private set; }

    public int InvoiceId { get; private set; }

    public int StoreId { get; private set; }

    public string CustomerName { get; }

    public string CustomerDocument { get; }

    public string Reason { get; }

    public SalesReturnStatus Status { get; private set; }

    public string? CreditNoteNumber { get; private set; }

    public DateTime CreatedAt { get; }

    public DateTime? AuthorizedAt { get; private set; }

    public DateTime? RejectedAt { get; private set; }

    public decimal Total { get; private set; }

    public List<SalesReturnLine> Lines { get; } = new();

    public void AddLine(string productSku, string description, int quantity, decimal unitPrice)
    {
        RequirePending("add lines");

        var sku = productSku.Trim();

        if (Lines.Any(line => string.Equals(line.ProductSku, sku, StringComparison.Ordinal)))
            throw new InvalidSalesReturnException($"{sku} is already part of the return.");

        Lines.Add(new SalesReturnLine(Id, sku, description, quantity, unitPrice));
        RecalculateTotal();
    }

    /// <summary>
    /// Approves the return once. The caller supplies the credit-note number so
    /// the domain does not depend on the document identity sequence.
    /// </summary>
    public void Authorize(DateTime date, string creditNoteNumber)
    {
        RequirePending("authorize");

        if (Lines.Count == 0)
            throw new InvalidSalesReturnException("A return needs at least one line.");

        RequireText(creditNoteNumber, "The credit note number is required.");

        Status = SalesReturnStatus.Authorized;
        CreditNoteNumber = creditNoteNumber.Trim();
        AuthorizedAt = UtcTimestamp.Normalize(date);
    }

    public void Reject(DateTime date)
    {
        RequirePending("reject");

        Status = SalesReturnStatus.Rejected;
        RejectedAt = UtcTimestamp.Normalize(date);
    }

    private void RequirePending(string action)
    {
        if (Status != SalesReturnStatus.Pending)
            throw new InvalidSalesReturnException(
                $"Cannot {action} a return that is already {Status}.");
    }

    private void RecalculateTotal() =>
        Total = Lines.Sum(line => line.LineTotal);

    private static void RequireText(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidSalesReturnException(message);
    }
}

/// <summary>
/// One product coming back. The description and the unit price are copied from
/// the invoice at registration time, so a later catalog change never rewrites
/// the credit note.
/// </summary>
public class SalesReturnLine
{
    public SalesReturnLine(
        int returnId,
        string productSku,
        string description,
        int quantity,
        decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(productSku))
            throw new InvalidSalesReturnException("The product SKU is required.");
        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidSalesReturnException("The line description is required.");
        if (quantity <= 0)
            throw new InvalidSalesReturnException("The quantity must be greater than zero.");
        if (unitPrice <= 0)
            throw new InvalidSalesReturnException("The unit price must be greater than zero.");

        ReturnId = returnId;
        ProductSku = productSku.Trim();
        Description = description.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public int Id { get; private set; }

    public int ReturnId { get; private set; }

    public string ProductSku { get; private set; }

    public string Description { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;
}
