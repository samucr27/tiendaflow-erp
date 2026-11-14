namespace TiendaFlow.Domain;

public class StockItem
{
    public StockItem(string sku, int storeId, int quantityAvailable, string location, int minStock = 0)
    {
        if (minStock < 0)
            throw new ArgumentOutOfRangeException(nameof(minStock), "Minimum stock cannot be negative.");

        Sku = sku;
        StoreId = storeId;
        QuantityAvailable = quantityAvailable;
        Location = location;
        MinStock = minStock;
    }

    public string Sku { get; }
    public int StoreId { get; }
    public int QuantityAvailable { get; private set; }
    public string Location { get; }
    public int MinStock { get; }
    public InventoryMovement? LastMovement { get; private set; }

    public bool IsBelowMinimum() => QuantityAvailable <= MinStock;

    public void RegisterSale(int quantity, DateTime date)
    {
        RequirePositive(quantity);

        if (quantity > QuantityAvailable)
            throw new InsufficientStockException(Sku, QuantityAvailable, quantity);

        QuantityAvailable -= quantity;
        LastMovement = new InventoryMovement(MovementType.Sale, quantity, date);
    }

    public void RegisterReceipt(int quantity, DateTime date)
    {
        RequirePositive(quantity);

        QuantityAvailable += quantity;
        LastMovement = new InventoryMovement(MovementType.Receipt, quantity, date);
    }

    /// <summary>
    /// Deducts units sent to another store. The origin is validated before the
    /// transfer is sent, so a stock row can never go negative.
    /// </summary>
    public void RegisterTransferOut(int quantity, DateTime date)
    {
        RequirePositive(quantity);

        if (quantity > QuantityAvailable)
            throw new InsufficientStockException(Sku, QuantityAvailable, quantity);

        QuantityAvailable -= quantity;
        LastMovement = new InventoryMovement(MovementType.TransferOut, quantity, date);
    }

    public void RegisterTransferIn(int quantity, DateTime date)
    {
        RequirePositive(quantity);

        QuantityAvailable += quantity;
        LastMovement = new InventoryMovement(MovementType.TransferIn, quantity, date);
    }

    /// <summary>
    /// Sets the stock to the physically counted quantity and records an
    /// adjustment movement with the absolute difference. A count of the same
    /// quantity does not create a movement.
    /// </summary>
    public void AdjustTo(int countedQuantity, DateTime date)
    {
        if (countedQuantity < 0)
            throw new ArgumentOutOfRangeException(
                nameof(countedQuantity),
                "The counted quantity cannot be negative.");

        var difference = countedQuantity - QuantityAvailable;

        if (difference == 0)
            return;

        QuantityAvailable = countedQuantity;
        LastMovement = new InventoryMovement(MovementType.Adjustment, Math.Abs(difference), date);
    }

    private static void RequirePositive(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
    }
}
