namespace TiendaFlow.Domain;

public class StockNotFoundException : Exception
{
    public StockNotFoundException(int storeId, string sku)
        : base($"No stock found for SKU {sku} in store {storeId}.") { }
}
