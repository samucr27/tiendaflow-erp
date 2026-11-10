namespace TiendaFlow.Domain;

/// <summary>
/// One product requested from a supplier. The received quantity grows as the
/// deliveries arrive, which lets the order be tracked partially.
/// </summary>
public class PurchaseOrderLine
{
    public PurchaseOrderLine(
        int orderId,
        string productSku,
        int quantity,
        decimal unitCost,
        int receivedQuantity = 0)
    {
        if (string.IsNullOrWhiteSpace(productSku))
            throw new InvalidPurchaseOrderException("The product SKU is required.");
        if (quantity <= 0)
            throw new InvalidPurchaseOrderException("The ordered quantity must be greater than zero.");
        if (unitCost <= 0)
            throw new InvalidPurchaseOrderException("The unit cost must be greater than zero.");
        if (receivedQuantity is < 0 || receivedQuantity > quantity)
            throw new InvalidPurchaseOrderException("The received quantity is out of range.");

        OrderId = orderId;
        ProductSku = productSku.Trim();
        Quantity = quantity;
        UnitCost = unitCost;
        ReceivedQuantity = receivedQuantity;
    }

    public int OrderId { get; private set; }

    public string ProductSku { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitCost { get; private set; }

    public int ReceivedQuantity { get; private set; }

    public int PendingQuantity => Quantity - ReceivedQuantity;

    public decimal LineTotal => Quantity * UnitCost;

    public void Receive(int quantity)
    {
        if (quantity <= 0)
            throw new InvalidPurchaseOrderException("The received quantity must be greater than zero.");
        if (quantity > PendingQuantity)
            throw new InvalidPurchaseOrderException(
                $"Cannot receive {quantity} units of {ProductSku}; only {PendingQuantity} are pending.");

        ReceivedQuantity += quantity;
    }
}
