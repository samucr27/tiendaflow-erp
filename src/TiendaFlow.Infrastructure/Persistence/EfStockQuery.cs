using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfStockQuery : IStockQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfStockQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<StockRow> ListByStore(int storeId) => Read(storeId, sku: null);

    public IReadOnlyList<StockRow> ListBySku(string sku) => Read(storeId: null, sku);

    private IReadOnlyList<StockRow> Read(int? storeId, string? sku)
    {
        var query =
            from stock in _context.Stocks.AsNoTracking()
            join product in _context.Products.AsNoTracking()
                on stock.Sku equals product.Sku into products
            from product in products.DefaultIfEmpty()
            select new { Stock = stock, Product = product };

        if (storeId is not null)
            query = query.Where(row => row.Stock.StoreId == storeId);

        if (sku is not null)
            query = query.Where(row => row.Stock.Sku == sku);

        return query
            .ToList()
            .Select(row => new StockRow(
                row.Stock.Sku,
                row.Product?.Name ?? row.Stock.Sku,
                row.Stock.StoreId,
                row.Stock.QuantityAvailable,
                row.Stock.MinStock,
                row.Stock.Location,
                row.Product?.SalePrice ?? 0m,
                row.Stock.LastMovement))
            .OrderBy(row => row.StoreId)
            .ThenBy(row => row.ProductName)
            .ToList();
    }
}
