using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public class StockService
{
    private readonly IStockRepository _repository;

    public StockService(IStockRepository repository) => _repository = repository;

    public StockInfo GetStock(int storeId, string sku)
    {
        var item = FindOrThrow(storeId, sku);

        return new StockInfo(item.Sku, item.StoreId, item.QuantityAvailable, item.Location, item.LastMovement);
    }

    public void RegisterSale(int storeId, string sku, int quantity, DateTime date)
    {
        var item = FindOrThrow(storeId, sku);

        item.RegisterSale(quantity, date);
        _repository.Update(item);
    }

    /// <summary>
    /// Adds units received at the store. When the SKU has no stock row yet
    /// (for example, a product just registered), it creates one at the
    /// requested location so the first receipt can be recorded.
    /// </summary>
    public void ReceiveStock(
        int storeId,
        string sku,
        int quantity,
        string location,
        int minStock,
        DateTime date)
    {
        var item = _repository.Get(storeId, sku);

        if (item is null)
        {
            var created = new StockItem(sku, storeId, 0, location, minStock);
            created.RegisterReceipt(quantity, date);
            _repository.Add(created);
            return;
        }

        item.RegisterReceipt(quantity, date);
        _repository.Update(item);
    }

    private StockItem FindOrThrow(int storeId, string sku) =>
        _repository.Get(storeId, sku) ?? throw new StockNotFoundException(storeId, sku);
}
