namespace TiendaFlow.Domain;

public class StockItem
{
    public StockItem(string sku, int storeId, int quantityAvailable, string location, int minStock = 0)
    {
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

    public bool IsBelowMinimum() => false;

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

    private static void RequirePositive(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
    }
}
