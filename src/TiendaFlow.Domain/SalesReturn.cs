namespace TiendaFlow.Domain;

/// <summary>
/// Partial or total return of an issued invoice (credit note). The issued
/// invoice is never edited: the return is a separate document that puts the
/// returned units back on the shelf and reduces the amount sold.
/// </summary>
public class SalesReturn
{
    public SalesReturn(int invoiceId, int storeId, string reason, int? authorizedByUserId, DateTime createdAt)
    {
        if (invoiceId <= 0)
            throw new InvalidSalesReturnException("The invoice is required.");
        if (storeId <= 0)
            throw new InvalidSalesReturnException("The store is required.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidSalesReturnException("The reason is required.");

        InvoiceId = invoiceId;
        StoreId = storeId;
        Reason = reason.Trim();
        AuthorizedByUserId = authorizedByUserId;
        CreatedAt = UtcTimestamp.Normalize(createdAt);
    }

    public int Id { get; private set; }

    public int InvoiceId { get; private set; }

    public int StoreId { get; private set; }

    public string Reason { get; private set; }

    public int? AuthorizedByUserId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public decimal Total { get; private set; }

    public List<SalesReturnLine> Lines { get; } = new();

    public void AddLine(string productSku, string description, int quantity, decimal unitPrice)
    {
        var sku = (productSku ?? string.Empty).Trim();

        if (Lines.Any(line => string.Equals(line.ProductSku, sku, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidSalesReturnException($"{sku} is already part of the return.");

        Lines.Add(new SalesReturnLine(sku, description, quantity, unitPrice));
        Total = Lines.Sum(line => line.LineTotal);
    }
}

public class SalesReturnLine
{
    public SalesReturnLine(string productSku, string description, int quantity, decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(productSku))
            throw new InvalidSalesReturnException("The product SKU is required.");
        if (quantity <= 0)
            throw new InvalidSalesReturnException("The quantity must be greater than zero.");
        if (unitPrice <= 0)
            throw new InvalidSalesReturnException("The unit price must be greater than zero.");

        ProductSku = productSku.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? ProductSku : description.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public int Id { get; private set; }

    public int SalesReturnId { get; private set; }

    public string ProductSku { get; private set; }

    public string Description { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;
}

public class InvalidSalesReturnException : Exception
{
    public InvalidSalesReturnException(string message) : base(message) { }
}
