namespace TiendaFlow.Domain;

public enum InventoryCountStatus
{
    Open,
    Closed,
    Adjusted
}

/// <summary>
/// A physical count for a store, optionally restricted to one location. The
/// document is opened with the system quantity of every stock row, the counted
/// quantity is typed by the operator, and the adjustment is applied only once
/// the count is closed. Once adjusted the document is immutable.
/// </summary>
public class InventoryCount
{
    public InventoryCount(int id, int storeId, string? location, DateTime createdAt)
    {
        if (storeId <= 0)
            throw new InvalidInventoryCountException("A store is required.");

        Id = id;
        StoreId = storeId;
        Location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        CreatedAt = UtcTimestamp.Normalize(createdAt);
        Status = InventoryCountStatus.Open;
    }

    public int Id { get; private set; }

    public int StoreId { get; private set; }

    public string? Location { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? ClosedAt { get; private set; }

    public DateTime? AdjustedAt { get; private set; }

    public InventoryCountStatus Status { get; private set; }

    public List<InventoryCountLine> Lines { get; } = new();

    public bool HasDifferences => Lines.Any(line => line.Difference is not 0);

    public void AddLine(string productSku, int systemQuantity)
    {
        RequireOpen("add lines");

        if (string.IsNullOrWhiteSpace(productSku))
            throw new InvalidInventoryCountException("The product SKU is required.");
        if (systemQuantity < 0)
            throw new InvalidInventoryCountException("The system quantity cannot be negative.");

        var sku = productSku.Trim();

        if (Lines.Any(line => string.Equals(line.ProductSku, sku, StringComparison.Ordinal)))
            throw new InvalidInventoryCountException($"{sku} is already part of the count.");

        Lines.Add(new InventoryCountLine(Id, sku, systemQuantity));
    }

    public void SetCounted(string productSku, int countedQuantity)
    {
        RequireOpen("count products");

        if (countedQuantity < 0)
            throw new InvalidInventoryCountException("The counted quantity cannot be negative.");

        var line = Lines.FirstOrDefault(item =>
                string.Equals(item.ProductSku, productSku?.Trim(), StringComparison.Ordinal))
            ?? throw new InvalidInventoryCountException($"{productSku} is not part of the count.");

        line.SetCounted(countedQuantity);
    }

    public void Close(DateTime closedAt)
    {
        if (Status != InventoryCountStatus.Open)
            throw InvalidTransition("close");

        if (Lines.Count == 0)
            throw new InvalidInventoryCountException("A count needs at least one line.");

        if (Lines.Any(line => line.CountedQuantity is null))
            throw new InvalidInventoryCountException("Every line must be counted before closing.");

        Status = InventoryCountStatus.Closed;
        ClosedAt = UtcTimestamp.Normalize(closedAt);
    }

    public void Adjust(DateTime adjustedAt)
    {
        if (Status != InventoryCountStatus.Closed)
            throw InvalidTransition("adjust");

        Status = InventoryCountStatus.Adjusted;
        AdjustedAt = UtcTimestamp.Normalize(adjustedAt);
    }

    private void RequireOpen(string action)
    {
        if (Status != InventoryCountStatus.Open)
            throw InvalidTransition(action);
    }

    private InvalidInventoryCountException InvalidTransition(string action) =>
        new($"Cannot {action} a count in status {Status}.");
}

/// <summary>
/// One product inside a physical count. The system quantity is a snapshot taken
/// when the count was opened, so later receipts or sales never rewrite it.
/// </summary>
public class InventoryCountLine
{
    public InventoryCountLine(int countId, string productSku, int systemQuantity)
    {
        if (string.IsNullOrWhiteSpace(productSku))
            throw new InvalidInventoryCountException("The product SKU is required.");
        if (systemQuantity < 0)
            throw new InvalidInventoryCountException("The system quantity cannot be negative.");

        CountId = countId;
        ProductSku = productSku.Trim();
        SystemQuantity = systemQuantity;
    }

    public int Id { get; private set; }

    public int CountId { get; private set; }

    public string ProductSku { get; private set; }

    public int SystemQuantity { get; private set; }

    public int? CountedQuantity { get; private set; }

    public int? Difference => CountedQuantity is int counted ? counted - SystemQuantity : null;

    public void SetCounted(int countedQuantity)
    {
        if (countedQuantity < 0)
            throw new InvalidInventoryCountException("The counted quantity cannot be negative.");

        CountedQuantity = countedQuantity;
    }
}

public class InvalidInventoryCountException : Exception
{
    public InvalidInventoryCountException(string message) : base(message) { }
}

public class InventoryCountNotFoundException : Exception
{
    public InventoryCountNotFoundException(int id)
        : base($"Inventory count {id} was not found.") { }
}
