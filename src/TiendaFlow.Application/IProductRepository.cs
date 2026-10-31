using TiendaFlow.Domain;

namespace TiendaFlow.Application;

public interface IProductRepository
{
    bool ExistsBySku(string sku);
    bool ExistsByBarcode(string barcode);
    void Add(Product product);
    Product? GetBySku(string sku);
}
