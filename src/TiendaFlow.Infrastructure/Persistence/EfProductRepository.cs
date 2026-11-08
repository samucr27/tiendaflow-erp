using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public class EfProductRepository : IProductRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfProductRepository(TiendaFlowDbContext context) => _context = context;

    public bool ExistsBySku(string sku) =>
        _context.Products.Any(product => product.Sku == sku);

    public bool ExistsByBarcode(string barcode) =>
        _context.Products.Any(product => product.Barcode == barcode);

    public void Add(Product product)
    {
        _context.Products.Add(product);
        _context.SaveChanges();
    }

    public void Update(Product product)
    {
        if (_context.Entry(product).State == EntityState.Detached)
            _context.Products.Update(product);

        _context.SaveChanges();
    }

    public Product? GetBySku(string sku) => _context.Products.Find(sku);
}
