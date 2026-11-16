namespace TiendaFlow.Domain;

/// <summary>
/// Sale registered at the point of sale (POS). It is the document behind the
/// receipt the cashier sees: it keeps the store, the cashier, the moment of
/// the sale and a snapshot of each product name and price, so reports and the
/// dashboard can count it even if the catalog changes later.
/// </summary>
public class PosSale
{
    public PosSale(string code, int storeId, int? cashierUserId, DateTime soldAt)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidPosSaleException("The sale code is required.");
        if (storeId <= 0)
            throw new InvalidPosSaleException("A store is required.");

        Code = code.Trim();
        StoreId = storeId;
        CashierUserId = cashierUserId;
        SoldAt = UtcTimestamp.Normalize(soldAt);
    }

    public int Id { get; private set; }

    public string Code { get; private set; }

    public int StoreId { get; private set; }

    public int? CashierUserId { get; private set; }

    public DateTime SoldAt { get; private set; }

    public decimal Total { get; private set; }

    public List<PosSaleLine> Lines { get; } = new();

    public void AddLine(string productSku, string productName, int quantity, decimal unitPrice)
    {
        var sku = (productSku ?? string.Empty).Trim();

        if (Lines.Any(line => string.Equals(line.ProductSku, sku, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidPosSaleException($"{sku} is already part of the sale.");

        Lines.Add(new PosSaleLine(sku, productName, quantity, unitPrice));
        Total = Lines.Sum(line => line.LineTotal);
    }
}

public class PosSaleLine
{
    public PosSaleLine(string productSku, string productName, int quantity, decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(productSku))
            throw new InvalidPosSaleException("The product SKU is required.");
        if (string.IsNullOrWhiteSpace(productName))
            throw new InvalidPosSaleException("The product name is required.");
        if (quantity <= 0)
            throw new InvalidPosSaleException("The quantity must be greater than zero.");
        if (unitPrice <= 0)
            throw new InvalidPosSaleException("The unit price must be greater than zero.");

        ProductSku = productSku.Trim();
        ProductName = productName.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public int Id { get; private set; }

    public int PosSaleId { get; private set; }

    public string ProductSku { get; private set; }

    public string ProductName { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;
}

public class InvalidPosSaleException : Exception
{
    public InvalidPosSaleException(string message) : base(message) { }
}
