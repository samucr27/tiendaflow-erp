using TiendaFlow.Application;
using TiendaFlow.Domain;
using Xunit;

namespace TiendaFlow.UnitTests;

public class InventoryDashboardServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0);

    [Fact]
    public void Get_ComputesUnitsValueAndLowStockForTheStore()
    {
        var products = new FakeProductQuery(new[]
        {
            new ProductListItem("SKU-001", "7701", "Agua 600 ml", 2500m),
            new ProductListItem("SKU-002", "7702", "Pan tajado", 5500m),
            new ProductListItem("SKU-003", "7703", "Jabón", 3000m)
        });

        var stock = new FakeStockQuery(new[]
        {
            new StockRow("SKU-001", "Agua 600 ml", 1, 40, 10, "Pasillo 1", 2500m, null),
            new StockRow("SKU-002", "Pan tajado", 1, 4, 8, "Pasillo 2", 5500m, new InventoryMovement(MovementType.Sale, 2, Now)),
            new StockRow("SKU-003", "Jabón", 2, 15, 5, "Pasillo 3", 3000m, null)
        });

        var dashboard = new InventoryDashboardService(products, stock).Get(storeId: 1);

        Assert.Equal(3, dashboard.ProductCount);
        Assert.Equal(44, dashboard.UnitsInStock);
        Assert.Equal(40 * 2500m + 4 * 5500m, dashboard.StockValue);
        Assert.Equal(2, dashboard.Rows.Count);
        var lowStock = Assert.Single(dashboard.LowStock);
        Assert.Equal("SKU-002", lowStock.Sku);
    }

    [Fact]
    public void Get_WhenTheStoreHasNoStock_ReturnsZerosAndNoLowStock()
    {
        var products = new FakeProductQuery(new[]
        {
            new ProductListItem("SKU-001", "7701", "Agua 600 ml", 2500m)
        });

        var dashboard = new InventoryDashboardService(products, new FakeStockQuery(Array.Empty<StockRow>()))
            .Get(storeId: 9);

        Assert.Equal(1, dashboard.ProductCount);
        Assert.Equal(0, dashboard.UnitsInStock);
        Assert.Equal(0m, dashboard.StockValue);
        Assert.Empty(dashboard.Rows);
        Assert.Empty(dashboard.LowStock);
    }

    [Theory]
    [InlineData(11, 10, false)]
    [InlineData(10, 10, true)]
    [InlineData(9, 10, true)]
    public void StockRow_IsBelowMinimum_ComparesQuantityWithMinimum(int quantity, int minStock, bool expected)
    {
        var row = new StockRow("SKU-001", "Agua 600 ml", 1, quantity, minStock, "Pasillo 1", 2500m, null);

        Assert.Equal(expected, row.IsBelowMinimum);
    }

    private sealed class FakeProductQuery : IProductCatalogQuery
    {
        private readonly IReadOnlyList<ProductListItem> _products;

        public FakeProductQuery(IReadOnlyList<ProductListItem> products) => _products = products;

        public IReadOnlyList<ProductListItem> Search(string? term) => _products;

        public ProductListItem? GetBySku(string sku) =>
            _products.FirstOrDefault(product => product.Sku == sku);

        public int Count() => _products.Count;
    }

    private sealed class FakeStockQuery : IStockQuery
    {
        private readonly IReadOnlyList<StockRow> _rows;

        public FakeStockQuery(IReadOnlyList<StockRow> rows) => _rows = rows;

        public IReadOnlyList<StockRow> ListByStore(int storeId) =>
            _rows.Where(row => row.StoreId == storeId).ToList();

        public IReadOnlyList<StockRow> ListBySku(string sku) =>
            _rows.Where(row => row.Sku == sku).ToList();
    }
}
