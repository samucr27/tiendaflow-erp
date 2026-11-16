using TiendaFlow.Application.Security;
using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public class StockService
{
    private readonly IStockRepository _repository;
    private readonly IAccessControl _accessControl;

    public StockService(IStockRepository repository, IAccessControl accessControl)
    {
        _repository = repository;
        _accessControl = accessControl;
    }

    public StockInfo GetStock(int storeId, string sku)
    {
        _accessControl.Demand(Permissions.InventoryStockView);

        var item = FindOrThrow(storeId, sku);

        return new StockInfo(item.Sku, item.StoreId, item.QuantityAvailable, item.Location, item.LastMovement);
    }

    public void RegisterSale(int storeId, string sku, int quantity, DateTime date)
    {
        _accessControl.Demand(Permissions.SalesPosUse);

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
        _accessControl.Demand(Permissions.InventoryStockReceive);

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
