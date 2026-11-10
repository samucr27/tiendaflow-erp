using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfProductCatalogQuery : IProductCatalogQuery
{
    private readonly TiendaFlowDbContext _context;

    public EfProductCatalogQuery(TiendaFlowDbContext context) => _context = context;

    public IReadOnlyList<ProductListItem> Search(string? term)
    {
        var query = _context.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(term))
        {
            var value = term.Trim();

            query = query.Where(product =>
                product.Name.Contains(value)
                || product.Sku.Contains(value)
                || product.Barcode.Contains(value));
        }

        return query
            .OrderBy(product => product.Name)
            .Select(product => new
            {
                product.Sku,
                product.Barcode,
                product.Name,
                product.SalePrice
            })
            .ToList()
            .Select(product => new ProductListItem(
                product.Sku, product.Barcode, product.Name, product.SalePrice))
            .ToList();
    }

    public ProductListItem? GetBySku(string sku) =>
        _context.Products
            .AsNoTracking()
            .Where(product => product.Sku == sku)
            .Select(product => new
            {
                product.Sku,
                product.Barcode,
                product.Name,
                product.SalePrice
            })
            .ToList()
            .Select(product => new ProductListItem(
                product.Sku, product.Barcode, product.Name, product.SalePrice))
            .FirstOrDefault();

    public int Count() => _context.Products.Count();
}
