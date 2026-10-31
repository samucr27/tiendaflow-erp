namespace TiendaFlow.Domain;

public class StockItem
{
    public StockItem(string sku, int storeId, int quantityAvailable, string location)
    {
        Sku = sku;
        StoreId = storeId;
        QuantityAvailable = quantityAvailable;
        Location = location;
    }

    public string Sku { get; }
    public int StoreId { get; }
    public int QuantityAvailable { get; private set; }
    public string Location { get; }
    public InventoryMovement? LastMovement { get; private set; }

    public void RegisterSale(int quantity, DateTime date) { }

    public void RegisterReceipt(int quantity, DateTime date) { }
}
