using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public interface IStockRepository
{
    StockItem? Get(int storeId, string sku);
    void Add(StockItem item);
    void Update(StockItem item);
}
