using TiendaFlow.Application;
using TiendaFlow.Domain;

namespace TiendaFlow.Infrastructure;

public class InMemoryProductRepository : IProductRepository
{
    private readonly Dictionary<string, Product> _bySku = new();
    private readonly Dictionary<string, Product> _byBarcode = new();

    public bool ExistsBySku(string sku) => _bySku.ContainsKey(sku);

    public bool ExistsByBarcode(string barcode) => _byBarcode.ContainsKey(barcode);

    public void Add(Product product)
    {
        _bySku[product.Sku] = product;
        _byBarcode[product.Barcode] = product;
    }

    public Product? GetBySku(string sku) => _bySku.GetValueOrDefault(sku);
}
