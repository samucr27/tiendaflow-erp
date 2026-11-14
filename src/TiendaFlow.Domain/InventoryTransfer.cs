namespace TiendaFlow.Domain;

public enum InventoryTransferStatus
{
    Draft,
    Sent,
    Received,
    Cancelled
}

/// <summary>
/// A transfer of units between two stores. Sending deducts the origin,
/// receiving adds the destination, and cancelling a sent transfer returns the
/// units to the origin. A received transfer is final.
/// </summary>
public class InventoryTransfer
{
    public InventoryTransfer(
        int id,
        int originStoreId,
        int destinationStoreId,
        DateTime createdAt)
    {
        if (originStoreId <= 0)
            throw new InvalidInventoryTransferException("An origin store is required.");
        if (destinationStoreId <= 0)
            throw new InvalidInventoryTransferException("A destination store is required.");
        if (originStoreId == destinationStoreId)
            throw new InvalidInventoryTransferException(
                "The origin and destination stores must be different.");

        Id = id;
        OriginStoreId = originStoreId;
        DestinationStoreId = destinationStoreId;
        CreatedAt = UtcTimestamp.Normalize(createdAt);
        Status = InventoryTransferStatus.Draft;
    }

    public int Id { get; private set; }

    public int OriginStoreId { get; private set; }

    public int DestinationStoreId { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? SentAt { get; private set; }

    public DateTime? ReceivedAt { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public InventoryTransferStatus Status { get; private set; }

    public List<InventoryTransferLine> Lines { get; } = new();

    public int TotalUnits => Lines.Sum(line => line.Quantity);

    public void AddLine(string productSku, int quantity)
    {
        RequireDraft("add lines");

        if (string.IsNullOrWhiteSpace(productSku))
            throw new InvalidInventoryTransferException("The product SKU is required.");
        if (quantity <= 0)
            throw new InvalidInventoryTransferException("The quantity must be greater than zero.");

        var sku = productSku.Trim();

        if (Lines.Any(line => string.Equals(line.ProductSku, sku, StringComparison.Ordinal)))
            throw new InvalidInventoryTransferException($"{sku} is already part of the transfer.");

        Lines.Add(new InventoryTransferLine(Id, sku, quantity));
    }

    public void Send(DateTime sentAt)
    {
        if (Status != InventoryTransferStatus.Draft)
            throw InvalidTransition("send");
        if (Lines.Count == 0)
            throw new InvalidInventoryTransferException("A transfer needs at least one line.");

        Status = InventoryTransferStatus.Sent;
        SentAt = UtcTimestamp.Normalize(sentAt);
    }

    public void Receive(DateTime receivedAt)
    {
        if (Status != InventoryTransferStatus.Sent)
            throw InvalidTransition("receive");

        Status = InventoryTransferStatus.Received;
        ReceivedAt = UtcTimestamp.Normalize(receivedAt);
    }

    public void Cancel(DateTime cancelledAt)
    {
        if (Status is not (InventoryTransferStatus.Draft or InventoryTransferStatus.Sent))
            throw InvalidTransition("cancel");

        Status = InventoryTransferStatus.Cancelled;
        CancelledAt = UtcTimestamp.Normalize(cancelledAt);
    }

    private void RequireDraft(string action)
    {
        if (Status != InventoryTransferStatus.Draft)
            throw InvalidTransition(action);
    }

    private InvalidInventoryTransferException InvalidTransition(string action) =>
        new($"Cannot {action} a transfer in status {Status}.");
}

/// <summary>
/// One product and quantity inside a transfer. The product name is not stored:
/// the read model joins the catalog so a renamed product stays readable.
/// </summary>
public class InventoryTransferLine
{
    public InventoryTransferLine(int transferId, string productSku, int quantity)
    {
        if (string.IsNullOrWhiteSpace(productSku))
            throw new InvalidInventoryTransferException("The product SKU is required.");
        if (quantity <= 0)
            throw new InvalidInventoryTransferException("The quantity must be greater than zero.");

        TransferId = transferId;
        ProductSku = productSku.Trim();
        Quantity = quantity;
    }

    public int Id { get; private set; }

    public int TransferId { get; private set; }

    public string ProductSku { get; private set; }

    public int Quantity { get; private set; }
}

public class InvalidInventoryTransferException : Exception
{
    public InvalidInventoryTransferException(string message) : base(message) { }
}

public class InventoryTransferNotFoundException : Exception
{
    public InventoryTransferNotFoundException(int id)
        : base($"Inventory transfer {id} was not found.") { }
}
