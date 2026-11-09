using TiendaFlow.Application.Security;

namespace TiendaFlow.Application;

public class InventoryDashboardService
{
    private readonly IProductCatalogQuery _products;
    private readonly IStockQuery _stock;
    private readonly IAccessControl _accessControl;

    public InventoryDashboardService(
        IProductCatalogQuery products,
        IStockQuery stock,
        IAccessControl accessControl)
    {
        _products = products;
        _stock = stock;
        _accessControl = accessControl;
    }

    public InventoryDashboard Get(int storeId)
    {
        _accessControl.Demand(Permissions.InventoryStockView);

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
