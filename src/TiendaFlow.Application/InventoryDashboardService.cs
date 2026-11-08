namespace TiendaFlow.Application;

public class InventoryDashboardService
{
    private readonly IProductCatalogQuery _products;
    private readonly IStockQuery _stock;

    public InventoryDashboardService(IProductCatalogQuery products, IStockQuery stock)
    {
        _products = products;
        _stock = stock;
    }

    public InventoryDashboard Get(int storeId)
    {
        var rows = _stock.ListByStore(storeId);

        var lowStock = rows
            .Where(row => row.IsBelowMinimum)
            .OrderBy(row => row.QuantityAvailable)
            .ThenBy(row => row.ProductName)
            .ToList();

        return new InventoryDashboard(
            _products.Count(),
            rows.Sum(row => row.QuantityAvailable),
            rows.Sum(row => row.StockValue),
            rows,
            lowStock);
    }
}
