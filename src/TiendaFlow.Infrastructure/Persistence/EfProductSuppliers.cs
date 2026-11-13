using Microsoft.EntityFrameworkCore;
using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure.Persistence;

public sealed class EfProductSupplierRepository : IProductSupplierRepository
{
    private readonly TiendaFlowDbContext _context;

    public EfProductSupplierRepository(TiendaFlowDbContext context) => _context = context;

    public ProductSupplier? Get(string productSku, int supplierId)
    {
        var sku = productSku.Trim();

        return _context.ProductSuppliers
            .FirstOrDefault(relation =>
                relation.ProductSku == sku && relation.SupplierId == supplierId);
    }

    public IReadOnlyList<ProductSupplier> List() =>
        _context.ProductSuppliers
            .AsNoTracking()
            .OrderByDescending(relation => relation.IsDefault)
            .ThenBy(relation => relation.ProductSku)
            .ToList();

    public void Add(ProductSupplier relation)
    {
        _context.ProductSuppliers.Add(relation);
        _context.SaveChanges();
    }

    public void Update(ProductSupplier relation)
    {
        if (_context.Entry(relation).State == EntityState.Detached)
            _context.ProductSuppliers.Update(relation);

        _context.SaveChanges();
    }
}
