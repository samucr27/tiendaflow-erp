using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public class EfProductRepository : IProductRepository
{
    private readonly TiendaFlowDbContext _db;

    public EfProductRepository(TiendaFlowDbContext db)
    {
        _db = db;
    }

    public bool ExistsBySku(string sku) =>
        _db.Products.AsNoTracking().Any(p => p.Sku == sku);

    public bool ExistsByBarcode(string barcode) =>
        _db.Products.AsNoTracking().Any(p => p.Barcode == barcode);

    public void Add(Product product)
    {
        _db.Products.Add(product);
        _db.SaveChanges();
    }

    public void Update(Product product)
    {
        _db.Products.Update(product);
        _db.SaveChanges();
    }

    public Product? GetBySku(string sku) =>
        _db.Products.FirstOrDefault(p => p.Sku == sku);
}