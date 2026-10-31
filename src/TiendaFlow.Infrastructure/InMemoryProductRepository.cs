using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure;

public class InMemoryProductRepository : IProductRepository
{
    public bool ExistsBySku(string sku) => false;
    public bool ExistsByBarcode(string barcode) => false;
    public void Add(Product product) { }
    public Product? GetBySku(string sku) => null;
}
