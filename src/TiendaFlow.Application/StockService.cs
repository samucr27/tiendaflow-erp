using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public class StockService
{
    private readonly IStockRepository _repository;

    public StockService(IStockRepository repository) => _repository = repository;

    public StockInfo GetStock(int storeId, string sku) => throw new NotImplementedException();

    public void RegisterSale(int storeId, string sku, int quantity, DateTime date) =>
        throw new NotImplementedException();
}
