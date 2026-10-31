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

    private StockItem FindOrThrow(int storeId, string sku) =>
        _repository.Get(storeId, sku) ?? throw new StockNotFoundException(storeId, sku);
}
